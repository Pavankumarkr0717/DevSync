using DevSync.Domain.Identity;

namespace DevSync.Application.Auth;

/// <summary>Starts a session for an authenticated account: session row, first refresh token, access token.</summary>
public sealed class SessionStarter(
    ISessionRepository sessions,
    IAccessTokenIssuer accessTokens,
    SessionSettings settings,
    TimeProvider time)
{
    public async Task<AuthResult> StartAsync(UserProfileDto user, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var session = Session.Start(user.Id, now, settings.IdleLifetime, settings.AbsoluteLifetime);
        var refreshToken = RefreshTokens.Generate();
        sessions.Add(session, RefreshToken.Issue(session.Id, RefreshTokens.Hash(refreshToken), now));
        await sessions.SaveChangesAsync(cancellationToken);

        var access = accessTokens.Issue(user.Id, session.Id);
        return new AuthResult(
            new AuthSessionDto(access.Token, access.ExpiresAt, user),
            refreshToken,
            session.AbsoluteExpiresAt);
    }
}
