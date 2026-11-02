namespace DevSync.Application.Presence;

/// <summary>
/// An online member as shown to other members (spec 005 FR-017): no email, role, session or connection id.
/// <see cref="AvatarUrl"/> is always null today (there are no profile images).
/// </summary>
public sealed record PresenceUserDto(Guid UserId, string DisplayName, string? AvatarUrl, string Status)
{
    public const string Online = "online";

    public static PresenceUserDto For(Guid userId, string displayName) => new(userId, displayName, null, Online);
}

/// <summary>Client → server <c>JoinWorkspace</c> argument. <see cref="WorkspaceId"/> is parsed by the server.</summary>
public sealed record JoinWorkspaceRequest(int V, string? WorkspaceId);

/// <summary>The authoritative, versioned list of a workspace's online members (also the join result).</summary>
public sealed record OnlineUsersDto(int V, Guid WorkspaceId, long Version, IReadOnlyList<PresenceUserDto> Users);

public sealed record UserJoinedDto(int V, Guid WorkspaceId, long Version, PresenceUserDto User);

/// <summary><see cref="Reason"/> is <c>left</c> or <c>removed</c>.</summary>
public sealed record UserLeftDto(int V, Guid WorkspaceId, long Version, PresenceUserDto User, string Reason);

public sealed record AccessRevokedDto(int V, Guid WorkspaceId);

/// <summary>The message schema version carried by every presence payload (constitution VI).</summary>
public static class PresenceSchema
{
    public const int Version = 1;
}

/// <summary>Codes sent as the only content of a hub error; never internal details.</summary>
public static class PresenceErrorCodes
{
    public const string InvalidRequest = "invalid_request";
    public const string WorkspaceNotFound = "workspace_not_found";
    public const string Unavailable = "unavailable";
}

public static class PresenceGroups
{
    /// <summary>The SignalR group of a workspace: <c>workspace:{id}</c> (lowercase, hyphenated).</summary>
    public static string For(Guid workspaceId) => "workspace:" + workspaceId.ToString("D");
}

public enum PresenceChangeKind
{
    Joined,
    Left,
    Removed,
}

/// <summary>A change to a workspace's listed members, with the snapshot taken at the same version.</summary>
public sealed record PresenceChange(PresenceChangeKind Kind, PresenceUserDto User, OnlineUsersDto Snapshot)
{
    public Guid WorkspaceId => Snapshot.WorkspaceId;

    public long Version => Snapshot.Version;
}

/// <summary>
/// A connection left a workspace. When <see cref="PendingOffline"/> is set it was the user's last
/// connection there: the user stays listed until the grace period confirms them offline.
/// </summary>
public sealed record PresenceDeparture(Guid WorkspaceId, Guid UserId, Guid? PendingOffline);

/// <summary>
/// The result of a join: the snapshot for the caller, the change to announce when this was the
/// user's first connection, and the departure from the workspace the connection was in before.
/// </summary>
public sealed record PresenceJoin(OnlineUsersDto Snapshot, PresenceChange? Joined, PresenceDeparture? Previous = null);

/// <summary>A member removed from a workspace: their connections there (now out of it) and the change, if they were listed.</summary>
public sealed record PresenceRevocation(IReadOnlyList<string> ConnectionIds, PresenceChange? Removed);
