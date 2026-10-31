using DevSync.Application.Common;
using DevSync.Domain.Workspaces;

namespace DevSync.Application.Workspaces;

public interface IWorkspaceRepository
{
    void Add(Workspace workspace);

    /// <summary>The aggregate with its members, tracked for changes; null if missing.</summary>
    Task<Workspace?> GetAsync(Guid workspaceId, CancellationToken cancellationToken);

    /// <summary>Persists pending changes atomically (one transaction).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Workspaces the user is a member of, newest first (ties broken by id, descending).</summary>
    Task<PagedResult<WorkspaceSummaryDto>> ListForMemberAsync(Guid userId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>
    /// Details if the workspace exists and the user is a member; otherwise null.
    /// Member emails are included only when <paramref name="includeEmails"/> is true.
    /// </summary>
    Task<WorkspaceDetailsDto?> GetDetailsForMemberAsync(Guid workspaceId, Guid userId, bool includeEmails, CancellationToken cancellationToken);

    Task<MemberDto?> GetMemberAsync(Guid workspaceId, Guid userId, bool includeEmail, CancellationToken cancellationToken);

    Task<bool> IsMemberByEmailAsync(Guid workspaceId, string normalizedEmail, CancellationToken cancellationToken);
}

/// <summary>The caller's current role in a workspace, read fresh on every request (FR-025).</summary>
public interface IWorkspaceAccess
{
    Task<WorkspaceRole?> GetRoleAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken);
}

public interface IInvitationRepository
{
    /// <summary>Adds and saves; false if a pending invitation for the same workspace and email already exists.</summary>
    Task<bool> TryAddAsync(Invitation invitation, CancellationToken cancellationToken);

    Task<Invitation?> FindAsync(Guid invitationId, CancellationToken cancellationToken);

    /// <summary>Marks pending invitations of the workspace whose expiry has passed as expired.</summary>
    Task ExpireLapsedAsync(Guid workspaceId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<bool> HasPendingAsync(Guid workspaceId, string normalizedEmail, CancellationToken cancellationToken);

    Task<int> CountPendingAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceInvitationDto>> ListOpenForWorkspaceAsync(Guid workspaceId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReceivedInvitationDto>> ListOpenForEmailAsync(string normalizedEmail, DateTimeOffset now, CancellationToken cancellationToken);
}
