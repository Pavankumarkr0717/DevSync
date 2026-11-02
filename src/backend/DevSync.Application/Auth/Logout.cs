using DevSync.Application.Common;
using DevSync.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace DevSync.Application.Auth;

/// <summary>
/// Ends the current session (FR-017). Idempotent: unknown or already-ended sessions are ignored.
/// Returns the id of the session it ended, or null, so live connections of that session can be closed (spec 005 FR-004).
/// </summary>
public sealed class LogoutHandler(ISessionRepository sessions, TimeProvider time, ILogger<LogoutHandler> logger)
{
    public async Task<Guid?> HandleAsync(string? refreshToken, Guid? sessionId, CancellationToken cancellationToken)
    {
        Session? session = null;
        if (!string.IsNullOrEmpty(refreshToken))
        {
            session = (await sessions.FindByTokenHashAsync(RefreshTokens.Hash(refreshToken), cancellationToken))?.Session;
        }

        if (session is null && sessionId is { } id)
        {
            session = await sessions.FindAsync(id, cancellationToken);
        }

        if (session is null || session.RevokedAt is not null)
        {
            return null;
        }

        session.Revoke(time.GetUtcNow(), SessionRevocationReason.Logout);
        await sessions.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Security event {Event} for user {UserId}", "auth.logout", session.UserId);
        return session.Id;
    }
}

/// <summary>Ends every session of the current user (FR-018).</summary>
public sealed class LogoutAllHandler(
    ISessionRepository sessions,
    ICurrentUser currentUser,
    TimeProvider time,
    ILogger<LogoutAllHandler> logger)
{
    public async Task HandleAsync(CancellationToken cancellationToken)
    {
        var count = await sessions.RevokeAllForUserAsync(
            currentUser.UserId, time.GetUtcNow(), SessionRevocationReason.LogoutAll, cancellationToken);
        logger.LogInformation(
            "Security event {Event} for user {UserId}: {SessionCount} session(s) ended",
            "auth.logout_all", currentUser.UserId, count);
    }
}

/// <summary>The signed-in user's profile (FR-019).</summary>
public sealed class GetProfileHandler(IUserAccounts accounts, ICurrentUser currentUser)
{
    public Task<UserProfileDto?> HandleAsync(CancellationToken cancellationToken) =>
        accounts.GetAsync(currentUser.UserId, cancellationToken);
}
