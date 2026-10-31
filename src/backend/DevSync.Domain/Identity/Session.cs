namespace DevSync.Domain.Identity;

public enum SessionRevocationReason
{
    Logout,
    LogoutAll,
    RefreshTokenReuse,
}

public enum PresentedTokenState
{
    /// <summary>Never used: rotate it.</summary>
    Unused,

    /// <summary>Used moments ago by a concurrent request: the client should retry with its newer cookie.</summary>
    Conflict,

    /// <summary>Used earlier: treat as theft and end the session.</summary>
    Reuse,
}

/// <summary>
/// One signed-in browser or device (spec 002 FR-009–FR-018). Ends after an idle limit (slid on
/// every refresh) or an absolute limit (never extended), or when revoked.
/// </summary>
public sealed class Session
{
    /// <summary>A refresh token presented again within this window counts as a concurrent race, not theft.</summary>
    public static readonly TimeSpan ConflictWindow = TimeSpan.FromSeconds(10);

    private Session()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastRefreshedAt { get; private set; }

    public DateTimeOffset IdleExpiresAt { get; private set; }

    public DateTimeOffset AbsoluteExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public SessionRevocationReason? RevokedReason { get; private set; }

    public static Session Start(Guid userId, DateTimeOffset now, TimeSpan idleLifetime, TimeSpan absoluteLifetime)
    {
        var absolute = now + absoluteLifetime;
        return new Session
        {
            Id = Guid.CreateVersion7(now),
            UserId = userId,
            CreatedAt = now,
            LastRefreshedAt = now,
            AbsoluteExpiresAt = absolute,
            IdleExpiresAt = Min(now + idleLifetime, absolute),
        };
    }

    public bool IsActive(DateTimeOffset now) =>
        RevokedAt is null && now < IdleExpiresAt && now < AbsoluteExpiresAt;

    /// <summary>Records a successful rotation and slides the idle limit, capped at the absolute limit.</summary>
    public void Refreshed(DateTimeOffset now, TimeSpan idleLifetime)
    {
        LastRefreshedAt = now;
        IdleExpiresAt = Min(now + idleLifetime, AbsoluteExpiresAt);
    }

    /// <summary>Ends the session. Revoking twice keeps the first time and reason.</summary>
    public void Revoke(DateTimeOffset now, SessionRevocationReason reason)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevokedReason = reason;
    }

    public static PresentedTokenState ClassifyPresentedToken(RefreshToken token, DateTimeOffset now) => token.UsedAt switch
    {
        null => PresentedTokenState.Unused,
        var used when now - used < ConflictWindow => PresentedTokenState.Conflict,
        _ => PresentedTokenState.Reuse,
    };

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
