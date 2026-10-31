using DevSync.Domain.Workspaces;

namespace DevSync.UnitTests.Domain;

/// <summary>Member operations and the immutable-owner invariant (spec 002 FR-021–FR-024).</summary>
public sealed class WorkspaceMembershipRulesTests
{
    private static readonly Guid Owner = Guid.CreateVersion7();
    private static readonly Guid Member = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    private static Workspace WithEditor()
    {
        var workspace = Workspace.Create("Team", null, Owner, new FixedTimeProvider(Now)).Value;
        Assert.Null(workspace.AddMember(Member, WorkspaceRole.Editor, Now));
        return workspace;
    }

    [Fact]
    public void Editor_and_viewer_roles_can_be_swapped()
    {
        var workspace = WithEditor();

        Assert.Null(workspace.ChangeMemberRole(Member, WorkspaceRole.Viewer));
        Assert.Equal(WorkspaceRole.Viewer, workspace.Members.Single(m => m.UserId == Member).Role);
        Assert.Null(workspace.ChangeMemberRole(Member, WorkspaceRole.Editor));
    }

    [Fact]
    public void Nobody_can_be_made_owner()
    {
        var workspace = WithEditor();

        Assert.Equal("validation_failed", workspace.ChangeMemberRole(Member, WorkspaceRole.Owner)!.Code);
        Assert.Equal("validation_failed", workspace.AddMember(Guid.CreateVersion7(), WorkspaceRole.Owner, Now)!.Code);
        Assert.Single(workspace.Members, m => m.Role == WorkspaceRole.Owner);
    }

    [Fact]
    public void The_owner_cannot_be_changed_or_removed()
    {
        var workspace = WithEditor();

        Assert.Equal(WorkspaceErrors.CannotChangeOwner, workspace.ChangeMemberRole(Owner, WorkspaceRole.Viewer));
        Assert.Equal(WorkspaceErrors.CannotRemoveOwner, workspace.RemoveMember(Owner));
        Assert.Equal(WorkspaceRole.Owner, workspace.Members.Single(m => m.UserId == Owner).Role);
    }

    [Fact]
    public void Unknown_members_are_reported()
    {
        var workspace = WithEditor();

        Assert.Equal(WorkspaceErrors.MemberNotFound, workspace.ChangeMemberRole(Guid.CreateVersion7(), WorkspaceRole.Viewer));
        Assert.Equal(WorkspaceErrors.MemberNotFound, workspace.RemoveMember(Guid.CreateVersion7()));
    }

    [Fact]
    public void A_member_cannot_be_added_twice()
    {
        var workspace = WithEditor();

        Assert.Equal(WorkspaceErrors.AlreadyMember, workspace.AddMember(Member, WorkspaceRole.Viewer, Now));
        Assert.Equal(WorkspaceErrors.AlreadyMember, workspace.AddMember(Owner, WorkspaceRole.Viewer, Now));
    }

    [Fact]
    public void Removing_a_member_leaves_the_owner()
    {
        var workspace = WithEditor();

        Assert.Null(workspace.RemoveMember(Member));
        Assert.Equal(Owner, Assert.Single(workspace.Members).UserId);
    }
}
