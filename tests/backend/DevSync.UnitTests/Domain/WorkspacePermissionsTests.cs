using DevSync.Domain.Workspaces;

namespace DevSync.UnitTests.Domain;

/// <summary>Spec 002 FR-027 permission table.</summary>
public sealed class WorkspacePermissionsTests
{
    [Theory]
    [InlineData(WorkspaceRole.Owner, WorkspaceOperation.Read, true)]
    [InlineData(WorkspaceRole.Owner, WorkspaceOperation.EditDetails, true)]
    [InlineData(WorkspaceRole.Owner, WorkspaceOperation.ViewMemberEmails, true)]
    [InlineData(WorkspaceRole.Owner, WorkspaceOperation.ManageMembers, true)]
    [InlineData(WorkspaceRole.Editor, WorkspaceOperation.Read, true)]
    [InlineData(WorkspaceRole.Editor, WorkspaceOperation.EditDetails, true)]
    [InlineData(WorkspaceRole.Editor, WorkspaceOperation.ViewMemberEmails, false)]
    [InlineData(WorkspaceRole.Editor, WorkspaceOperation.ManageMembers, false)]
    [InlineData(WorkspaceRole.Viewer, WorkspaceOperation.Read, true)]
    [InlineData(WorkspaceRole.Viewer, WorkspaceOperation.EditDetails, false)]
    [InlineData(WorkspaceRole.Viewer, WorkspaceOperation.ViewMemberEmails, false)]
    [InlineData(WorkspaceRole.Viewer, WorkspaceOperation.ManageMembers, false)]
    public void Matrix_matches_the_specification(WorkspaceRole role, WorkspaceOperation operation, bool allowed)
    {
        Assert.Equal(allowed, WorkspacePermissions.Allows(role, operation));
    }
}
