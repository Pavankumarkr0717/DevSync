using System.Net;
using System.Net.Http.Json;
using DevSync.Domain.Workspaces;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.SignalR.Client;

namespace DevSync.IntegrationTests.Presence;

/// <summary>Spec 005 US5: removal and ended sessions take effect on live connections (FR-004, FR-020, FR-021, SC-006).</summary>
public sealed class PresenceRevocationTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(700);

    [Fact]
    public async Task Removing_an_online_member_evicts_every_connection_and_is_announced_at_once()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var ownerId = await factory.CreateAccountAsync("Priya Shah");
        var memberId = await factory.CreateAccountAsync("Ben Ortiz");
        var latecomerId = await factory.CreateAccountAsync("Cara Lee");
        var ownerHttp = await factory.ClientForAsync(ownerId);
        var workspace = (await ownerHttp.CreateWorkspaceAsync("Checkout")).GetProperty("id").GetGuid();
        await factory.AddMemberAsync(workspace, memberId, WorkspaceRole.Editor);
        await factory.AddMemberAsync(workspace, latecomerId, WorkspaceRole.Viewer);

        await using var owner = await HubClient.ConnectAsync(factory, ownerId);
        await owner.JoinAsync(workspace);
        await using var tab1 = await HubClient.ConnectAsync(factory, memberId);
        await using var tab2 = await HubClient.ConnectAsync(factory, memberId);
        await tab1.JoinAsync(workspace);
        await tab2.JoinAsync(workspace);
        await owner.NextAsync("UserJoined");

        var response = await ownerHttp.DeleteAsync($"/api/workspaces/{workspace}/members/{memberId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(workspace, (await tab1.NextAsync("AccessRevoked", TimeSpan.FromSeconds(2))).GetProperty("workspaceId").GetGuid());
        Assert.Equal(workspace, (await tab2.NextAsync("AccessRevoked", TimeSpan.FromSeconds(2))).GetProperty("workspaceId").GetGuid());
        var left = await owner.NextAsync("UserLeft", TimeSpan.FromSeconds(2));
        Assert.Equal("Ben Ortiz", left.UserName());
        Assert.Equal("removed", left.GetProperty("reason").GetString());

        // The removed member's connections are out of the group: later changes don't reach them.
        await using var cara = await HubClient.ConnectAsync(factory, latecomerId);
        await cara.JoinAsync(workspace);
        Assert.Equal("Cara Lee", (await owner.NextAsync("UserJoined")).UserName());
        Assert.DoesNotContain(await tab1.DrainAsync(Quiet), e => e.Name is "UserJoined" or "OnlineUsersUpdated");
        Assert.DoesNotContain(await tab2.DrainAsync(TimeSpan.Zero), e => e.Name is "UserJoined" or "OnlineUsersUpdated");

        // And they cannot rejoin.
        await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() => tab1.JoinAsync(workspace));
    }

    [Fact]
    public async Task Changing_a_role_does_not_touch_presence()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var ownerId = await factory.CreateAccountAsync("Priya Shah");
        var memberId = await factory.CreateAccountAsync("Ben Ortiz");
        var ownerHttp = await factory.ClientForAsync(ownerId);
        var workspace = (await ownerHttp.CreateWorkspaceAsync("Checkout")).GetProperty("id").GetGuid();
        await factory.AddMemberAsync(workspace, memberId, WorkspaceRole.Editor);
        await using var owner = await HubClient.ConnectAsync(factory, ownerId);
        await owner.JoinAsync(workspace);
        await using var member = await HubClient.ConnectAsync(factory, memberId);
        await member.JoinAsync(workspace);
        await owner.NextAsync("UserJoined");
        await owner.DrainAsync(TimeSpan.FromMilliseconds(200));
        await member.DrainAsync(TimeSpan.Zero);

        var response = await ownerHttp.PatchAsJsonAsync($"/api/workspaces/{workspace}/members/{memberId}", new { role = "viewer" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await owner.DrainAsync(Quiet));
        Assert.Empty(await member.DrainAsync(TimeSpan.Zero));
        Assert.Equal(HubConnectionState.Connected, member.Connection.State);
    }

    [Fact]
    public async Task Logging_out_closes_that_sessions_connections_only()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var auth = AuthClient.Create(factory);
        await auth.RegisterAsync("ana@example.com");
        var other = AuthClient.Create(factory);
        await other.LoginAsync("ana@example.com");
        await using var thisDevice = await HubClient.ConnectAsync(factory, auth.AccessToken!);
        await using var otherDevice = await HubClient.ConnectAsync(factory, other.AccessToken!);

        await auth.LogoutAsync();

        await thisDevice.Closed.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(300);
        Assert.Equal(HubConnectionState.Connected, otherDevice.Connection.State);
    }

    [Fact]
    public async Task Logging_out_everywhere_closes_every_connection_and_they_cannot_reconnect()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var auth = AuthClient.Create(factory);
        await auth.RegisterAsync("ana@example.com");
        var other = AuthClient.Create(factory);
        await other.LoginAsync("ana@example.com");
        await using var first = await HubClient.ConnectAsync(factory, auth.AccessToken!);
        await using var second = await HubClient.ConnectAsync(factory, other.AccessToken!);

        await auth.LogoutAllAsync();

        await first.Closed.WaitAsync(TimeSpan.FromSeconds(10));
        await second.Closed.WaitAsync(TimeSpan.FromSeconds(10));
        await using var again = HubClient.Build(factory, other.AccessToken);
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => again.StartAsync());
    }

    [Fact]
    public async Task A_connection_is_closed_when_its_access_token_expires()
    {
        var clock = DevSyncApiFactory.NewClock();
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres, time: clock);
        var userId = await factory.CreateAccountAsync("Priya Shah");
        await using var hub = await HubClient.ConnectAsync(factory, userId);

        clock.Advance(TimeSpan.FromMinutes(16));

        await hub.Closed.WaitAsync(TimeSpan.FromSeconds(15));
    }
}
