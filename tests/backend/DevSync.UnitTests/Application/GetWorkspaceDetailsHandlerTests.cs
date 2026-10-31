using DevSync.Application.Identity;
using DevSync.Application.Workspaces;
using DevSync.Domain.Workspaces;

namespace DevSync.UnitTests.Application;

public sealed class GetWorkspaceDetailsHandlerTests
{
    private static readonly Guid User = Guid.CreateVersion7();
    private static readonly Guid WorkspaceId = Guid.CreateVersion7();

    [Fact]
    public async Task Returns_details_when_the_repository_finds_them_for_the_current_user()
    {
        var details = new WorkspaceDetailsDto(
            WorkspaceId, "Team", null, new UserRefDto(User, "A"), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, [], WorkspaceRole.Owner);
        var repository = new FakeWorkspaceRepository { DetailsToReturn = details };

        var result = await new GetWorkspaceDetailsHandler(repository, new FakeCurrentUser(User))
            .HandleAsync(WorkspaceId, WorkspaceRole.Owner, CancellationToken.None);

        Assert.Same(details, result);
        Assert.Equal((WorkspaceId, User, true), repository.LastDetailsCall);
    }

    [Theory]
    [InlineData(WorkspaceRole.Editor)]
    [InlineData(WorkspaceRole.Viewer)]
    public async Task Member_emails_are_requested_only_for_the_owner(WorkspaceRole role)
    {
        var repository = new FakeWorkspaceRepository();

        await new GetWorkspaceDetailsHandler(repository, new FakeCurrentUser(User)).HandleAsync(WorkspaceId, role, CancellationToken.None);

        Assert.False(repository.LastDetailsCall!.Value.IncludeEmails);
    }

    [Fact]
    public async Task Returns_null_when_missing_or_not_a_member()
    {
        var repository = new FakeWorkspaceRepository();

        var result = await new GetWorkspaceDetailsHandler(repository, new FakeCurrentUser(User))
            .HandleAsync(WorkspaceId, WorkspaceRole.Viewer, CancellationToken.None);

        Assert.Null(result);
    }
}
