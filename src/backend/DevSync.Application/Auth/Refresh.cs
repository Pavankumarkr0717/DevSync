using DevSync.Application.Common;
using DevSync.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace DevSync.Application.Auth;

/// <summary>
/// Rotates a refresh token (spec 002 FR-010–FR-013): an unused token is exchanged for a new one;
/// a token used moments ago is a concurrent race (<c>refresh_conflict</c>, retry); a token used
/// earlier is treated as stolen and ends the whole session.
/// </summary>
public sealed class RefreshHandler(
    ISessionRepository sessions,
    IUserAccounts accounts,
    IAccessTokenIssuer accessTokens,
    SessionSettings settings,
    TimeProvider time,
    ILogger<RefreshHandler> logger)
{
    public async Task<Outcome<AuthResult>> HandleAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Outcome<AuthResult>.Fail(ErrorCodes.SessionEnded);
        }

        var found = await sessions.FindByTokenHashAsync(RefreshTokens.Hash(refreshToken), cancellationToken);
        if (found is null)
        {
            return Outcome<AuthResult>.Fail(ErrorCodes.SessionEnded);
        }

        var (session, presented) = found.Value;
        var now = time.GetUtcNow();
        if (!session.IsActive(now))
        {
            return Outcome<AuthResult>.Fail(ErrorCodes.SessionEnded);
        }

        switch (Session.ClassifyPresentedToken(presented, now))
        {
            case PresentedTokenState.Conflict:
                return Outcome<AuthResult>.Fail(ErrorCodes.RefreshConflict);

            case PresentedTokenState.Reuse:
                session.Revoke(now, SessionRevocationReason.RefreshTokenReuse);
                await sessions.SaveChangesAsync(cancellationToken);
                logger.LogWarning(
                    "Security event {Event}: session {SessionId} of user {UserId} revoked",
                    "auth.refresh.reuse_detected", session.Id, session.UserId);
                return Outcome<AuthResult>.Fail(ErrorCodes.SessionEnded);
        }

        var user = await accounts.GetAsync(session.UserId, cancellationToken);
        if (user is null)
        {
            return Outcome<AuthResult>.Fail(ErrorCodes.SessionEnded);
        }

        var nextToken = RefreshTokens.Generate();
        var next = RefreshToken.Issue(session.Id, RefreshTokens.Hash(nextToken), now);
        session.Refreshed(now, settings.IdleLifetime);
        if (!await sessions.TryRotateAsync(session, presented, next, now, cancellationToken))
        {
            // Another request rotated this token a moment ago.
            return Outcome<AuthResult>.Fail(ErrorCodes.RefreshConflict);
        }

        var access = accessTokens.Issue(user.Id, session.Id);
        return Outcome<AuthResult>.Ok(new AuthResult(
            new AuthSessionDto(access.Token, access.ExpiresAt, user),
            nextToken,
            session.AbsoluteExpiresAt));
    }
}
