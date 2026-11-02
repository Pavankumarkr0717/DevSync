# ADR 0005: Real-time workspace presence

**Status**: Accepted | **Date**: 2026-11-01 | **Spec**: [005 Real-Time Workspace Presence](../../specs/005-workspace-presence/spec.md)

## Context

Feature 005 adds DevSync's first real-time channel: members see who else is in a workspace,
who joins and who leaves.

**Requirements**:
- Every workspace-scoped real-time operation must be authorized on the server (constitution IV).
- Presence must be correct across tabs, devices, reloads and network loss.
- Live access must not outlive a revoked membership or session.
- No Redis, broker or backplane may be added (constitution IX), and transient presence must not
  be persisted.

The alternatives are in [research.md](../../specs/005-workspace-presence/research.md) (R1–R12).

## Decision

1. **ASP.NET Core SignalR**, part of the shared framework, with one strongly typed hub
   (`WorkspaceHub : Hub<IWorkspaceClient>`) at `/api/hubs/workspace`. The browser uses
   `@microsoft/signalr` 10.0.11. The hub sits under `/api`, so the same-origin proxies carry it
   (WebSocket upgrade added to nginx and Vite).
2. **Same access token as the HTTP API.** It is accepted from the `access_token` query string
   **only** on `/api/hubs/*`, because browsers can't set headers on WebSockets. The existing
   session check applies at connect.
   - Connections close at token expiry: `ConnectionRegistry` uses the app's `TimeProvider`, with
     `CloseOnAuthenticationExpiration` as a backstop.
   - On logout and logout-all, the server aborts the connections of the ended sessions.
3. **Membership checked on every join** through `IWorkspaceAccess`, the HTTP API's own source,
   and re-read after the group add to close the race with a concurrent removal. Removing a
   member actively evicts their connections and sends `AccessRevoked`. Group membership is never
   treated as authorization.
4. **An in-memory `PresenceTracker`**, behind `IPresenceTracker`, under a single lock. A user is
   online while at least one connection has joined. The last connection leaving starts a
   5-second grace period before "left", so reloads are silent.
5. **Versioned snapshots.** Every change gets a version from one increasing counter.
   `OnlineUsersUpdated` carries the authoritative list, and `UserJoined`/`UserLeft` drive
   notifications only. Clients apply newer versions only, and reset their baseline on every join
   result. Every payload carries `v: 1` and is validated on receipt.
6. **Thin hub.** Rules live in `WorkspacePresenceService` and the tracker, which have no SignalR
   types. `PresenceBroadcaster` is the only sender.
7. **Client-owned reconnect loop**: retries after 0, 2, 5, 10 and 30 s, then "unavailable" with
   Retry. It refreshes the token before each attempt and rejoins with a fresh snapshot. One
   connection per tab, scoped to the workspace routes by a layout route.

## Consequences

- No new infrastructure and no schema change. Presence works the moment the API starts.
- **Single instance only.** A restart loses presence until clients rejoin, and a second API
  instance would split it. The [distributed path](../architecture/realtime-presence.md#path-to-distributed-presence)
  (backplane, shared tracker, expiring keys) replaces only the tracker and adds a backplane.
- Session revocations other than logout and logout-all reach live connections within one
  access-token lifetime (15 minutes).
- The query-string token is a known SignalR trade-off. It is limited to the hub path, short-lived,
  session-bound and never logged.
- The `@microsoft/signalr` client adds about 50 kB (minified) to the entry chunk.
