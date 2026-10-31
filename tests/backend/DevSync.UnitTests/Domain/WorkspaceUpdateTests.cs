using DevSync.Domain.Workspaces;
using Microsoft.Extensions.Time.Testing;

namespace DevSync.UnitTests.Domain;

/// <summary>Editing name and description reuses the creation rules (spec 002 US5).</summary>
public sealed class WorkspaceUpdateTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Valid_update_trims_and_sets_updated_at()
    {
        var workspace = Workspace.Create("Old", "Old notes", Guid.CreateVersion7(), _time).Value;
        _time.Advance(TimeSpan.FromHours(1));

        var errors = workspace.UpdateDetails("  New  ", "   ", _time);

        Assert.Empty(errors);
        Assert.Equal("New", workspace.Name);
        Assert.Null(workspace.Description);
        Assert.Equal(_time.GetUtcNow(), workspace.UpdatedAt);
        Assert.NotEqual(workspace.CreatedAt, workspace.UpdatedAt);
    }

    [Theory]
    [InlineData("", null, "name")]
    [InlineData("bad\u0007", null, "name")]
    [InlineData("ok", "bell\u0007", "description")]
    public void Invalid_update_changes_nothing(string name, string? description, string field)
    {
        var workspace = Workspace.Create("Old", "Old notes", Guid.CreateVersion7(), _time).Value;
        var updatedAt = workspace.UpdatedAt;
        _time.Advance(TimeSpan.FromHours(1));

        var errors = workspace.UpdateDetails(name, description, _time);

        Assert.Equal(field, Assert.Single(errors).Field);
        Assert.Equal("Old", workspace.Name);
        Assert.Equal("Old notes", workspace.Description);
        Assert.Equal(updatedAt, workspace.UpdatedAt);
    }

    [Fact]
    public void Too_long_values_are_rejected()
    {
        var workspace = Workspace.Create("Old", null, Guid.CreateVersion7(), _time).Value;

        var errors = workspace.UpdateDetails(new string('n', 101), new string('d', 501), _time);

        Assert.Equal(["description", "name"], errors.Select(e => e.Field).Order());
    }
}
