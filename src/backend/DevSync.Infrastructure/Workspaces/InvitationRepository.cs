using DevSync.Application.Identity;
using DevSync.Application.Workspaces;
using DevSync.Domain.Workspaces;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevSync.Infrastructure.Workspaces;

internal sealed class InvitationRepository(DevSyncDbContext db) : IInvitationRepository
{
    public async Task<bool> TryAddAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        var entry = db.WorkspaceInvitations.Add(invitation);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A concurrent request created the same pending invitation (ux_workspace_invitations_pending).
            entry.State = EntityState.Detached;
            return false;
        }
    }

    public Task<Invitation?> FindAsync(Guid invitationId, CancellationToken cancellationToken) =>
        db.WorkspaceInvitations.SingleOrDefaultAsync(i => i.Id == invitationId, cancellationToken);

    public Task ExpireLapsedAsync(Guid workspaceId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.WorkspaceInvitations
            .Where(i => i.WorkspaceId == workspaceId && i.Status == InvitationStatus.Pending && i.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, InvitationStatus.Expired), cancellationToken);

    public Task<bool> HasPendingAsync(Guid workspaceId, string normalizedEmail, CancellationToken cancellationToken) =>
        db.WorkspaceInvitations.AnyAsync(
            i => i.WorkspaceId == workspaceId && i.NormalizedEmail == normalizedEmail && i.Status == InvitationStatus.Pending,
            cancellationToken);

    public Task<int> CountPendingAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        db.WorkspaceInvitations.CountAsync(
            i => i.WorkspaceId == workspaceId && i.Status == InvitationStatus.Pending,
            cancellationToken);

    public async Task<IReadOnlyList<WorkspaceInvitationDto>> ListOpenForWorkspaceAsync(
        Guid workspaceId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await (
                from i in db.WorkspaceInvitations.AsNoTracking()
                where i.WorkspaceId == workspaceId && i.Status == InvitationStatus.Pending && i.ExpiresAt > now
                join u in db.Users.AsNoTracking() on i.InvitedByUserId equals u.Id
                orderby i.CreatedAt descending, i.Id descending
                select new WorkspaceInvitationDto(i.Id, i.Email, i.Role, new UserRefDto(u.Id, u.DisplayName), i.CreatedAt, i.ExpiresAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ReceivedInvitationDto>> ListOpenForEmailAsync(
        string normalizedEmail, DateTimeOffset now, CancellationToken cancellationToken) =>
        await (
                from i in db.WorkspaceInvitations.AsNoTracking()
                where i.NormalizedEmail == normalizedEmail && i.Status == InvitationStatus.Pending && i.ExpiresAt > now
                join w in db.Workspaces.AsNoTracking() on i.WorkspaceId equals w.Id
                join u in db.Users.AsNoTracking() on i.InvitedByUserId equals u.Id
                orderby i.CreatedAt descending, i.Id descending
                select new ReceivedInvitationDto(
                    i.Id,
                    new WorkspaceRefDto(w.Id, w.Name),
                    i.Role,
                    new UserRefDto(u.Id, u.DisplayName),
                    i.ExpiresAt))
            .ToListAsync(cancellationToken);
}
