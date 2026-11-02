using DevSync.Application.Presence;

namespace DevSync.UnitTests.Application.Presence;

/// <summary>Spec 005: in-memory presence across tabs, devices, leaves, grace and revocation (FR-007–FR-020).</summary>
public sealed class PresenceTrackerTests
{
    private static readonly Guid WorkspaceA = Guid.NewGuid();
    private static readonly Guid WorkspaceB = Guid.NewGuid();
    private static readonly PresenceUserDto Priya = PresenceUserDto.For(Guid.NewGuid(), "Priya Shah");
    private static readonly PresenceUserDto Alex = PresenceUserDto.For(Guid.NewGuid(), "Alex Kim");

    private readonly PresenceTracker _tracker = new();

    private void Connect(string connectionId, PresenceUserDto user, Guid? sessionId = null) =>
        _tracker.Register(connectionId, user.UserId, sessionId ?? Guid.NewGuid());

    // ---- US1: joining ----

    [Fact]
    public void First_join_announces_the_user_and_returns_a_snapshot_with_them()
    {
        Connect("c1", Priya);

        var join = _tracker.Join("c1", WorkspaceA, Priya)!;

        Assert.NotNull(join.Joined);
        Assert.Equal(PresenceChangeKind.Joined, join.Joined!.Kind);
        Assert.Equal(WorkspaceA, join.Snapshot.WorkspaceId);
        Assert.Equal(1, join.Snapshot.V);
        Assert.Equal([Priya], join.Snapshot.Users);
        Assert.Equal(join.Joined.Version, join.Snapshot.Version);
    }

    [Fact]
    public void A_second_connection_of_the_same_user_is_listed_once_and_not_announced()
    {
        Connect("c1", Priya);
        Connect("c2", Priya);
        var first = _tracker.Join("c1", WorkspaceA, Priya)!;

        var second = _tracker.Join("c2", WorkspaceA, Priya)!;

        Assert.Null(second.Joined);
        Assert.Single(second.Snapshot.Users);
        Assert.Equal(first.Snapshot.Version, second.Snapshot.Version);
        Assert.Equal(["c1", "c2"], _tracker.ConnectionsInWorkspace(WorkspaceA, Priya.UserId).Order());
    }

    [Fact]
    public void Repeating_a_join_on_the_same_connection_changes_nothing()
    {
        Connect("c1", Priya);
        var first = _tracker.Join("c1", WorkspaceA, Priya)!;

        var again = _tracker.Join("c1", WorkspaceA, Priya)!;

        Assert.Null(again.Joined);
        Assert.Null(again.Previous);
        Assert.Equal(first.Snapshot.Version, again.Snapshot.Version);
        Assert.Single(_tracker.ConnectionsInWorkspace(WorkspaceA, Priya.UserId));
    }

    [Fact]
    public void Joining_another_workspace_leaves_the_previous_one()
    {
        Connect("c1", Priya);
        _tracker.Join("c1", WorkspaceA, Priya);

        var join = _tracker.Join("c1", WorkspaceB, Priya)!;

        Assert.Equal(new PresenceDeparture(WorkspaceA, Priya.UserId, join.Previous!.PendingOffline), join.Previous);
        Assert.NotNull(join.Previous.PendingOffline);
        Assert.Equal(WorkspaceB, _tracker.WorkspaceOf("c1"));
        Assert.Empty(_tracker.ConnectionsInWorkspace(WorkspaceA, Priya.UserId));
    }

    [Fact]
    public void Workspaces_are_isolated()
    {
        Connect("c1", Priya);
        Connect("c2", Alex);
        _tracker.Join("c1", WorkspaceA, Priya);

        var inB = _tracker.Join("c2", WorkspaceB, Alex)!;

        Assert.Equal([Alex], inB.Snapshot.Users);
    }

    [Fact]
    public void Snapshots_are_sorted_copies()
    {
        Connect("c1", Priya);
        Connect("c2", Alex);
        var first = _tracker.Join("c1", WorkspaceA, Priya)!;

        var second = _tracker.Join("c2", WorkspaceA, Alex)!;

        Assert.Equal([Priya], first.Snapshot.Users);
        Assert.Equal([Alex, Priya], second.Snapshot.Users);
        Assert.True(second.Snapshot.Version > first.Snapshot.Version);
    }

