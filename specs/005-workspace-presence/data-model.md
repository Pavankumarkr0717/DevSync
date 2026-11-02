# Data Model: Real-Time Workspace Presence

No database tables or migrations. Everything below is **in-memory state on the single API
instance** (FR-018, FR-019) or **messages** on the hub. Durable inputs come from 002:
`workspace_memberships` (authorization), `sessions` (sign-in validity) and users (display names).

## In-memory state (`PresenceTracker`)

### TrackedConnection

| Field | Type | Rules |
|-------|------|-------|
| `ConnectionId` | string | Server-assigned SignalR id. Key. Never sent to clients. |
| `UserId` | Guid | From the `sub` claim at connect. Immutable. |
| `SessionId` | Guid | From the `sid` claim at connect. Used for session-end aborts. |
| `WorkspaceId` | Guid? | At most one workspace at a time (FR-007). Null until a join succeeds. |

### WorkspacePresence (per workspace with ≥ 1 listed user)

| Field | Type | Rules |
|-------|------|-------|
| `WorkspaceId` | Guid | Key. Group name `workspace:{WorkspaceId:D}` (lowercase GUID with hyphens). |
| `Version` | long | Taken from one tracker-wide counter that only increases while the process runs, so every change to the listed users of any workspace gets a larger number. A restart starts over, and clients reset their baseline on every join result. |
| `Users` | map userId → OnlineUser | Each user appears once (FR-012). |

### OnlineUser

| Field | Type | Rules |
|-------|------|-------|
| `User` | PresenceUserDto | Display name captured at the user's first join. |
| `ConnectionIds` | set<string> | Connections of this user in this workspace. |
| `PendingOffline` | Guid? | Set when `ConnectionIds` becomes empty. Cleared by a re-join. While set, the user is still listed. |

## Transitions

```text
                Join (first connection)             Join (another connection)
   (absent) ───────────────────────────► ONLINE ◄──────────────────────────┐
                 version++ · UserJoined     │  │                           │
                                            │  └─ Leave/Disconnect (not last) ─┘  no event
          Leave/Disconnect (last connection)│
                                            ▼
                                  PENDING_OFFLINE (still listed, token T)
                  re-join within grace ─────┘   │ ConfirmOffline(T) after grace
                  (token cleared, no event)     ▼
                                            (absent)  version++ · UserLeft(reason: "left")

   Revoke (member removed)  ONLINE/PENDING_OFFLINE ──► (absent)  version++ · UserLeft(reason: "removed")
                            + AccessRevoked to each of the user's connections, evicted from the group
```

Rules:

- A connection's `WorkspaceId` changes only through Join (switching first performs the Leave
  steps on the previous workspace), Leave or Disconnect.
- Disconnect of an unknown connection, Leave without a workspace, and ConfirmOffline with a stale
  token are no-ops (FR-009, FR-016).
- All transitions run inside one lock. Snapshots (`OnlineUsersDto`) are copied inside the lock
  and sent outside it.
- Session abort: the registry lists connections by `SessionId`/`UserId`. Aborting them causes
  ordinary Disconnect transitions.

## Messages

Field-level schemas are in [contracts/workspace-hub.md](./contracts/workspace-hub.md).

| Message | Direction | Purpose |
|---------|-----------|---------|
| `JoinWorkspaceRequest` `{ v, workspaceId }` | client → server | Join one workspace |
| `JoinWorkspaceResult` `{ v, workspaceId, version, users[] }` | server → caller | Snapshot that resets the client's baseline |
| `LeaveWorkspace()` | client → server | Leave the current workspace (idempotent) |
| `OnlineUsersUpdated` `{ v, workspaceId, version, users[] }` | server → group | Authoritative list after a change |
| `UserJoined` `{ v, workspaceId, version, user }` | server → group except the joiner's connections | Notification |
| `UserLeft` `{ v, workspaceId, version, user, reason }` | server → group | Notification |
| `AccessRevoked` `{ v, workspaceId }` | server → removed user's connections | Stop showing presence; "no longer have access" |

### PresenceUserDto

| Field | Type | Rules |
|-------|------|-------|
| `userId` | string (GUID) | — |
| `displayName` | string | From the user directory, never from the client |
| `avatarUrl` | string \| null | Always null today (no profile images) |
| `status` | `"online"` | Only online users are listed |

Excluded on purpose (FR-017): email, role, session id, connection id, tokens.

## Frontend state (`presence` slice)

| Field | Type | Rules |
|-------|------|-------|
| `status` | `idle` \| `connecting` \| `connected` \| `reconnecting` \| `failed` | Driven by `presenceClient` |
| `workspaceId` | string \| null | The workspace currently joined or requested |
| `version` | number | Version of the snapshot shown; snapshots with `version <= this` or another `workspaceId` are ignored |
| `baseline` | number | Version from the last join result; notifications with `version <= baseline` are ignored |
| `users` | PresenceUser[] | Sorted: self first, then by display name |
| `stale` | boolean | True while not `connected` |
| `revoked` | boolean | True after `AccessRevoked` for the current workspace |
| `restoredAt` | number \| null | Drives the one-off "Connection restored." |
