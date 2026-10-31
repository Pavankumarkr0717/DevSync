using DevSync.Application.Common;
using DevSync.Domain.Common;
using Microsoft.Extensions.Logging;

namespace DevSync.Application.Auth;

public sealed record LoginCommand(string? Email, string? Password);

/// <summary>
/// Signs in with email and password (spec 002 FR-005, FR-008). Unknown emails and wrong passwords
/// produce the same outcome; the failure limiter applies to both alike.
/// </summary>
public sealed class LoginHandler(
    IUserAccounts accounts,
    ILoginAttemptLimiter limiter,
    SessionStarter sessions,
    ILogger<LoginHandler> logger)
{
    public async Task<Outcome<AuthResult>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            errors.Add(new FieldError("email", "Email is required."));
        }

        if (string.IsNullOrEmpty(command.Password))
        {
            errors.Add(new FieldError("password", "Password is required."));
        }

        if (errors.Count > 0)
        {
            return Outcome<AuthResult>.Invalid(errors);
        }

        var email = command.Email!.Trim();
        var normalizedEmail = EmailAddress.Normalize(email);
        if (limiter.IsBlocked(normalizedEmail))
        {
            logger.LogWarning("Security event {Event}", "auth.login.blocked");
            return Outcome<AuthResult>.Fail(ErrorCodes.TooManyAttempts);
        }

        var user = await accounts.VerifyAsync(email, command.Password!, cancellationToken);
        if (user is null)
        {
            limiter.RecordFailure(normalizedEmail);
            logger.LogInformation("Security event {Event}", "auth.login.failed");
            return Outcome<AuthResult>.Fail(ErrorCodes.InvalidCredentials);
        }

        limiter.Reset(normalizedEmail);
        logger.LogInformation("Security event {Event} for user {UserId}", "auth.login.succeeded", user.Id);
        return Outcome<AuthResult>.Ok(await sessions.StartAsync(user, cancellationToken));
    }
}
