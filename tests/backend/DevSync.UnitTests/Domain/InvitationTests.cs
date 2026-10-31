using DevSync.Domain.Common;
using DevSync.Domain.Workspaces;
using Microsoft.Extensions.Time.Testing;

namespace DevSync.UnitTests.Domain;

public sealed class InvitationTests
{
    private static readonly Guid WorkspaceId = Guid.CreateVersion7();
    private static readonly Guid Owner = Guid.CreateVersion7();
    private static readonly Guid Invitee = Guid.CreateVersion7();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero));

    private Invitation Pending(string email = "ben@example.com") =>
        Invitation.Create(WorkspaceId, email, WorkspaceRole.Editor, Owner, _time).Value;

    [Fact]
    public void Create_trims_and_normalizes_the_email_and_sets_a_7_day_expiry()
    {
        var invitation = Pending("  Ben@Example.com ");

        Assert.Equal("Ben@Example.com", invitation.Email);
        Assert.Equal("BEN@EXAMPLE.COM", invitation.NormalizedEmail);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Equal(invitation.CreatedAt + TimeSpan.FromDays(7), invitation.ExpiresAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("two@@example.com")]
    [InlineData("space in@example.com")]
    [InlineData("nodomain@")]
    [InlineData("@nolocal.com")]
    [InlineData("nodot@localhost")]
    public void Invalid_emails_are_rejected(string? email)
    {
        var result = Invitation.Create(WorkspaceId, email, WorkspaceRole.Editor, Owner, _time);

        Assert.False(result.IsSuccess);
        Assert.Equal("email", Assert.Single(result.Errors).Field);
    }

    [Fact]
    public void Emails_longer_than_254_characters_are_rejected()
    {
        var email = new string('a', 243) + "@example.com"; // 255

        Assert.False(Invitation.Create(WorkspaceId, email, WorkspaceRole.Viewer, Owner, _time).IsSuccess);
    }

    [Fact]
    public void Only_editor_or_viewer_can_be_offered()
    {
        var result = Invitation.Create(WorkspaceId, "ben@example.com", WorkspaceRole.Owner, Owner, _time);

        Assert.Equal("role", Assert.Single(result.Errors).Field);
    }

    [Fact]
    public void The_matching_account_can_accept_before_expiry()
    {
        var invitation = Pending();

        Assert.Null(invitation.Accept(Invitee, "BEN@EXAMPLE.COM", _time.GetUtcNow()));
        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.Equal(Invitee, invitation.AcceptedUserId);
        Assert.Equal(_time.GetUtcNow(), invitation.RespondedAt);
    }

    [Fact]
    public void Another_account_cannot_accept_or_decline()
    {
        var invitation = Pending();

        Assert.NotNull(invitation.Accept(Invitee, "OTHER@EXAMPLE.COM", _time.GetUtcNow()));
        Assert.NotNull(invitation.Decline("OTHER@EXAMPLE.COM", _time.GetUtcNow()));
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public void The_matching_account_can_decline()
    {
        var invitation = Pending();

        Assert.Null(invitation.Decline("BEN@EXAMPLE.COM", _time.GetUtcNow()));
        Assert.Equal(InvitationStatus.Declined, invitation.Status);
    }

    [Fact]
    public void Owner_can_revoke_a_pending_invitation_and_it_cannot_be_accepted_afterwards()
    {
        var invitation = Pending();

        Assert.Null(invitation.Revoke(_time.GetUtcNow()));
        Assert.Equal(InvitationStatus.Revoked, invitation.Status);
        Assert.NotNull(invitation.Accept(Invitee, "BEN@EXAMPLE.COM", _time.GetUtcNow()));
    }

    [Fact]
    public void At_the_expiry_instant_any_touch_expires_the_invitation()
    {
        var invitation = Pending();
        _time.Advance(TimeSpan.FromDays(7) - TimeSpan.FromTicks(1));
        Assert.False(invitation.IsExpired(_time.GetUtcNow()));

        _time.Advance(TimeSpan.FromTicks(1));
        var error = invitation.Accept(Invitee, "BEN@EXAMPLE.COM", _time.GetUtcNow());

        Assert.NotNull(error);
        Assert.Equal(InvitationStatus.Expired, invitation.Status);
    }

    [Fact]
    public void Answered_invitations_cannot_change_again()
    {
        var invitation = Pending();
        invitation.Decline("BEN@EXAMPLE.COM", _time.GetUtcNow());

        Assert.NotNull(invitation.Accept(Invitee, "BEN@EXAMPLE.COM", _time.GetUtcNow()));
        Assert.NotNull(invitation.Revoke(_time.GetUtcNow()));
        Assert.Equal(InvitationStatus.Declined, invitation.Status);
    }

    [Fact]
    public void Email_normalization_matches_identity()
    {
        Assert.Equal("ANA@EXAMPLE.COM", EmailAddress.Normalize(" ana@Example.com "));
    }
}
