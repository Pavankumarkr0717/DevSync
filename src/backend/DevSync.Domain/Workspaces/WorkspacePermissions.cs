namespace DevSync.Domain.Workspaces;

/// <summary>The single source of truth for what each role may do (spec 002 FR-027, spec 003 FR-024, spec 004 FR-030).</summary>
public static class WorkspacePermissions
{
    public static bool Allows(WorkspaceRole role, WorkspaceOperation operation) => operation switch
    {
        WorkspaceOperation.Read => true,
        WorkspaceOperation.EditDetails => role is WorkspaceRole.Owner or WorkspaceRole.Editor,
        WorkspaceOperation.ViewMemberEmails => role is WorkspaceRole.Owner,
        WorkspaceOperation.ManageMembers => role is WorkspaceRole.Owner,
        WorkspaceOperation.EditFiles => role is WorkspaceRole.Owner or WorkspaceRole.Editor,
        WorkspaceOperation.DeleteFiles => role is WorkspaceRole.Owner,
        WorkspaceOperation.EditDiagrams => role is WorkspaceRole.Owner or WorkspaceRole.Editor,
        WorkspaceOperation.DeleteDiagrams => role is WorkspaceRole.Owner,
        _ => false,
    };
}
