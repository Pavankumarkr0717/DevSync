using System.Text.Json;
using DevSync.Domain.Workspaces;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Presence;

/// <summary>Spec 005 US1–US3: joining, the online list, join/leave events, tabs and reconnection over a real hub.</summary>
public sealed class PresenceHubTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly TimeSpan Grace = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(900);

    private DevSyncApiFactory _factory = null!;
    private Guid _priya;
    private Guid _alex;
    private Guid _workspace;
    private readonly List<HubClient> _clients = [];

    public async ValueTask InitializeAsync()
    {
        _factory = await DevSyncApiFactory.CreateAsync(
            postgres, settings: new Dictionary<string, string?> { ["DevSync:Presence:GracePeriodSeconds"] = Grace.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        _priya = await _factory.CreateAccountAsync("Priya Shah");
        _alex = await _factory.CreateAccountAsync("Alex Kim");
        var owner = await _factory.ClientForAsync(_priya);
        _workspace = (await owner.CreateWorkspaceAsync("Checkout")).GetProperty("id").GetGuid();
        await _factory.AddMemberAsync(_workspace, _alex, WorkspaceRole.Viewer);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            await client.DisposeAsync();
        }

        await _factory.DisposeAsync();
    }

    private async Task<HubClient> ConnectAsync(Guid userId)
    {
        var client = await HubClient.ConnectAsync(_factory, userId);
        _clients.Add(client);
        return client;
    }

    [Fact]
    public async Task A_member_joins_and_receives_the_online_list_with_themselves()
    {
        var priya = await ConnectAsync(_priya);

        var snapshot = await priya.JoinAsync(_workspace);

        Assert.Equal(1, snapshot.GetProperty("v").GetInt32());
        Assert.Equal(_workspace, snapshot.GetProperty("workspaceId").GetGuid());
        Assert.Equal(["Priya Shah"], snapshot.Names());
        var user = snapshot.GetProperty("users")[0];
        Assert.Equal(_priya, user.GetProperty("userId").GetGuid());
        Assert.Equal("online", user.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, user.GetProperty("avatarUrl").ValueKind);
    }

    [Fact]
    public async Task Others_are_told_once_when_a_member_joins_and_payloads_carry_no_private_data()
    {
        var priya = await ConnectAsync(_priya);
        var first = await priya.JoinAsync(_workspace);
        var alex = await ConnectAsync(_alex);

        var alexSnapshot = await alex.JoinAsync(_workspace);

        var joined = await priya.NextAsync("UserJoined");
        var updated = await priya.NextAsync("OnlineUsersUpdated");
        Assert.Equal("Alex Kim", joined.UserName());
        Assert.Equal(["Alex Kim", "Priya Shah"], updated.Names());
        Assert.Equal(alexSnapshot.Version(), updated.Version());
        Assert.True(updated.Version() > first.Version());
        Assert.Equal(joined.Version(), updated.Version());

        foreach (var payload in new[] { joined, updated, alexSnapshot })
        {
            var json = payload.GetRawText();
            Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("role", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("session", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("connection", json, StringComparison.OrdinalIgnoreCase);
        }

        // The joiner is never told about themselves.
        Assert.DoesNotContain(await alex.DrainAsync(Quiet), e => e.Name == "UserJoined");
    }

    [Fact]
    public async Task The_last_connection_closing_is_announced_after_the_grace_period()
    {
        var priya = await ConnectAsync(_priya);
        await priya.JoinAsync(_workspace);
        var alex = await ConnectAsync(_alex);
        await alex.JoinAsync(_workspace);
        await priya.NextAsync("UserJoined");

        await alex.Connection.StopAsync();

        var left = await priya.NextAsync("UserLeft");
        Assert.Equal("Alex Kim", left.UserName());
        Assert.Equal("left", left.GetProperty("reason").GetString());
        Assert.Equal(["Priya Shah"], (await priya.NextAsync("OnlineUsersUpdated")).Names());
    }

    [Fact]
    public async Task Leaving_explicitly_is_announced_and_repeating_it_is_harmless()
    {
        var priya = await ConnectAsync(_priya);
        await priya.JoinAsync(_workspace);
        var alex = await ConnectAsync(_alex);
        await alex.JoinAsync(_workspace);
        await priya.NextAsync("UserJoined");

        await alex.LeaveAsync();
        await alex.LeaveAsync();

        Assert.Equal("Alex Kim", (await priya.NextAsync("UserLeft")).UserName());
        Assert.DoesNotContain(await priya.DrainAsync(Quiet), e => e.Name == "UserLeft");
    }

    [Fact]
    public async Task A_reconnect_within_the_grace_period_is_silent()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(
            postgres, settings: new Dictionary<string, string?> { ["DevSync:Presence:GracePeriodSeconds"] = "3" });
        var priyaId = await factory.CreateAccountAsync("Priya Shah");
        var alexId = await factory.CreateAccountAsync("Alex Kim");
        var workspace = (await (await factory.ClientForAsync(priyaId)).CreateWorkspaceAsync("W")).GetProperty("id").GetGuid();
        await factory.AddMemberAsync(workspace, alexId, WorkspaceRole.Editor);
        await using var priya = await HubClient.ConnectAsync(factory, priyaId);
        await priya.JoinAsync(workspace);
        var alexToken = (await factory.StartSessionAsync(alexId)).AccessToken;
        var alex = await HubClient.ConnectAsync(factory, alexToken);
        var before = await alex.JoinAsync(workspace);
        await priya.NextAsync("UserJoined");

        // A page reload: the old connection ends, a new one (with a new connection id) joins.
        var oldConnectionId = alex.Connection.ConnectionId;
        await alex.DisposeAsync();
        await using var reloaded = await HubClient.ConnectAsync(factory, alexToken);
        var after = await reloaded.JoinAsync(workspace);

        Assert.NotEqual(oldConnectionId, reloaded.Connection.ConnectionId);
        Assert.Equal(before.Version(), after.Version());
        Assert.Equal(["Alex Kim", "Priya Shah"], after.Names());
        var events = await priya.DrainAsync(TimeSpan.FromSeconds(4));
        Assert.DoesNotContain(events, e => e.Name is "UserLeft" or "UserJoined");
    }

    [Fact]
    public async Task A_user_with_two_connections_is_listed_once_and_leaves_only_with_the_last()
    {
        var priya = await ConnectAsync(_priya);
        await priya.JoinAsync(_workspace);
        var tab1 = await ConnectAsync(_alex);
        var tab2 = await ConnectAsync(_alex);
        await tab1.JoinAsync(_workspace);
        var snapshot = await tab2.JoinAsync(_workspace);
        Assert.Equal(["Alex Kim", "Priya Shah"], snapshot.Names());
        await priya.NextAsync("UserJoined");

        await tab1.Connection.StopAsync();
        Assert.DoesNotContain(await priya.DrainAsync(Quiet), e => e.Name is "UserLeft" or "UserJoined");

        await tab2.Connection.StopAsync();
        Assert.Equal("Alex Kim", (await priya.NextAsync("UserLeft")).UserName());
        Assert.DoesNotContain(await priya.DrainAsync(Quiet), e => e.Name == "UserLeft");
    }

    [Fact]
    public async Task Switching_workspaces_on_one_connection_moves_the_member()
    {
        var other = (await (await _factory.ClientForAsync(_alex)).CreateWorkspaceAsync("Payments")).GetProperty("id").GetGuid();
        var priya = await ConnectAsync(_priya);
        await priya.JoinAsync(_workspace);
        var alex = await ConnectAsync(_alex);
        await alex.JoinAsync(_workspace);
        await priya.NextAsync("UserJoined");
        await alex.DrainAsync(TimeSpan.FromMilliseconds(200)); // Alex's own join in Checkout

        var inOther = await alex.JoinAsync(other);

        Assert.Equal(["Alex Kim"], inOther.Names());
        Assert.Equal("Alex Kim", (await priya.NextAsync("UserLeft")).UserName());

        // Checkout's later events no longer reach Alex.
        var payments = await alex.DrainAsync(Quiet);
        Assert.All(payments, e => Assert.NotEqual(_workspace, e.Payload.GetProperty("workspaceId").GetGuid()));
    }
}
