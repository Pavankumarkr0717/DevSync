# Contract: Workspace Hub (real-time presence)

**Endpoint**: `/api/hubs/workspace` (SignalR, JSON hub protocol, camelCase). Transports:
WebSockets, then Server-Sent Events, then long polling.

**Authentication**: the 002 access token.
- `Authorization: Bearer <token>` (negotiate and long polling), or
- the `access_token` query parameter, accepted **only** on `/api/hubs/*` paths.

The session must be active at connect. The connection is closed by the server when the token
expires, or when its session is ended by logout or logout-all. Unauthenticated negotiate → `401`
with the usual Problem Details (`unauthenticated` / `token_expired` / `session_ended`).

**Limits**:
- maximum incoming message 4 KB;
- keep-alive 15 s;
- client timeout 30 s;
- one workspace per connection.

## Group naming

`workspace:{workspaceId}` where `workspaceId` is the lowercase hyphenated GUID (`D` format).
Groups are internal: clients never name or join a group directly.

## Client → server

### `JoinWorkspace(request: JoinWorkspaceRequest): JoinWorkspaceResult`

```json
// request
{ "v": 1, "workspaceId": "6f1c…-…" }
// result
{ "v": 1, "workspaceId": "6f1c…", "version": 7,
  "users": [ { "userId": "…", "displayName": "Priya Shah", "avatarUrl": null, "status": "online" } ] }
```

Behaviour:
1. If `v` is not `1` → error `invalid_request`.
2. If the id is malformed, the workspace doesn't exist, or the caller isn't a member (any role
   may join) → error `workspace_not_found`. Nothing is revealed or announced.
3. If the connection is already in another workspace → it leaves that one first (normal leave
   rules).
4. If it is already in this workspace → the current snapshot is returned and nothing is
   announced.
5. Otherwise the connection is added to the group. If this is the user's first connection
   there, the others receive `UserJoined` and `OnlineUsersUpdated`.
6. The membership is re-read after the group is added. If it was removed in the meantime, the
   join is undone → `workspace_not_found`.

### `LeaveWorkspace(): void`

Leaves the current workspace. It is idempotent. If this was the user's last connection, a "left"
is announced after the grace period (default 5 s), unless a connection of theirs rejoins first.

### Errors

Errors are raised as `HubException` with the message set to a **code only**: `invalid_request`,
`workspace_not_found` or `unavailable`. Internal exception details are never sent
(`EnableDetailedErrors = false`). The client maps the codes to messages.

## Server → client

Every payload has `v: 1`. Clients must ignore payloads whose `workspaceId` isn't the workspace
they are showing, and payloads with an older `version`.

| Event | Sent to | Payload |
|-------|---------|---------|
| `OnlineUsersUpdated` | group | `{ v, workspaceId, version, users: PresenceUser[] }`: the full list, authoritative |
| `UserJoined` | group, excluding the joining user's connections | `{ v, workspaceId, version, user: PresenceUser }` |
| `UserLeft` | group (the user's own connections are already gone) | `{ v, workspaceId, version, user: PresenceUser, reason: "left" \| "removed" }` |
| `AccessRevoked` | each connection of a removed member (then evicted from the group) | `{ v, workspaceId }` |

Ordering: for a change with version *n*, `UserJoined` or `UserLeft` and then `OnlineUsersUpdated`
are sent with the same *n*. Across concurrent changes, events may arrive out of order. The
version decides.

`PresenceUser` = `{ userId: string, displayName: string, avatarUrl: string | null, status: "online" }`.

## Lifecycle summary

| Situation | Server behaviour |
|-----------|------------------|
| Connect | Validate JWT and session; register the connection (user, session) |
| Join / switch / rejoin | As above. The re-check closes the authorize→remove race |
| Tab closed / network loss | `OnDisconnectedAsync` (immediately, or after the 30 s timeout) → leave rules |
| Last connection gone | User stays listed through the grace period, then `UserLeft` (reason `left`) |
| Member removed (HTTP DELETE member) | Evict every connection of that user from the group, `AccessRevoked` to them, `UserLeft` (reason `removed`) at once |
| Role changed | Nothing |
| Logout | Abort the connections of that session |
| Logout-all | Abort all of the user's connections |
| Token expired | Connection closed by the server; the client reconnects with a refreshed token |
| Server restart | All state lost; clients reconnect and rejoin, rebuilding presence |

## Logging (structured, no tokens)

`presence.connected`, `presence.joined`, `presence.join_refused` (code), `presence.left`
(reason), `presence.disconnected`, `presence.revoked`, `presence.session_aborted`. Each carries
`UserId` and `WorkspaceId` where known, and `ConnectionId` (logs only).
