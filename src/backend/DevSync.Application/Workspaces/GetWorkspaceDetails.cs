using DevSync.Application.Common;
using DevSync.Domain.Workspaces;

namespace DevSync.Application.Workspaces;

/// <summary>Returns details for a workspace the current user belongs to (001 US3; 002 FR-027).</summary>
public sealed class GetWorkspaceDetailsHandler(IWorkspaceRepository workspaces, ICurrentUser currentUser)
{
    /// <returns>The details, or null when the workspace does not exist or the user is not a member.</returns>
    public Task<WorkspaceDetailsDto?> HandleAsync(Guid workspaceId, WorkspaceRole callerRole, CancellationToken cancellationToken) =>
        workspaces.GetDetailsForMemberAsync(
            workspaceId,
            currentUser.UserId,
            WorkspacePermissions.Allows(callerRole, WorkspaceOperation.ViewMemberEmails),
            cancellationToken);
}
