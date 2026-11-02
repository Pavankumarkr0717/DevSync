using System.Net;
using System.Net.Http.Headers;
using DevSync.Domain.Workspaces;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.SignalR;

namespace DevSync.IntegrationTests.Presence;

/// <summary>Spec 005 US4: only signed-in members can connect, join or observe (FR-002–FR-006, FR-011, NFR-002, SC-005).</summary>
public sealed class PresenceSecurityTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly CapturingLoggerProvider _logs = new();
    private DevSyncApiFactory _factory = null!;
    private Guid _owner;
    private Guid _stranger;
    private Guid _workspace;

    public async ValueTask InitializeAsync()
    {
        _factory = await DevSyncApiFactory.CreateAsync(postgres, loggerProvider: _logs);
        _owner = await _factory.CreateAccountAsync("Owner");
        _stranger = await _factory.CreateAccountAsync("Mallory");
        _workspace = (await (await _factory.ClientForAsync(_owner)).CreateWorkspaceAsync("Secret")).GetProperty("id").GetGuid();
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private async Task<HttpResponseMessage> NegotiateAsync(string? bearer = null, string? queryToken = null)
    {
        var client = _factory.CreateClient();
        var url = HubClient.HubPath + "/negotiate?negotiateVersion=1" + (queryToken is null ? "" : "&access_token=" + queryToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Connecting_without_a_token_is_refused()
    {
        await (await NegotiateAsync()).AssertProblemAsync(401, "unauthenticated");

        await using var connection = HubClient.Build(_factory, accessToken: null);
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => connection.StartAsync());
    }

    [Fact]
    public async Task Connecting_with_a_garbage_token_is_refused()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await NegotiateAsync(bearer: "not.a.token")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await NegotiateAsync(queryToken: "not.a.token")).StatusCode);
    }

    [Fact]
    public async Task Connecting_with_the_token_of_an_ended_session_is_refused()
    {
        var auth = AuthClient.Create(_factory);
        await auth.RegisterAsync("ana@example.com");
        var token = auth.AccessToken!;
        await auth.LogoutAsync();

        await (await NegotiateAsync(bearer: token)).AssertProblemAsync(401, "session_ended");
    }

    [Fact]
    public async Task The_query_string_token_is_accepted_for_the_hub_only()
    {
        var (token, _) = await _factory.StartSessionAsync(_owner);

        Assert.Equal(HttpStatusCode.OK, (await NegotiateAsync(queryToken: token)).StatusCode);
        var api = await _factory.CreateClient().GetAsync("/api/workspaces?access_token=" + token);
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
    }

    [Fact]
    public async Task A_non_member_cannot_join_and_members_learn_nothing()
    {
        await using var owner = await HubClient.ConnectAsync(_factory, _owner);
        await owner.JoinAsync(_workspace);
        await owner.NextAsync("OnlineUsersUpdated"); // the owner's own join
        await using var mallory = await HubClient.ConnectAsync(_factory, _stranger);

        var error = await Assert.ThrowsAsync<HubException>(() => mallory.JoinAsync(_workspace));

        Assert.EndsWith("workspace_not_found", error.Message);
        Assert.Empty(await owner.DrainAsync(TimeSpan.FromMilliseconds(500)));
        Assert.Empty(await mallory.DrainAsync(TimeSpan.Zero));
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Malformed_workspace_ids_are_not_found(string? workspaceId)
    {
        await using var owner = await HubClient.ConnectAsync(_factory, _owner);

        var error = await Assert.ThrowsAsync<HubException>(() => owner.JoinAsync(workspaceId));

        Assert.EndsWith("workspace_not_found", error.Message);
    }

    [Fact]
    public async Task A_missing_workspace_looks_the_same_as_a_forbidden_one()
    {
        await using var owner = await HubClient.ConnectAsync(_factory, _owner);

        var error = await Assert.ThrowsAsync<HubException>(() => owner.JoinAsync(Guid.NewGuid()));

        Assert.EndsWith("workspace_not_found", error.Message);
    }

    [Fact]
    public async Task An_unknown_schema_version_is_an_invalid_request()
    {
        await using var owner = await HubClient.ConnectAsync(_factory, _owner);

        var error = await Assert.ThrowsAsync<HubException>(() => owner.JoinAsync(_workspace.ToString(), v: 2));

        Assert.EndsWith("invalid_request", error.Message);
    }

    [Fact]
    public async Task Events_never_cross_workspaces()
    {
        var member = await _factory.CreateAccountAsync("Ben");
        var other = (await (await _factory.ClientForAsync(_stranger)).CreateWorkspaceAsync("Public")).GetProperty("id").GetGuid();
        await _factory.AddMemberAsync(_workspace, member, WorkspaceRole.Editor);
        await using var mallory = await HubClient.ConnectAsync(_factory, _stranger);
        await mallory.JoinAsync(other);
        await mallory.NextAsync("OnlineUsersUpdated"); // Mallory's own join, in her workspace
        await using var owner = await HubClient.ConnectAsync(_factory, _owner);
        await owner.JoinAsync(_workspace);

        await using var ben = await HubClient.ConnectAsync(_factory, member);
        await ben.JoinAsync(_workspace);

        Assert.Equal("Ben", (await owner.NextAsync("UserJoined")).UserName());
        Assert.Empty(await mallory.DrainAsync(TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public async Task Tokens_never_reach_the_logs()
    {
        var (token, _) = await _factory.StartSessionAsync(_owner);
        await NegotiateAsync(queryToken: token);
        await using var owner = await HubClient.ConnectAsync(_factory, token);
        await owner.JoinAsync(_workspace);
        await Assert.ThrowsAsync<HubException>(() => owner.JoinAsync("bad-" + token));

        Assert.Contains(_logs.Messages, m => m.Contains("presence.joined", StringComparison.Ordinal));
        Assert.Contains(_logs.Messages, m => m.Contains("presence.join_refused", StringComparison.Ordinal));
        Assert.DoesNotContain(_logs.Messages, m => m.Contains(token, StringComparison.Ordinal));
    }
}
