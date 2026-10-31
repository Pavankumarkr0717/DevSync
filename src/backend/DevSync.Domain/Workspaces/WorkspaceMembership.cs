namespace DevSync.Domain.Workspaces;

/// <summary>Links a user to a workspace with a role. Part of the Workspace aggregate.</summary>
public sealed class WorkspaceMembership
{
    internal WorkspaceMembership(Guid workspaceId, Guid userId, WorkspaceRole role, DateTimeOffset joinedAt)
    {
        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid UserId { get; private set; }

    public WorkspaceRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    internal void ChangeRole(WorkspaceRole role) => Role = role;
}
