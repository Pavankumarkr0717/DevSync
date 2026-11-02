using DevSync.Application.Presence;
using DevSync.Application.Workspaces;
using DevSync.Domain.Workspaces;

namespace DevSync.UnitTests.Application.Presence;

/// <summary>Spec 005 US4: every join is validated and authorized on the server (FR-003, FR-005, FR-006).</summary>
public sealed class WorkspacePresenceServiceTests
{
    private static readonly Guid Workspace = Guid.NewGuid();
    private static readonly Guid Member = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();

    private readonly FakeAccess _access = new();
    private readonly FakeUserDirectory _users = new FakeUserDirectory()
        .With(Member, "Priya Shah", "priya@example.com")
        .With(Stranger, "Mallory", "mallory@example.com");

    private WorkspacePresenceService Service => new(_access, _users);

    private static JoinWorkspaceRequest Request(string? workspaceId, int v = 1) => new(v, workspaceId);

    [Theory]
    [InlineData(WorkspaceRole.Owner)]
    [InlineData(WorkspaceRole.Editor)]
    [InlineData(WorkspaceRole.Viewer)]
    public async Task Members_of_any_role_may_join_under_their_directory_name(WorkspaceRole role)
    {
        _access.Roles[(Workspace, Member)] = role;

        var outcome = await Service.AuthorizeJoinAsync(Member, Request(Workspace.ToString()), CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(Workspace, outcome.Value.WorkspaceId);
        Assert.Equal(PresenceUserDto.For(Member, "Priya Shah"), outcome.Value.User);
    }

    [Fact]
    public async Task A_non_member_gets_the_same_answer_as_for_a_missing_workspace()
    {
        _access.Roles[(Workspace, Member)] = WorkspaceRole.Owner;

        var nonMember = await Service.AuthorizeJoinAsync(Stranger, Request(Workspace.ToString()), CancellationToken.None);
        var missing = await Service.AuthorizeJoinAsync(Stranger, Request(Guid.NewGuid().ToString()), CancellationToken.None);

        Assert.Equal(PresenceErrorCodes.WorkspaceNotFound, nonMember.Error);
        Assert.Equal(PresenceErrorCodes.WorkspaceNotFound, missing.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("{6f1c2b8e-0000-4000-8000-000000000001}")]
    public async Task Malformed_workspace_ids_are_not_found(string? workspaceId)
    {
        var outcome = await Service.AuthorizeJoinAsync(Member, Request(workspaceId), CancellationToken.None);

        Assert.Equal(PresenceErrorCodes.WorkspaceNotFound, outcome.Error);
        Assert.Empty(_access.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task An_unknown_schema_version_is_an_invalid_request(int v)
    {
        _access.Roles[(Workspace, Member)] = WorkspaceRole.Owner;

        var outcome = await Service.AuthorizeJoinAsync(Member, Request(Workspace.ToString(), v), CancellationToken.None);

        Assert.Equal(PresenceErrorCodes.InvalidRequest, outcome.Error);
    }

    [Fact]
    public async Task A_missing_request_is_an_invalid_request()
    {
        var outcome = await Service.AuthorizeJoinAsync(Member, null, CancellationToken.None);

        Assert.Equal(PresenceErrorCodes.InvalidRequest, outcome.Error);
    }

    [Fact]
    public async Task Membership_is_read_fresh_on_every_check()
    {
        _access.Roles[(Workspace, Member)] = WorkspaceRole.Editor;
        Assert.True(await Service.IsMemberAsync(Workspace, Member, CancellationToken.None));

        _access.Roles.Remove((Workspace, Member));

        Assert.False(await Service.IsMemberAsync(Workspace, Member, CancellationToken.None));
        Assert.Equal(2, _access.Calls.Count);
    }

    private sealed class FakeAccess : IWorkspaceAccess
    {
        public Dictionary<(Guid Workspace, Guid User), WorkspaceRole> Roles { get; } = [];

        public List<(Guid, Guid)> Calls { get; } = [];

        public Task<WorkspaceRole?> GetRoleAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken)
        {
            Calls.Add((workspaceId, userId));
            return Task.FromResult(Roles.TryGetValue((workspaceId, userId), out var role) ? role : (WorkspaceRole?)null);
        }
    }
}
