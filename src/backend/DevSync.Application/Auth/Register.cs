using DevSync.Application.Common;
using DevSync.Domain.Common;
using Microsoft.Extensions.Logging;

namespace DevSync.Application.Auth;

public sealed record RegisterCommand(string? DisplayName, string? Email, string? Password);

/// <summary>Creates an account and signs it in (spec 002 US1, FR-001–FR-007).</summary>
public sealed class RegisterHandler(IUserAccounts accounts, SessionStarter sessions, ILogger<RegisterHandler> logger)
{
    public const int DisplayNameMaxLength = 100;
    public const int PasswordMinLength = 12;
    public const int PasswordMaxLength = 128;

    public async Task<Outcome<AuthResult>> HandleAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();

        var displayName = command.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length == 0)
        {
            errors.Add(new FieldError("displayName", "Name is required."));
        }
        else if (Text.CodePointLength(displayName) > DisplayNameMaxLength)
        {
            errors.Add(new FieldError("displayName", $"Name must be at most {DisplayNameMaxLength} characters."));
        }
        else if (Text.ContainsControlCharacters(displayName))
        {
            errors.Add(new FieldError("displayName", "Name must not contain control characters."));
        }

        var email = EmailAddress.Create(command.Email);
        errors.AddRange(email.Errors);

        errors.AddRange(ValidatePassword(command.Password, email.IsSuccess ? email.Value : null));

        if (errors.Count > 0)
        {
            return Outcome<AuthResult>.Invalid(errors);
        }

        var created = await accounts.CreateAsync(displayName, email.Value, command.Password!, cancellationToken);
        if (!created.IsSuccess)
        {
            return created.Cast<AuthResult>();
        }

        logger.LogInformation("Security event {Event} for user {UserId}", "auth.register", created.Value.Id);
        return Outcome<AuthResult>.Ok(await sessions.StartAsync(created.Value, cancellationToken));
    }

    /// <summary>12–128 characters, never trimmed, not equal to the email (FR-003).</summary>
    public static IEnumerable<FieldError> ValidatePassword(string? password, string? email)
    {
        if (string.IsNullOrEmpty(password))
        {
            yield return new FieldError("password", "Password is required.");
            yield break;
        }

        var length = Text.CodePointLength(password);
        if (length < PasswordMinLength)
        {
            yield return new FieldError("password", $"Password must be at least {PasswordMinLength} characters.");
        }
        else if (length > PasswordMaxLength)
        {
            yield return new FieldError("password", $"Password must be at most {PasswordMaxLength} characters.");
        }
        else if (email is not null && string.Equals(password, email, StringComparison.OrdinalIgnoreCase))
        {
            yield return new FieldError("password", "Password must not be your email address.");
        }
    }
}
