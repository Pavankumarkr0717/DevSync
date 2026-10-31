using DevSync.Application.Common;
using DevSync.Domain.Workspaces;

namespace DevSync.Application.Workspaces;

/// <summary>Edits name and description (spec 002 US5). Authorization (Owner or Editor) is checked by the caller.</summary>
public sealed class UpdateWorkspaceHandler(IWorkspaceRepository workspaces, ICurrentUser currentUser, TimeProvider time)
{
    public async Task<Outcome<WorkspaceDetailsDto>> HandleAsync(
        Guid workspaceId, CreateWorkspaceCommand command, WorkspaceRole callerRole, CancellationToken cancellationToken)
    {
        var workspace = await workspaces.GetAsync(workspaceId, cancellationToken);
        if (workspace is null)
        {
            return Outcome<WorkspaceDetailsDto>.Fail(ErrorCodes.NotFound);
        }

        var errors = workspace.UpdateDetails(command.Name, command.Description, time);
        if (errors.Count > 0)
        {
            return Outcome<WorkspaceDetailsDto>.Invalid(errors);
        }

        await workspaces.SaveChangesAsync(cancellationToken);
        var details = await workspaces.GetDetailsForMemberAsync(
            workspaceId,
            currentUser.UserId,
            WorkspacePermissions.Allows(callerRole, WorkspaceOperation.ViewMemberEmails),
            cancellationToken);
        return details is null ? Outcome<WorkspaceDetailsDto>.Fail(ErrorCodes.NotFound) : Outcome<WorkspaceDetailsDto>.Ok(details);
    }
}
