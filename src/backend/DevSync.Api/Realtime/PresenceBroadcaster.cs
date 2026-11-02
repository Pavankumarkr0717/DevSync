using DevSync.Application.Presence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace DevSync.Api.Realtime;

/// <summary>Bound from <c>DevSync:Presence</c>.</summary>
public sealed class PresenceOptions
{
    public const string SectionName = "DevSync:Presence";

    /// <summary>How long a user whose last connection ended stays listed before "left" is announced (FR-014).</summary>
    public double GracePeriodSeconds { get; set; } = 5;
}

/// <summary>
/// The only code that sends presence events (spec 005 research R7). It turns tracker changes into
/// hub messages, runs the offline grace period, evicts removed members and closes the connections
/// of ended sessions.
/// </summary>
public sealed class PresenceBroadcaster(
    IHubContext<WorkspaceHub, IWorkspaceClient> hub,
    IPresenceTracker tracker,
    ConnectionRegistry registry,
    TimeProvider time,
    IOptions<PresenceOptions> options,
    ILogger<PresenceBroadcaster> logger)
{
    public async Task AnnounceJoinedAsync(PresenceChange change, IReadOnlyList<string> userConnectionIds)
    {
        var group = PresenceGroups.For(change.WorkspaceId);
        await hub.Clients.GroupExcept(group, userConnectionIds)
            .UserJoined(new UserJoinedDto(PresenceSchema.Version, change.WorkspaceId, change.Version, change.User));
        await hub.Clients.Group(group).OnlineUsersUpdated(change.Snapshot);
    }

    /// <summary>
    /// Handles a connection leaving a workspace: out of the group (unless it is disconnecting, which
    /// SignalR handles) and, for the user's last connection, the grace period before "left".
    /// </summary>
    public async Task DepartAsync(string connectionId, PresenceDeparture? departure, bool removeFromGroup = true)
    {
        if (departure is null)
        {
            return;
        }

        if (removeFromGroup)
        {
            await hub.Groups.RemoveFromGroupAsync(connectionId, PresenceGroups.For(departure.WorkspaceId));
        }

        if (departure.PendingOffline is { } token)
        {
            _ = ConfirmOfflineLaterAsync(departure.WorkspaceId, departure.UserId, token);
        }
    }

    /// <summary>A member was removed (FR-020): evict every connection now, tell them, and announce at once.</summary>
    public async Task RevokeMembershipAsync(Guid workspaceId, Guid userId)
    {
        var revocation = tracker.Revoke(workspaceId, userId);
        var group = PresenceGroups.For(workspaceId);
        foreach (var connectionId in revocation.ConnectionIds)
        {
            await hub.Groups.RemoveFromGroupAsync(connectionId, group);
        }

        if (revocation.ConnectionIds.Count > 0)
        {
            await hub.Clients.Clients(revocation.ConnectionIds).AccessRevoked(new AccessRevokedDto(PresenceSchema.Version, workspaceId));
        }

        logger.LogInformation(
            "Presence event {Event} in workspace {WorkspaceId}: user {UserId}, {ConnectionCount} connection(s) evicted",
            "presence.revoked", workspaceId, userId, revocation.ConnectionIds.Count);

        if (revocation.Removed is { } change)
        {
            await AnnounceLeftAsync(change);
        }
    }

    /// <summary>Closes the hub connections opened with an ended session (logout, FR-004).</summary>
    public void EndSession(Guid sessionId) => Abort(tracker.ConnectionsForSession(sessionId), "session", sessionId);

    /// <summary>Closes every hub connection of the user (logout everywhere, FR-004).</summary>
    public void EndAllSessions(Guid userId) => Abort(tracker.ConnectionsForUser(userId), "user", userId);

    private void Abort(IReadOnlyList<string> connectionIds, string scope, Guid id)
    {
        foreach (var connectionId in connectionIds)
        {
            registry.Abort(connectionId);
        }

        logger.LogInformation(
            "Presence event {Event} for {Scope} {Id}: {ConnectionCount} connection(s) closed",
            "presence.session_aborted", scope, id, connectionIds.Count);
    }

    private async Task ConfirmOfflineLaterAsync(Guid workspaceId, Guid userId, Guid token)
    {
        try
        {
            var grace = TimeSpan.FromSeconds(Math.Max(0, options.Value.GracePeriodSeconds));
            if (grace > TimeSpan.Zero)
            {
                await Task.Delay(grace, time);
            }

            if (tracker.ConfirmOffline(workspaceId, userId, token) is { } change)
            {
                await AnnounceLeftAsync(change);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Presence grace check failed in workspace {WorkspaceId} for user {UserId}", workspaceId, userId);
        }
    }

    private async Task AnnounceLeftAsync(PresenceChange change)
    {
        var reason = change.Kind == PresenceChangeKind.Removed ? "removed" : "left";
        var group = PresenceGroups.For(change.WorkspaceId);
        logger.LogInformation(
            "Presence event {Event} in workspace {WorkspaceId}: user {UserId} ({Reason})",
            "presence.left", change.WorkspaceId, change.User.UserId, reason);
        await hub.Clients.Group(group)
            .UserLeft(new UserLeftDto(PresenceSchema.Version, change.WorkspaceId, change.Version, change.User, reason));
        await hub.Clients.Group(group).OnlineUsersUpdated(change.Snapshot);
    }
}
