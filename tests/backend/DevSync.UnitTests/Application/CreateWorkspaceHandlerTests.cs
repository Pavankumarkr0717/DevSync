using DevSync.Application.Workspaces;
using DevSync.Domain.Workspaces;

namespace DevSync.UnitTests.Application;

public sealed class CreateWorkspaceHandlerTests
{
    private static readonly Guid Owner = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeWorkspaceRepository _repository = new();

    private CreateWorkspaceHandler Handler() => new(_repository, new FakeCurrentUser(Owner), new FixedTimeProvider(Now));

    [Fact]
    public async Task Valid_command_saves_once_and_returns_details_owned_by_the_current_user()
    {
        var result = await Handler().HandleAsync(new CreateWorkspaceCommand(" Team ", "Notes"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(_repository.Added);
        Assert.Equal(1, _repository.SaveCount);

        var details = result.Value;
        Assert.Equal("Team", details.Name);
        Assert.Equal("Notes", details.Description);
        Assert.Equal(Owner, details.Owner.Id);
        Assert.Equal(WorkspaceRole.Owner, details.MyRole);
        Assert.Equal(Now, details.CreatedAt);
        Assert.Equal(Owner, Assert.Single(details.Members).UserId);
        Assert.True(_repository.LastDetailsCall!.Value.IncludeEmails); // the creator is the owner
    }

    [Fact]
    public async Task Invalid_command_returns_field_errors_and_saves_nothing()
    {
        var result = await Handler().HandleAsync(new CreateWorkspaceCommand("", null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("name", Assert.Single(result.Errors).Field);
        Assert.Empty(_repository.Added);
        Assert.Equal(0, _repository.SaveCount);
    }
}
