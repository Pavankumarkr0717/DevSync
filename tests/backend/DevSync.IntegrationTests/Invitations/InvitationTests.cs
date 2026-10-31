using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Time.Testing;

namespace DevSync.IntegrationTests.Invitations;

/// <summary>US4 invitation workflow (FR-020): consent-based, no enumeration, expiry, revocation.</summary>
public sealed class InvitationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private FakeTimeProvider _clock = null!;
    private DevSyncApiFactory _factory = null!;
    private HttpClient _owner = null!;
    private Guid _workspaceId;

    public async ValueTask InitializeAsync()
    {
        _clock = DevSyncApiFactory.NewClock();
        _factory = await DevSyncApiFactory.CreateAsync(postgres, time: _clock);
        _owner = await _factory.ClientForAsync(await _factory.CreateAccountAsync("Ana", "ana@example.com"));
        _workspaceId = (await _owner.CreateWorkspaceAsync("Design Review")).GetProperty("id").GetGuid();
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private string InvitationsPath => $"/api/workspaces/{_workspaceId}/invitations";

    private Task<HttpResponseMessage> InviteAsync(string email, string role = "editor") =>
        _owner.PostAsJsonAsync(InvitationsPath, new { email, role });

    [Fact]
    public async Task Inviting_registered_and_unregistered_emails_looks_identical()
    {
        await _factory.CreateAccountAsync("Ben", "ben@example.com");

        var registered = await InviteAsync("ben@example.com");
        var unregistered = await InviteAsync("nobody-yet@example.com");

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        Assert.Equal(HttpStatusCode.Created, unregistered.StatusCode);
        Assert.Equal(
            await registered.NormalizedBodyAsync("id", "email", "createdAt", "expiresAt"),
            await unregistered.NormalizedBodyAsync("id", "email", "createdAt", "expiresAt"));
    }

    [Fact]
    public async Task The_invitee_sees_accepts_and_becomes_a_member_with_the_offered_role()
    {
        var ben = await _factory.CreateAccountAsync("Ben", "ben@example.com");
        var benClient = await _factory.ClientForAsync(ben);
        var invitation = await (await InviteAsync("BEN@Example.com", "viewer")).ReadJsonAsync();
        Assert.Single((await (await _owner.GetAsync(InvitationsPath)).ReadJsonAsync()).EnumerateArray());

        var inbox = await (await benClient.GetAsync("/api/invitations")).ReadJsonAsync();
        var received = Assert.Single(inbox.EnumerateArray());
        Assert.Equal("Design Review", received.GetProperty("workspace").GetProperty("name").GetString());
        Assert.Equal("viewer", received.GetProperty("role").GetString());
        Assert.Equal("Ana", received.GetProperty("invitedBy").GetProperty("displayName").GetString());

        var accepted = await benClient.PostAsync($"/api/invitations/{invitation.GetProperty("id").GetGuid()}/accept", null);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("viewer", (await accepted.ReadJsonAsync()).GetProperty("role").GetString());
        var details = await (await benClient.GetAsync($"/api/workspaces/{_workspaceId}")).ReadJsonAsync();
        Assert.Equal("viewer", details.GetProperty("myRole").GetString());
        Assert.Empty((await (await benClient.GetAsync("/api/invitations")).ReadJsonAsync()).EnumerateArray());
        Assert.Empty((await (await _owner.GetAsync(InvitationsPath)).ReadJsonAsync()).EnumerateArray());
    }

    [Fact]
    public async Task Declining_creates_no_membership()
    {
        var ben = await _factory.ClientForAsync(await _factory.CreateAccountAsync("Ben", "ben@example.com"));
        var id = (await (await InviteAsync("ben@example.com")).ReadJsonAsync()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await ben.PostAsync($"/api/invitations/{id}/decline", null)).StatusCode);

        await (await ben.GetAsync($"/api/workspaces/{_workspaceId}")).AssertProblemAsync(404, "not_found");
        await (await ben.PostAsync($"/api/invitations/{id}/accept", null)).AssertProblemAsync(404, "not_found");
    }

    [Fact]
    public async Task A_revoked_invitation_can_no_longer_be_accepted()
    {
        var ben = await _factory.ClientForAsync(await _factory.CreateAccountAsync("Ben", "ben@example.com"));
        var id = (await (await InviteAsync("ben@example.com")).ReadJsonAsync()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await _owner.DeleteAsync($"{InvitationsPath}/{id}")).StatusCode);

        await (await ben.PostAsync($"/api/invitations/{id}/accept", null)).AssertProblemAsync(404, "not_found");
        await (await _owner.DeleteAsync($"{InvitationsPath}/{id}")).AssertProblemAsync(404, "not_found");
    }

    [Fact]
    public async Task An_expired_invitation_cannot_be_accepted_and_the_email_can_be_invited_again()
    {
        var ben = await _factory.ClientForAsync(await _factory.CreateAccountAsync("Ben", "ben@example.com"));
        var id = (await (await InviteAsync("ben@example.com")).ReadJsonAsync()).GetProperty("id").GetGuid();

        _clock.Advance(TimeSpan.FromDays(7));
        // Keep Ben's session alive across the jump (idle limit is 7 days).
        ben = await _factory.ClientForAsync(await UserIdAsync("ben@example.com"));

        await (await ben.PostAsync($"/api/invitations/{id}/accept", null)).AssertProblemAsync(404, "not_found");
        _owner = await _factory.ClientForAsync(await UserIdAsync("ana@example.com"));
        Assert.Equal(HttpStatusCode.Created, (await InviteAsync("ben@example.com")).StatusCode);
    }

    [Fact]
    public async Task Another_account_cannot_accept_or_decline_someone_elses_invitation()
    {
        var mallory = await _factory.ClientForAsync(await _factory.CreateAccountAsync("Mallory", "mallory@example.com"));
        var id = (await (await InviteAsync("ben@example.com")).ReadJsonAsync()).GetProperty("id").GetGuid();

        await (await mallory.PostAsync($"/api/invitations/{id}/accept", null)).AssertProblemAsync(404, "not_found");
        await (await mallory.PostAsync($"/api/invitations/{id}/decline", null)).AssertProblemAsync(404, "not_found");
        Assert.Empty((await (await mallory.GetAsync("/api/invitations")).ReadJsonAsync()).EnumerateArray());
        await (await mallory.GetAsync($"/api/workspaces/{_workspaceId}")).AssertProblemAsync(404, "not_found");
    }

    [Fact]
    public async Task Accepting_twice_is_not_found_the_second_time()
    {
        var ben = await _factory.ClientForAsync(await _factory.CreateAccountAsync("Ben", "ben@example.com"));
        var id = (await (await InviteAsync("ben@example.com")).ReadJsonAsync()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await ben.PostAsync($"/api/invitations/{id}/accept", null)).StatusCode);
        await (await ben.PostAsync($"/api/invitations/{id}/accept", null)).AssertProblemAsync(404, "not_found");
    }

    [Fact]
    public async Task Invitation_creation_rules_are_enforced()
    {
        var member = await _factory.CreateAccountAsync("Member", "member@example.com");
        await _factory.AddMemberAsync(_workspaceId, member, Domain.Workspaces.WorkspaceRole.Viewer);
        await InviteAsync("pending@example.com");

        await (await InviteAsync("PENDING@example.com")).AssertProblemAsync(409, "invitation_exists");
        await (await InviteAsync("member@example.com")).AssertProblemAsync(409, "already_member");
        await (await InviteAsync(" Ana@Example.com ")).AssertProblemAsync(400, "cannot_invite_self");
        var badRole = await (await InviteAsync("x@example.com", "owner")).AssertProblemAsync(400, "validation_failed");
        Assert.True(badRole.GetProperty("errors").TryGetProperty("role", out _));
        var badEmail = await (await InviteAsync("not-an-email")).AssertProblemAsync(400, "validation_failed");
        Assert.True(badEmail.GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task At_most_50_pending_invitations_per_workspace()
    {
        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await InviteAsync($"person{i}@example.com")).StatusCode);
        }

        await (await InviteAsync("one-more@example.com")).AssertProblemAsync(409, "too_many_invitations");
    }

    [Fact]
    public async Task Malformed_invitation_ids_are_not_found()
    {
        await (await _owner.PostAsync("/api/invitations/not-a-guid/accept", null)).AssertProblemAsync(404, "not_found");
        await (await _owner.DeleteAsync($"{InvitationsPath}/not-a-guid")).AssertProblemAsync(404, "not_found");
    }

    private async Task<Guid> UserIdAsync(string email)
    {
        await using var scope = _factory.CreateDbScope();
        return scope.Db.Users.Single(u => u.NormalizedEmail == email.ToUpperInvariant()).Id;
    }

    private static string Text(JsonElement e) => e.GetRawText();
}
