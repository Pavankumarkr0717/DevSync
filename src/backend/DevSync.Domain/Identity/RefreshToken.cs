namespace DevSync.Domain.Identity;

/// <summary>
/// One link in a session's chain of single-use refresh tokens. Only the SHA-256 hash of the
/// token is stored (spec 002 FR-014).
/// </summary>
public sealed class RefreshToken
{
    public const int HashLength = 32;

    private RefreshToken()
    {
        TokenHash = [];
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public byte[] TokenHash { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public Guid? ReplacedById { get; private set; }

    public static RefreshToken Issue(Guid sessionId, byte[] tokenHash, DateTimeOffset now)
    {
        if (tokenHash.Length != HashLength)
        {
            throw new ArgumentException($"Token hash must be {HashLength} bytes.", nameof(tokenHash));
        }

        return new RefreshToken
        {
            Id = Guid.CreateVersion7(now),
            SessionId = sessionId,
            TokenHash = tokenHash,
            IssuedAt = now,
        };
    }

    public void MarkUsed(DateTimeOffset now) => UsedAt ??= now;
}
