using DevSync.Application.Presence;

namespace DevSync.Api.Realtime;

/// <summary>Server → client events of the workspace hub (specs/005-workspace-presence/contracts/workspace-hub.md).</summary>
public interface IWorkspaceClient
{
    /// <summary>The authoritative list of online members after a change.</summary>
    Task OnlineUsersUpdated(OnlineUsersDto snapshot);

    /// <summary>A member came online; for notifications only.</summary>
    Task UserJoined(UserJoinedDto joined);

    /// <summary>A member went offline or was removed; for notifications only.</summary>
    Task UserLeft(UserLeftDto left);

    /// <summary>Sent to a removed member's connections before they are taken out of the group.</summary>
    Task AccessRevoked(AccessRevokedDto revoked);
}
