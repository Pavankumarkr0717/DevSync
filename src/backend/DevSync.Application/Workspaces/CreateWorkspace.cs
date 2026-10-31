using DevSync.Application.Common;
using DevSync.Domain.Common;
using DevSync.Domain.Workspaces;

namespace DevSync.Application.Workspaces;

public sealed record CreateWorkspaceCommand(string? Name, string? Description);

/// <summary>Creates a workspace owned by the current user (001 US1; 002 US3).</summary>
public sealed class CreateWorkspaceHandler(
    IWorkspaceRepository workspaces,
    ICurrentUser currentUser,
    TimeProvider time)
{
    public async Task<Result<WorkspaceDetailsDto>> HandleAsync(CreateWorkspaceCommand command, CancellationToken cancellationToken)
    {
        var ownerId = currentUser.UserId;
        var created = Workspace.Create(command.Name, command.Description, ownerId, time);
        if (!created.IsSuccess)
        {
            return Result<WorkspaceDetailsDto>.Failure(created.Errors);
        }

        workspaces.Add(created.Value);
        await workspaces.SaveChangesAsync(cancellationToken);

        var details = await workspaces.GetDetailsForMemberAsync(created.Value.Id, ownerId, includeEmails: true, cancellationToken)
            ?? throw new InvalidOperationException("The workspace just created could not be read back.");
        return Result<WorkspaceDetailsDto>.Success(details);
    }
}
