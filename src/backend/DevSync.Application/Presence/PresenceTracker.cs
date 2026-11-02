namespace DevSync.Application.Presence;

/// <summary>
/// In-memory presence for a single API instance (spec 005 research R5, R6). One lock guards all
/// state so "first connection" and "last connection" decisions are atomic; snapshots are copied
/// inside the lock. Versions come from one counter that only increases, so every change to a
/// workspace's listed members gets a larger version than the one before it.
/// </summary>
public sealed class PresenceTracker : IPresenceTracker
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, TrackedConnection> _connections = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, Dictionary<Guid, OnlineUser>> _workspaces = [];
    private long _version;

    public void Register(string connectionId, Guid userId, Guid sessionId)
    {
        lock (_gate)
        {
            _connections.TryAdd(connectionId, new TrackedConnection(userId, sessionId));
        }
    }

    public Guid? WorkspaceOf(string connectionId)
    {
        lock (_gate)
        {
            return _connections.TryGetValue(connectionId, out var connection) ? connection.WorkspaceId : null;
        }
    }

    public PresenceJoin? Join(string connectionId, Guid workspaceId, PresenceUserDto user)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(connectionId, out var connection) || connection.UserId != user.UserId)
            {
                return null;
            }

            var previous = connection.WorkspaceId is { } current && current != workspaceId
                ? LeaveLocked(connectionId, connection)
                : null;

            if (!_workspaces.TryGetValue(workspaceId, out var users))
            {
                users = [];
                _workspaces[workspaceId] = users;
            }

            connection.WorkspaceId = workspaceId;
            if (users.TryGetValue(user.UserId, out var online))
            {
                // Another tab, a re-join within the grace period, or a repeated join: nothing to announce.
                online.ConnectionIds.Add(connectionId);
                online.PendingOffline = null;
                return new PresenceJoin(SnapshotLocked(workspaceId, _version), null, previous);
            }

            users[user.UserId] = new OnlineUser(user) { ConnectionIds = { connectionId } };
            var snapshot = SnapshotLocked(workspaceId, ++_version);
            return new PresenceJoin(snapshot, new PresenceChange(PresenceChangeKind.Joined, user, snapshot), previous);
        }
    }

    public PresenceDeparture? Leave(string connectionId)
    {
        lock (_gate)
        {
            return _connections.TryGetValue(connectionId, out var connection) ? LeaveLocked(connectionId, connection) : null;
        }
    }

    public PresenceDeparture? Disconnect(string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.Remove(connectionId, out var connection))
            {
                return null;
            }

            return LeaveLocked(connectionId, connection);
        }
    }

    public PresenceChange? ConfirmOffline(Guid workspaceId, Guid userId, Guid pendingToken)
    {
        lock (_gate)
        {
            if (!_workspaces.TryGetValue(workspaceId, out var users)
                || !users.TryGetValue(userId, out var online)
                || online.PendingOffline != pendingToken)
            {
                return null;
            }

            return UnlistLocked(workspaceId, users, online, PresenceChangeKind.Left);
        }
    }

    public PresenceRevocation Revoke(Guid workspaceId, Guid userId)
    {
        lock (_gate)
        {
            if (!_workspaces.TryGetValue(workspaceId, out var users) || !users.TryGetValue(userId, out var online))
            {
                return new PresenceRevocation([], null);
            }

            var connectionIds = online.ConnectionIds.ToArray();
            foreach (var id in connectionIds)
            {
                if (_connections.TryGetValue(id, out var connection))
                {
                    connection.WorkspaceId = null;
                }
            }

            return new PresenceRevocation(connectionIds, UnlistLocked(workspaceId, users, online, PresenceChangeKind.Removed));
        }
    }

    public IReadOnlyList<string> ConnectionsForSession(Guid sessionId)
    {
        lock (_gate)
        {
            return _connections.Where(c => c.Value.SessionId == sessionId).Select(c => c.Key).ToArray();
        }
    }

    public IReadOnlyList<string> ConnectionsForUser(Guid userId)
    {
        lock (_gate)
        {
            return _connections.Where(c => c.Value.UserId == userId).Select(c => c.Key).ToArray();
        }
    }

    public IReadOnlyList<string> ConnectionsInWorkspace(Guid workspaceId, Guid userId)
    {
        lock (_gate)
        {
            return _workspaces.TryGetValue(workspaceId, out var users) && users.TryGetValue(userId, out var online)
                ? online.ConnectionIds.ToArray()
                : [];
        }
    }

    private PresenceDeparture? LeaveLocked(string connectionId, TrackedConnection connection)
    {
        if (connection.WorkspaceId is not { } workspaceId)
        {
            return null;
        }

        connection.WorkspaceId = null;
        if (!_workspaces.TryGetValue(workspaceId, out var users)
            || !users.TryGetValue(connection.UserId, out var online)
            || !online.ConnectionIds.Remove(connectionId))
        {
            return null;
        }

        if (online.ConnectionIds.Count > 0)
        {
            return new PresenceDeparture(workspaceId, connection.UserId, null);
        }

        // Last connection: keep the user listed until the grace period confirms they are gone.
        online.PendingOffline = Guid.NewGuid();
        return new PresenceDeparture(workspaceId, connection.UserId, online.PendingOffline);
    }

    private PresenceChange UnlistLocked(Guid workspaceId, Dictionary<Guid, OnlineUser> users, OnlineUser online, PresenceChangeKind kind)
    {
        users.Remove(online.User.UserId);
        var snapshot = SnapshotLocked(workspaceId, ++_version);
        if (users.Count == 0)
        {
            _workspaces.Remove(workspaceId);
        }

        return new PresenceChange(kind, online.User, snapshot);
    }

    private OnlineUsersDto SnapshotLocked(Guid workspaceId, long version)
    {
        var users = _workspaces.TryGetValue(workspaceId, out var listed)
            ? listed.Values
                .Select(u => u.User)
                .OrderBy(u => u.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(u => u.UserId)
                .ToArray()
            : [];
        return new OnlineUsersDto(PresenceSchema.Version, workspaceId, version, users);
    }

    private sealed class TrackedConnection(Guid userId, Guid sessionId)
    {
        public Guid UserId { get; } = userId;

        public Guid SessionId { get; } = sessionId;

        public Guid? WorkspaceId { get; set; }
    }

    private sealed class OnlineUser(PresenceUserDto user)
    {
        public PresenceUserDto User { get; } = user;

        public HashSet<string> ConnectionIds { get; } = new(StringComparer.Ordinal);

        public Guid? PendingOffline { get; set; }
    }
}
