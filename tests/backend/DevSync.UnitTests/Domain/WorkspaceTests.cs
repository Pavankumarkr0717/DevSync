using DevSync.Domain.Workspaces;

namespace DevSync.UnitTests.Domain;

public sealed class WorkspaceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.CreateVersion7();

    [Fact]
    public void Create_makes_the_creator_the_only_owner_member()
    {
        var workspace = Workspace.Create("Team", "Notes", Owner, new FixedTimeProvider(Now)).Value;

        Assert.Equal("Team", workspace.Name);
        Assert.Equal("Notes", workspace.Description);
        Assert.Equal(Owner, workspace.OwnerId);
        var member = Assert.Single(workspace.Members);
        Assert.Equal(Owner, member.UserId);
        Assert.Equal(workspace.Id, member.WorkspaceId);
        Assert.Equal(WorkspaceRole.Owner, member.Role);
    }

    [Fact]
    public void Create_takes_all_timestamps_from_the_time_provider()
    {
        var workspace = Workspace.Create("Team", null, Owner, new FixedTimeProvider(Now)).Value;

        Assert.Equal(Now, workspace.CreatedAt);
        Assert.Equal(Now, workspace.UpdatedAt);
        Assert.Equal(Now, Assert.Single(workspace.Members).JoinedAt);
    }

    [Fact]
    public void Create_assigns_a_version_7_uuid()
    {
        var first = Workspace.Create("A", null, Owner, TimeProvider.System).Value;
        var second = Workspace.Create("A", null, Owner, TimeProvider.System).Value;

        Assert.Equal(7, first.Id.Version);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Create_reports_every_invalid_field_at_once()
    {
        var result = Workspace.Create("  ", new string('d', 501), Owner, TimeProvider.System);

        Assert.False(result.IsSuccess);
        Assert.Equal(["description", "name"], result.Errors.Select(e => e.Field).Order());
    }
}