    [Fact]
    public void A_join_from_an_unknown_or_foreign_connection_is_refused()
    {
        Assert.Null(_tracker.Join("ghost", WorkspaceA, Priya));

        Connect("c1", Alex);
        Assert.Null(_tracker.Join("c1", WorkspaceA, Priya));
    }

    // ---- US2: leaving and the grace period ----

    [Fact]
    public void The_last_connection_leaving_keeps_the_user_listed_until_confirmed()
    {
        Connect("c1", Priya);
        Connect("c2", Alex);
        _tracker.Join("c1", WorkspaceA, Priya);
        _tracker.Join("c2", WorkspaceA, Alex);

        var departure = _tracker.Leave("c2")!;

        Assert.NotNull(departure.PendingOffline);
        Assert.Equal(2, _tracker.Join("c1", WorkspaceA, Priya)!.Snapshot.Users.Count);

        var left = _tracker.ConfirmOffline(WorkspaceA, Alex.UserId, departure.PendingOffline!.Value)!;

        Assert.Equal(PresenceChangeKind.Left, left.Kind);
        Assert.Equal(Alex, left.User);
        Assert.Equal([Priya], left.Snapshot.Users);
    }

    [Fact]
    public void A_rejoin_within_the_grace_period_cancels_the_left_announcement()
    {
        Connect("c1", Alex);
        _tracker.Join("c1", WorkspaceA, Alex);
        var departure = _tracker.Disconnect("c1")!;
        Connect("c2", Alex);

        var rejoin = _tracker.Join("c2", WorkspaceA, Alex)!;

        Assert.Null(rejoin.Joined);
        Assert.Null(_tracker.ConfirmOffline(WorkspaceA, Alex.UserId, departure.PendingOffline!.Value));
        Assert.Equal([Alex], rejoin.Snapshot.Users);
    }

    [Fact]
    public void Leaving_while_another_connection_remains_announces_nothing()
    {
        Connect("c1", Alex);
        Connect("c2", Alex);
        _tracker.Join("c1", WorkspaceA, Alex);
        _tracker.Join("c2", WorkspaceA, Alex);

        var departure = _tracker.Disconnect("c1")!;

        Assert.Null(departure.PendingOffline);
        Assert.Equal(["c2"], _tracker.ConnectionsInWorkspace(WorkspaceA, Alex.UserId));
    }

    [Fact]
    public void Leaving_twice_unknown_connections_and_duplicate_disconnects_are_no_ops()
    {
        Connect("c1", Alex);
        Assert.Null(_tracker.Leave("c1"));
        Assert.Null(_tracker.Leave("ghost"));

        _tracker.Join("c1", WorkspaceA, Alex);
        Assert.NotNull(_tracker.Disconnect("c1"));
        Assert.Null(_tracker.Disconnect("c1"));
        Assert.Null(_tracker.Leave("c1"));
    }

    [Fact]
    public void A_stale_confirmation_token_is_ignored()
    {
        Connect("c1", Alex);
        _tracker.Join("c1", WorkspaceA, Alex);
        _tracker.Leave("c1");

        Assert.Null(_tracker.ConfirmOffline(WorkspaceA, Alex.UserId, Guid.NewGuid()));
        Assert.Null(_tracker.ConfirmOffline(WorkspaceB, Alex.UserId, Guid.NewGuid()));
    }

    // ---- US3: tabs, devices, concurrency ----

    [Fact]
    public async Task Two_connections_ending_at_once_produce_exactly_one_pending_offline()
    {
        for (var round = 0; round < 200; round++)
        {
            var tracker = new PresenceTracker();
            tracker.Register("a", Alex.UserId, Guid.NewGuid());
            tracker.Register("b", Alex.UserId, Guid.NewGuid());
            tracker.Join("a", WorkspaceA, Alex);
            tracker.Join("b", WorkspaceA, Alex);

            var results = await Task.WhenAll(
                Task.Run(() => tracker.Disconnect("a")),
                Task.Run(() => tracker.Disconnect("b")));

            Assert.Single(results, d => d?.PendingOffline is not null);
        }
    }

