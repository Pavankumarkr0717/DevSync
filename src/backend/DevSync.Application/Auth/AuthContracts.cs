using DevSync.Application.Common;
using DevSync.Domain.Identity;

namespace DevSync.Application.Auth;

public sealed record UserProfileDto(Guid Id, string DisplayName, string Email, DateTimeOffset CreatedAt);

public sealed record AuthSessionDto(string AccessToken, DateTimeOffset AccessTokenExpiresAt, UserProfileDto User);

/// <summary>A started or renewed session: the response body plus the raw refresh token for the cookie.</summary>
public sealed record AuthResult(AuthSessionDto Session, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Session lifetimes (spec 002 FR-011).</summary>
public sealed record SessionSettings(TimeSpan IdleLifetime, TimeSpan AbsoluteLifetime);

/// <summary>Account storage and password verification (ASP.NET Core Identity).</summary>
public interface IUserAccounts
{
    /// <summary>Creates an account. Fails with <c>email_taken</c> or field errors on <c>password</c>.</summary>
    Task<Outcome<UserProfileDto>> CreateAsync(string displayName, string email, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the account if the password matches; otherwise null. Performs equivalent hashing
    /// work for unknown emails so the two cases are indistinguishable (FR-005).
    /// </summary>
    Task<UserProfileDto?> VerifyAsync(string email, string password, CancellationToken cancellationToken);

    Task<UserProfileDto?> GetAsync(Guid userId, CancellationToken cancellationToken);
}

public interface ISessionRepository
{
    void Add(Session session, RefreshToken token);

    Task<(Session Session, RefreshToken Token)?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken);

    Task<Session?> FindAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically marks <paramref name="presented"/> used (only if still unused), stores <paramref name="next"/>
    /// and the session's pending changes. Returns false, changing nothing, if another request used the token first.
    /// </summary>
    Task<bool> TryRotateAsync(Session session, RefreshToken presented, RefreshToken next, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>True if the session exists, belongs to <paramref name="userId"/> and is active at <paramref name="now"/>.</summary>
    Task<bool> IsActiveAsync(Guid sessionId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<int> RevokeAllForUserAsync(Guid userId, DateTimeOffset now, SessionRevocationReason reason, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IAccessTokenIssuer
{
    AccessToken Issue(Guid userId, Guid sessionId);
}

/// <summary>Counts failed sign-ins per normalized email, identically for existing and unknown emails (FR-008).</summary>
public interface ILoginAttemptLimiter
{
    bool IsBlocked(string normalizedEmail);

    void RecordFailure(string normalizedEmail);

    void Reset(string normalizedEmail);
}
