using DevSync.Application.Common;
using DevSync.Application.Identity;
using DevSync.Application.Workspaces;
using DevSync.Domain.Workspaces;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Infrastructure.Workspaces;

internal sealed class WorkspaceRepository(DevSyncDbContext db) : IWorkspaceRepository
{
    public void Add(Workspace workspace) => db.Workspaces.Add(workspace);

    public Task<Workspace?> GetAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        db.Workspaces.Include(w => w.Members).SingleOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);

    // A single SaveChanges runs in one database transaction, so related changes (a workspace and its
    // owner membership, or a membership and its accepted invitation) are stored together or not at all.
    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task<PagedResult<WorkspaceSummaryDto>> ListForMemberAsync(
        Guid userId, PageRequest page, CancellationToken cancellationToken)
    {
        var query =
            from m in db.WorkspaceMemberships.AsNoTracking()
            where m.UserId == userId
            join w in db.Workspaces.AsNoTracking() on m.WorkspaceId equals w.Id
            select new { Workspace = w, m.Role };

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.Workspace.CreatedAt)
            .ThenByDescending(x => x.Workspace.Id)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .Select(x => new WorkspaceSummaryDto(x.Workspace.Id, x.Workspace.Name, x.Role, x.Workspace.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<WorkspaceSummaryDto>(items, page.Page, page.PageSize, total);
    }

    public async Task<WorkspaceDetailsDto?> GetDetailsForMemberAsync(
        Guid workspaceId, Guid userId, bool includeEmails, CancellationToken cancellationToken)
    {
        // Membership is part of the filter: a non-member gets exactly the same "nothing" as a missing workspace.
        var workspace = await (
                from w in db.Workspaces.AsNoTracking()
                where w.Id == workspaceId
                join m in db.WorkspaceMemberships.AsNoTracking() on new { WorkspaceId = w.Id, UserId = userId }
                    equals new { m.WorkspaceId, m.UserId }
                join o in db.Users.AsNoTracking() on w.OwnerId equals o.Id
                select new
                {
                    w.Id,
                    w.Name,
                    w.Description,
                    Owner = new UserRefDto(o.Id, o.DisplayName),
                    w.CreatedAt,
                    w.UpdatedAt,
                    MyRole = m.Role,
                })
            .SingleOrDefaultAsync(cancellationToken);

        if (workspace is null)
        {
            return null;
        }

        var members = await MembersQuery(workspaceId, includeEmails).ToListAsync(cancellationToken);

        return new WorkspaceDetailsDto(
            workspace.Id,
            workspace.Name,
            workspace.Description,
            workspace.Owner,
            workspace.CreatedAt,
            workspace.UpdatedAt,
            members,
            workspace.MyRole);
    }

    public Task<MemberDto?> GetMemberAsync(Guid workspaceId, Guid userId, bool includeEmail, CancellationToken cancellationToken) =>
        MembersQuery(workspaceId, includeEmail, userId).SingleOrDefaultAsync(cancellationToken);

    public Task<bool> IsMemberByEmailAsync(Guid workspaceId, string normalizedEmail, CancellationToken cancellationToken) =>
        (from m in db.WorkspaceMemberships
         where m.WorkspaceId == workspaceId
         join u in db.Users on m.UserId equals u.Id
         where u.NormalizedEmail == normalizedEmail
         select m).AnyAsync(cancellationToken);

    private IQueryable<MemberDto> MembersQuery(Guid workspaceId, bool includeEmails, Guid? onlyUserId = null) =>
        from m in db.WorkspaceMemberships.AsNoTracking()
        where m.WorkspaceId == workspaceId && (onlyUserId == null || m.UserId == onlyUserId)
        join u in db.Users.AsNoTracking() on m.UserId equals u.Id
        orderby m.JoinedAt, u.Id
        select new MemberDto(m.UserId, u.DisplayName, m.Role, m.JoinedAt, includeEmails ? u.Email : null);
}

internal sealed class WorkspaceAccess(DevSyncDbContext db) : IWorkspaceAccess
{
    public async Task<WorkspaceRole?> GetRoleAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken)
    {
        var roles = await db.WorkspaceMemberships.AsNoTracking()
            .Where(m => m.WorkspaceId == workspaceId && m.UserId == userId)
            .Select(m => m.Role)
            .ToListAsync(cancellationToken);
        return roles.Count == 0 ? null : roles[0];
    }
}
