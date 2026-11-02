namespace DevSync.Application.Presence;

/// <summary>
/// Who is online in which workspace, by connection (spec 005 data-model). The in-process
/// <see cref="PresenceTracker"/> is the only implementation; the interface is the seam for a
/// distributed store (docs/architecture/realtime-presence.md, "Distributed path").
/// Every method is safe to call concurrently and idempotent where the contract says so.
/// </summary>
public interface IPresenceTracker
{
    /// <summary>Records a new connection of an authenticated user and session.</summary>
    void Register(string connectionId, Guid userId, Guid sessionId);

    /// <summary>The workspace the connection is in, if any.</summary>
    Guid? WorkspaceOf(string connectionId);

    /// <summary>
    /// Puts the connection in the workspace, leaving the one it was in (see <see cref="PresenceJoin.Previous"/>).
    /// Null if the connection is not registered (it has already disconnected).
    /// </summary>
    PresenceJoin? Join(string connectionId, Guid workspaceId, PresenceUserDto user);

    /// <summary>Takes the connection out of its workspace; it stays registered. Null if it was in none.</summary>
    PresenceDeparture? Leave(string connectionId);

    /// <summary>Forgets the connection, leaving its workspace first. Null if it was in none or is unknown.</summary>
    PresenceDeparture? Disconnect(string connectionId);

    /// <summary>After the grace period: the "left" change, or null if the user came back or was already removed.</summary>
    PresenceChange? ConfirmOffline(Guid workspaceId, Guid userId, Guid pendingToken);

    /// <summary>Takes every connection of the user out of the workspace and unlists them at once.</summary>
    PresenceRevocation Revoke(Guid workspaceId, Guid userId);

    IReadOnlyList<string> ConnectionsForSession(Guid sessionId);

    IReadOnlyList<string> ConnectionsForUser(Guid userId);

    /// <summary>The user's connections that are in the workspace.</summary>
    IReadOnlyList<string> ConnectionsInWorkspace(Guid workspaceId, Guid userId);
}
