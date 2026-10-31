namespace DevSync.Domain.Workspaces;

/// <summary>A member's role in a workspace (spec 002 FR-021). Exactly one Owner per workspace.</summary>
public enum WorkspaceRole
{
    Owner,
    Editor,
    Viewer,
}
