using DevSync.Application.Common;
using DevSync.Application.Identity;
using DevSync.Application.Workspaces;

namespace DevSync.Application.Presence;

/// <summary>Who may join which workspace's presence, and as whom (spec 005 FR-003, FR-005, FR-006).</summary>
public sealed record PresenceCandidate(Guid WorkspaceId, PresenceUserDto User);

/// <summary>
/// The presence rules that need the database: every join is validated and authorized against the
/// membership data the HTTP API uses (<see cref="IWorkspaceAccess"/>), and the shown name comes
/// from the user directory, never from the client. Free of SignalR types (research R7).
/// </summary>
public sealed class WorkspacePresenceService(IWorkspaceAccess access, IUserDirectory users)
{
    /// <summary>
    /// <c>invalid_request</c> for a wrong schema version; <c>workspace_not_found</c> for a malformed
    /// id, a missing workspace or a non-member alike, so a join never reveals that a workspace exists.
    /// </summary>
    public async Task<Outcome<PresenceCandidate>> AuthorizeJoinAsync(
        Guid userId, JoinWorkspaceRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || request.V != PresenceSchema.Version)
        {
            return Outcome<PresenceCandidate>.Fail(PresenceErrorCodes.InvalidRequest);
        }

        if (!Guid.TryParseExact(request.WorkspaceId, "D", out var workspaceId) || workspaceId == Guid.Empty)
        {
            return Outcome<PresenceCandidate>.Fail(PresenceErrorCodes.WorkspaceNotFound);
        }

        if (!await IsMemberAsync(workspaceId, userId, cancellationToken))
        {
            return Outcome<PresenceCandidate>.Fail(PresenceErrorCodes.WorkspaceNotFound);
        }

        var user = await users.GetAsync(userId, cancellationToken);
        return user is null
            ? Outcome<PresenceCandidate>.Fail(PresenceErrorCodes.WorkspaceNotFound)
            : Outcome<PresenceCandidate>.Ok(new PresenceCandidate(workspaceId, PresenceUserDto.For(user.Id, user.DisplayName)));
    }

    /// <summary>Membership read fresh, used again after a join to close the race with a concurrent removal (research R4).</summary>
    public async Task<bool> IsMemberAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken) =>
        await access.GetRoleAsync(workspaceId, userId, cancellationToken) is not null;
}
