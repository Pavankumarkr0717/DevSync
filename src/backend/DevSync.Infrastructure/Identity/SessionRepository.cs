using DevSync.Application.Auth;
using DevSync.Domain.Identity;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Infrastructure.Identity;

internal sealed class SessionRepository(DevSyncDbContext db) : ISessionRepository
{
    public void Add(Session session, RefreshToken token)
    {
        db.Sessions.Add(session);
        db.RefreshTokens.Add(token);
    }

    public async Task<(Session Session, RefreshToken Token)?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        if (token is null)
        {
            return null;
        }

        var session = await db.Sessions.SingleAsync(s => s.Id == token.SessionId, cancellationToken);
        return (session, token);
    }

    public Task<Session?> FindAsync(Guid sessionId, CancellationToken cancellationToken) =>
        db.Sessions.SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

    public async Task<bool> TryRotateAsync(
        Session session, RefreshToken presented, RefreshToken next, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Only one request can flip used_at from NULL; a concurrent one waits on the row lock and then updates 0 rows.
        var claimed = await db.RefreshTokens
            .Where(t => t.Id == presented.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), cancellationToken);
        if (claimed != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return false;
        }

        db.RefreshTokens.Add(next);
        await db.SaveChangesAsync(cancellationToken); // new token + slid session expiry
        await db.RefreshTokens
            .Where(t => t.Id == presented.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReplacedById, next.Id), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public Task<bool> IsActiveAsync(Guid sessionId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.Sessions.AsNoTracking().AnyAsync(
            s => s.Id == sessionId
                && s.UserId == userId
                && s.RevokedAt == null
                && s.IdleExpiresAt > now
                && s.AbsoluteExpiresAt > now,
            cancellationToken);

    public Task<int> RevokeAllForUserAsync(Guid userId, DateTimeOffset now, SessionRevocationReason reason, CancellationToken cancellationToken) =>
        db.Sessions
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.RevokedAt, now).SetProperty(x => x.RevokedReason, reason),
                cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
