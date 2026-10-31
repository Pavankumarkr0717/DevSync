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

    // Spec 003 FR-024
    [InlineData(WorkspaceRole.Owner, WorkspaceOperation.EditFiles, true)]
    [InlineData(WorkspaceRole.Owner, WorkspaceOperation.DeleteFiles, true)]
    [InlineData(WorkspaceRole.Editor, WorkspaceOperation.EditFiles, true)]
    [InlineData(WorkspaceRole.Editor, WorkspaceOperation.DeleteFiles, false)]
    [InlineData(WorkspaceRole.Viewer, WorkspaceOperation.EditFiles, false)]
    [InlineData(WorkspaceRole.Viewer, WorkspaceOperation.DeleteFiles, false)]

    // Spec 004 FR-030
    [InlineData(WorkspaceRole.Owner, WorkspaceOperation.EditDiagrams, true)]
    [InlineData(WorkspaceRole.Owner, WorkspaceOperation.DeleteDiagrams, true)]
    [InlineData(WorkspaceRole.Editor, WorkspaceOperation.EditDiagrams, true)]
    [InlineData(WorkspaceRole.Editor, WorkspaceOperation.DeleteDiagrams, false)]
    [InlineData(WorkspaceRole.Viewer, WorkspaceOperation.EditDiagrams, false)]
    [InlineData(WorkspaceRole.Viewer, WorkspaceOperation.DeleteDiagrams, false)]
    public void Matrix_matches_the_specification(WorkspaceRole role, WorkspaceOperation operation, bool allowed)
    {
        Assert.Equal(allowed, WorkspacePermissions.Allows(role, operation));
    }
}
