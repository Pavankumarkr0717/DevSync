using DevSync.Application.Auth;
using DevSync.Application.Common;
using DevSync.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevSync.Infrastructure.Identity;

/// <summary>Accounts and password checks through ASP.NET Core Identity's <see cref="UserManager{TUser}"/>.</summary>
internal sealed class UserAccounts(UserManager<AppUser> users, IPasswordHasher<AppUser> hasher, TimeProvider time) : IUserAccounts
{
    private static readonly AppUser DummyUser = new();
    private static string? s_dummyHash;

    public async Task<Outcome<UserProfileDto>> CreateAsync(
        string displayName, string email, string password, CancellationToken cancellationToken)
    {
        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            DisplayName = displayName,
            CreatedAt = time.GetUtcNow(),
            LockoutEnabled = false,
        };

        IdentityResult result;
        try
        {
            result = await users.CreateAsync(user, password);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A concurrent registration of the same email won the race.
            return Outcome<UserProfileDto>.Fail(ErrorCodes.EmailTaken);
        }

        if (result.Succeeded)
        {
            return Outcome<UserProfileDto>.Ok(ToProfile(user));
        }

        if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
        {
            return Outcome<UserProfileDto>.Fail(ErrorCodes.EmailTaken);
        }

        var fieldErrors = result.Errors
            .Select(e => new FieldError(e.Code.StartsWith("Password", StringComparison.Ordinal) ? "password" : "email", e.Description))
            .ToList();
        return Outcome<UserProfileDto>.Invalid(fieldErrors);
    }

    public async Task<UserProfileDto?> VerifyAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            // Same hashing work as a real check, so unknown emails cannot be told apart by timing (FR-005).
            s_dummyHash ??= hasher.HashPassword(DummyUser, Guid.NewGuid().ToString());
            hasher.VerifyHashedPassword(DummyUser, s_dummyHash, password);
            return null;
        }

        return await users.CheckPasswordAsync(user, password) ? ToProfile(user) : null;
    }

    public async Task<UserProfileDto?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is null ? null : ToProfile(user);
    }

    private static UserProfileDto ToProfile(AppUser user) =>
        new(user.Id, user.DisplayName, user.Email!, user.CreatedAt);
}

/// <summary>Adds DevSync's rules to Identity's: at most 128 characters, not equal to the email (FR-003).</summary>
internal sealed class PasswordPolicyValidator : IPasswordValidator<AppUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user, string? password)
    {
        if (password is null || Text.CodePointLength(password) > RegisterHandler.PasswordMaxLength)
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordTooLong",
                Description = $"Password must be at most {RegisterHandler.PasswordMaxLength} characters.",
            }));
        }

        if (string.Equals(password, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordEqualsEmail",
                Description = "Password must not be your email address.",
            }));
        }

        return Task.FromResult(IdentityResult.Success);
    }
}