    [Fact]
    public async Task Concurrent_joins_leaves_and_disconnects_keep_the_state_consistent()
    {
        var users = Enumerable.Range(0, 10).Select(i => PresenceUserDto.For(Guid.NewGuid(), $"User {i}")).ToArray();
        Guid[] workspaces = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        var versions = new System.Collections.Concurrent.ConcurrentBag<(Guid Workspace, long Version)>();

        await Parallel.ForEachAsync(Enumerable.Range(0, 200), async (i, _) =>
        {
            var user = users[i % users.Length];
            var connectionId = $"c{i}";
            _tracker.Register(connectionId, user.UserId, Guid.NewGuid());
            var workspace = workspaces[i % workspaces.Length];
            if (_tracker.Join(connectionId, workspace, user) is { Joined: { } joined })
            {
                versions.Add((workspace, joined.Version));
            }

            await Task.Yield();
            if (_tracker.Join(connectionId, workspaces[(i + 1) % workspaces.Length], user) is { Joined: { } moved })
            {
                versions.Add((moved.WorkspaceId, moved.Version));
            }

            var departure = i % 3 == 0 ? _tracker.Leave(connectionId) : _tracker.Disconnect(connectionId);
            if (departure?.PendingOffline is { } token
                && _tracker.ConfirmOffline(departure.WorkspaceId, departure.UserId, token) is { } left)
            {
                versions.Add((left.WorkspaceId, left.Version));
            }
        });

        // Every version is unique: no change was ever assigned the same number twice.
        Assert.Equal(versions.Count, versions.Select(v => v.Version).Distinct().Count());

        // Whatever is still listed has a live connection or is pending offline; nobody is listed twice.
        foreach (var workspace in workspaces)
        {
            foreach (var user in users)
            {
                var connections = _tracker.ConnectionsInWorkspace(workspace, user.UserId);
                Assert.Equal(connections.Count, connections.Distinct().Count());
                Assert.All(connections, c => Assert.Equal(workspace, _tracker.WorkspaceOf(c)));
            }
        }
    }

    [Fact]
    public void Sessions_and_users_find_their_connections()
    {
        var session = Guid.NewGuid();
        Connect("c1", Alex, session);
        Connect("c2", Alex);
        Connect("c3", Priya);

        Assert.Equal(["c1"], _tracker.ConnectionsForSession(session));
        Assert.Equal(["c1", "c2"], _tracker.ConnectionsForUser(Alex.UserId).Order());

        _tracker.Disconnect("c1");
        Assert.Empty(_tracker.ConnectionsForSession(session));
    }

    // ---- US5: revocation ----

    [Fact]
    public void Revoking_removes_every_connection_at_once_without_a_grace_period()
    {
        Connect("c1", Priya);
        Connect("a1", Alex);
        Connect("a2", Alex);
        _tracker.Join("c1", WorkspaceA, Priya);
        _tracker.Join("a1", WorkspaceA, Alex);
        var before = _tracker.Join("a2", WorkspaceA, Alex)!.Snapshot.Version;

        var revocation = _tracker.Revoke(WorkspaceA, Alex.UserId);

        Assert.Equal(["a1", "a2"], revocation.ConnectionIds.Order());
        Assert.Equal(PresenceChangeKind.Removed, revocation.Removed!.Kind);
        Assert.Equal(before + 1, revocation.Removed.Version);
        Assert.Equal([Priya], revocation.Removed.Snapshot.Users);
        Assert.Null(_tracker.WorkspaceOf("a1"));
        Assert.Null(_tracker.Disconnect("a1"));
    }

    [Fact]
    public void Revoking_a_user_who_is_not_online_does_nothing()
    {
        var revocation = _tracker.Revoke(WorkspaceA, Alex.UserId);

        Assert.Empty(revocation.ConnectionIds);
        Assert.Null(revocation.Removed);
    }

    [Fact]
    public void Revoking_a_user_pending_offline_unlists_them()
    {
        Connect("a1", Alex);
        _tracker.Join("a1", WorkspaceA, Alex);
        var departure = _tracker.Disconnect("a1")!;

        var revocation = _tracker.Revoke(WorkspaceA, Alex.UserId);

        Assert.NotNull(revocation.Removed);
        Assert.Null(_tracker.ConfirmOffline(WorkspaceA, Alex.UserId, departure.PendingOffline!.Value));
    }
}
