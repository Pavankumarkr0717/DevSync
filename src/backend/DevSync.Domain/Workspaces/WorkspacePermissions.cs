namespace DevSync.Domain.Workspaces;

/// <summary>The single source of truth for what each role may do (spec 002 FR-027).</summary>
public static class WorkspacePermissions
{
    public static bool Allows(WorkspaceRole role, WorkspaceOperation operation) => operation switch
    {
        WorkspaceOperation.Read => true,
        WorkspaceOperation.EditDetails => role is WorkspaceRole.Owner or WorkspaceRole.Editor,
        WorkspaceOperation.ViewMemberEmails => role is WorkspaceRole.Owner,
        WorkspaceOperation.ManageMembers => role is WorkspaceRole.Owner,
        _ => false,
    };
}
