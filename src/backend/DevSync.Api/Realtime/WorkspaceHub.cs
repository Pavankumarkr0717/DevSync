using DevSync.Api.Auth;
using DevSync.Application.Presence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DevSync.Api.Realtime;

/// <summary>
/// The real-time workspace hub (spec 005). Thin transport: identity comes only from the
/// authenticated connection's claims, rules live in <see cref="WorkspacePresenceService"/> and
/// <see cref="IPresenceTracker"/>, and messages go out through <see cref="PresenceBroadcaster"/>.
/// Errors reach the client as a code only (contracts/workspace-hub.md).
/// </summary>
[Authorize]
public sealed class WorkspaceHub(
    WorkspacePresenceService presence,
    IPresenceTracker tracker,
    PresenceBroadcaster broadcaster,
    ConnectionRegistry registry,
    ILogger<WorkspaceHub> logger) : Hub<IWorkspaceClient>
{
    public override Task OnConnectedAsync()
    {
        if (!Guid.TryParse(Context.User?.FindFirst(AccessTokenIssuer.UserIdClaim)?.Value, out var userId)
            || !Guid.TryParse(Context.User?.FindFirst(AccessTokenIssuer.SessionIdClaim)?.Value, out var sessionId)
            || !long.TryParse(Context.User?.FindFirst("exp")?.Value, out var expires))
        {
            Context.Abort();
            return Task.CompletedTask;
        }

        // The connection lives no longer than the access token it was opened with (FR-004).
        registry.Add(Context, DateTimeOffset.FromUnixTimeSeconds(expires));
        tracker.Register(Context.ConnectionId, userId, sessionId);
        logger.LogInformation(
            "Presence event {Event}: user {UserId} on connection {ConnectionId}", "presence.connected", userId, Context.ConnectionId);
        return Task.CompletedTask;
    }

    /// <summary>Joins one workspace's presence (leaving any other) and returns its online members.</summary>
    public async Task<OnlineUsersDto> JoinWorkspace(JoinWorkspaceRequest request)
    {
        var userId = UserId();
        var connectionId = Context.ConnectionId;
        try
        {
            var authorized = await presence.AuthorizeJoinAsync(userId, request, Context.ConnectionAborted);
            if (!authorized.IsSuccess)
            {
                throw Refuse(authorized.Error!, userId, request?.WorkspaceId);
            }

            var workspaceId = authorized.Value.WorkspaceId;

            // Into the group before taking the snapshot, so no change between the two is missed.
            await Groups.AddToGroupAsync(connectionId, PresenceGroups.For(workspaceId));
            var join = tracker.Join(connectionId, workspaceId, authorized.Value.User)
                ?? throw new HubException(PresenceErrorCodes.Unavailable);
            await broadcaster.DepartAsync(connectionId, join.Previous);

            // A removal that committed after the check above must not leave this connection in the group.
            if (!await presence.IsMemberAsync(workspaceId, userId, Context.ConnectionAborted))
            {
                await broadcaster.RevokeMembershipAsync(workspaceId, userId);
                throw Refuse(PresenceErrorCodes.WorkspaceNotFound, userId, request.WorkspaceId);
            }

            if (join.Joined is { } change)
            {
                await broadcaster.AnnounceJoinedAsync(change, tracker.ConnectionsInWorkspace(workspaceId, userId));
            }

            logger.LogInformation(
                "Presence event {Event} in workspace {WorkspaceId}: user {UserId} on connection {ConnectionId} (first: {First})",
                "presence.joined", workspaceId, userId, connectionId, join.Joined is not null);
            return join.Snapshot;
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Presence join failed for user {UserId} on connection {ConnectionId}", userId, connectionId);
            throw new HubException(PresenceErrorCodes.Unavailable);
        }
    }

    /// <summary>Leaves the current workspace. Idempotent.</summary>
    public async Task LeaveWorkspace()
    {
        var departure = tracker.Leave(Context.ConnectionId);
        await broadcaster.DepartAsync(Context.ConnectionId, departure);
        if (departure is not null)
        {
            logger.LogInformation(
                "Presence event {Event} in workspace {WorkspaceId}: user {UserId} on connection {ConnectionId}",
                "presence.left_workspace", departure.WorkspaceId, departure.UserId, Context.ConnectionId);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        registry.Remove(Context.ConnectionId);
        var departure = tracker.Disconnect(Context.ConnectionId);
        await broadcaster.DepartAsync(Context.ConnectionId, departure, removeFromGroup: false);
        logger.LogInformation(
            "Presence event {Event}: connection {ConnectionId} (workspace {WorkspaceId}, unexpected: {Unexpected})",
            "presence.disconnected", Context.ConnectionId, departure?.WorkspaceId, exception is not null);
    }

    private Guid UserId() =>
        Guid.TryParse(Context.User?.FindFirst(AccessTokenIssuer.UserIdClaim)?.Value, out var id)
            ? id
            : throw new HubException(PresenceErrorCodes.InvalidRequest);

    private HubException Refuse(string code, Guid userId, string? workspaceId)
    {
        // The raw id is client input: log its length only when it is not a well-formed GUID.
        var loggedId = Guid.TryParse(workspaceId, out var parsed) ? parsed.ToString() : $"(malformed, {workspaceId?.Length ?? 0} chars)";
        logger.LogInformation(
            "Presence event {Event} for user {UserId}: {Code} (workspace {WorkspaceId})",
            "presence.join_refused", userId, code, loggedId);
        return new HubException(code);
    }
}
