using DevSync.Application.Common;
using DevSync.Application.Workspaces;
using DevSync.Domain.Workspaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DevSync.UnitTests.Application;

/// <summary>Invitation creation rules (spec 002 FR-020).</summary>
public sealed class InviteHandlerTests
{
    private static readonly Guid Owner = Guid.CreateVersion7();
    private static readonly Guid WorkspaceId = Guid.CreateVersion7();

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero));
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeInvitationRepository _invitations = new();
    private readonly FakeUserDirectory _users = new FakeUserDirectory().With(Owner, "Ana", "ana@example.com");

    private InviteMemberHandler Handler() =>
        new(_workspaces, _invitations, _users, new FakeCurrentUser(Owner), _time, NullLogger<InviteMemberHandler>.Instance);

    private Task<Outcome<WorkspaceInvitationDto>> InviteAsync(string email, string role = "editor") =>
        Handler().HandleAsync(WorkspaceId, new InviteMemberCommand(email, role), CancellationToken.None);

    [Fact]
    public async Task Creates_a_pending_invitation_with_the_offered_role()
    {
        var outcome = await InviteAsync("ben@example.com", "Viewer");

        Assert.True(outcome.IsSuccess);
        Assert.Equal(WorkspaceRole.Viewer, outcome.Value.Role);
        Assert.Equal("Ana", outcome.Value.InvitedBy.DisplayName);
        Assert.Single(_invitations.Items);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    [InlineData("")]
    public async Task Only_editor_or_viewer_can_be_offered(string role)
    {
        var outcome = await InviteAsync("ben@example.com", role);

        Assert.Equal(ErrorCodes.ValidationFailed, outcome.Error);
        Assert.Contains(outcome.FieldErrors, e => e.Field == "role");
    }

    [Fact]
    public async Task The_owner_cannot_invite_their_own_email_in_any_case()
    {
        Assert.Equal(ErrorCodes.CannotInviteSelf, (await InviteAsync(" ANA@Example.com ")).Error);
    }

    [Fact]
    public async Task A_current_member_cannot_be_invited()
    {
        _workspaces.MemberEmails.Add("BEN@EXAMPLE.COM");

        Assert.Equal(ErrorCodes.AlreadyMember, (await InviteAsync("ben@example.com")).Error);
    }

    [Fact]
    public async Task A_second_pending_invitation_for_the_same_email_is_refused()
    {
        await InviteAsync("ben@example.com");

        Assert.Equal(ErrorCodes.InvitationExists, (await InviteAsync("BEN@example.com")).Error);
    }

    [Fact]
    public async Task An_expired_invitation_is_replaced_by_a_new_one()
    {
        await InviteAsync("ben@example.com");
        _time.Advance(Invitation.Lifetime);

        var outcome = await InviteAsync("ben@example.com");

        Assert.True(outcome.IsSuccess);
        Assert.Equal(InvitationStatus.Expired, _invitations.Items[0].Status);
    }

    [Fact]
    public async Task At_most_50_pending_invitations_per_workspace()
    {
        for (var i = 0; i < InviteMemberHandler.MaxPendingPerWorkspace; i++)
        {
            Assert.True((await InviteAsync($"person{i}@example.com")).IsSuccess);
        }

        Assert.Equal(ErrorCodes.TooManyInvitations, (await InviteAsync("one-too-many@example.com")).Error);
    }

    [Fact]
    public async Task A_concurrent_duplicate_is_reported_as_existing()
    {
        _invitations.SimulateRace = true;

        Assert.Equal(ErrorCodes.InvitationExists, (await InviteAsync("ben@example.com")).Error);
    }
}
