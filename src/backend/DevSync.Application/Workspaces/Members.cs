using DevSync.Application.Common;
using DevSync.Domain.Workspaces;
using Microsoft.Extensions.Logging;

namespace DevSync.Application.Workspaces;

public static class AssignableRoles
{
    public const string Field = "role";

    /// <summary>Parses "editor" or "viewer" (case-insensitive). Owner can never be assigned (FR-023).</summary>
    public static WorkspaceRole? Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "editor" => WorkspaceRole.Editor,
        "viewer" => WorkspaceRole.Viewer,
        _ => null,
    };

    public const string Message = "Role must be editor or viewer.";
}

/// <summary>Owner changes a member's role between Editor and Viewer (FR-022).</summary>
public sealed class ChangeMemberRoleHandler(
    IWorkspaceRepository workspaces,
    ICurrentUser currentUser,
    ILogger<ChangeMemberRoleHandler> logger)
{
    public async Task<Outcome<MemberDto>> HandleAsync(Guid workspaceId, Guid memberUserId, string? role, CancellationToken cancellationToken)
    {
        var newRole = AssignableRoles.Parse(role);
        if (newRole is null)
        {
            return Outcome<MemberDto>.Invalid(AssignableRoles.Field, AssignableRoles.Message);
        }

        var workspace = await workspaces.GetAsync(workspaceId, cancellationToken);
        if (workspace is null)
        {
            return Outcome<MemberDto>.Fail(ErrorCodes.NotFound);
        }

        if (workspace.ChangeMemberRole(memberUserId, newRole.Value) is { } error)
        {
            return Outcome<MemberDto>.From(error);
        }

        await workspaces.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Security event {Event} in workspace {WorkspaceId}: user {TargetUserId} is now {Role} (by {UserId})",
            "membership.role_changed", workspaceId, memberUserId, newRole, currentUser.UserId);

        var member = await workspaces.GetMemberAsync(workspaceId, memberUserId, includeEmail: true, cancellationToken);
        return member is null ? Outcome<MemberDto>.Fail(ErrorCodes.MemberNotFound) : Outcome<MemberDto>.Ok(member);
    }
}

/// <summary>Owner removes a member (FR-022).</summary>
public sealed class RemoveMemberHandler(
    IWorkspaceRepository workspaces,
    ICurrentUser currentUser,
    ILogger<RemoveMemberHandler> logger)
{
    public async Task<Outcome<bool>> HandleAsync(Guid workspaceId, Guid memberUserId, CancellationToken cancellationToken)
    {
        var workspace = await workspaces.GetAsync(workspaceId, cancellationToken);
        if (workspace is null)
        {
            return Outcome<bool>.Fail(ErrorCodes.NotFound);
        }

        if (workspace.RemoveMember(memberUserId) is { } error)
        {
            return Outcome<bool>.From(error);
        }

        await workspaces.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Security event {Event} in workspace {WorkspaceId}: user {TargetUserId} removed (by {UserId})",
            "membership.removed", workspaceId, memberUserId, currentUser.UserId);
        return Outcome<bool>.Ok(true);
    }
}
