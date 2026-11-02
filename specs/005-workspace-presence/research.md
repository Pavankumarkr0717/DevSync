# Research: Real-Time Workspace Presence

All Technical Context items were resolved; none remain NEEDS CLARIFICATION.

## R1. Real-time transport

- **Decision**: ASP.NET Core SignalR (in the shared framework), with one strongly typed hub at
  `/api/hubs/workspace`. The browser uses `@microsoft/signalr` 10.0.11 with the JSON protocol, and
  WebSockets with fallback to SSE and long polling.
- **Rationale**: The brief and the constitution (XI) call for maintained libraries. SignalR
  provides groups, keep-alives, transport fallback and a strongly typed `Hub<T>` on the server we
  already run, with no extra infrastructure. Putting the hub under `/api/` reuses the
  same-origin nginx and Vite proxies, so no CORS and no new cookie scope.
- **Alternatives**:
  - Raw WebSockets: we would hand-build framing, keep-alive, fallback and groups.
  - Server-Sent Events plus POST: one-way only, with joins as separate HTTP calls and no
    connection lifecycle hooks.
  - MessagePack protocol: smaller frames, but another dependency for tiny payloads.

## R2. Authenticating the connection

- **Decision**:
  - Reuse the existing JWT bearer scheme.
  - In `JwtBearerEvents.OnMessageReceived`, read `access_token` from the query string only when
    the path starts with `/api/hubs/`. Everywhere else the header is the only source.
  - The existing `OnTokenValidated` session check runs at connect, so a revoked session cannot
    connect.
  - The hub is `[Authorize]`, and the fallback policy applies anyway.
  - The client's `accessTokenFactory` returns the in-memory token, first refreshing it through
    the shared `refreshSession` when it has 30 seconds or less to live.
- **Rationale**: Browsers can't set an `Authorization` header on WebSocket or EventSource
  requests, and the query string is SignalR's documented mechanism. Keeping it to the hub path
  stops the query-string token from becoming a general way in. The request logger records
  route patterns, not URLs, and `Microsoft.AspNetCore` hosting logs stay at Warning, so the
  token is never logged. This is covered by a test that scans the captured logs.
- **Alternatives**:
  - Cookie auth for the hub: the refresh cookie is path-scoped to `/api/auth` and is
    deliberately not an access credential (002 R7). Adding an access cookie would bring CSRF
    surface.
  - A one-time "ticket" endpoint: more moving parts for the same exposure (the token lives
    15 minutes, is session-bound and is revocable).

## R3. Expiry and session revocation for live connections

- **Decision**:
  - **Token expiry**: map the hub with `CloseOnAuthenticationExpiration = true`. The server
    closes the connection when the token it was opened with expires. The client's reconnect
    loop (R10) then reconnects with a fresh token, or sends the person to sign in if the session
    has ended.
  - **Session end**: `POST /api/auth/logout` and `/logout-all` call
    `PresenceBroadcaster.EndSessionsAsync(...)`. This aborts (`HubCallerContext.Abort()`) every
    registered connection whose session id (the `sid` claim captured at connect) has ended, or
    every connection of the user for logout-all.
  - Revocations made in other ways (for example the session reuse detector in 002) take effect
    at the next token expiry, at most 15 minutes. This is documented.
- **Rationale**: This meets FR-004 without a per-message database check. Abort is
  server-enforced, so a modified client cannot ignore it.
- **Alternatives**:
  - Re-checking the session on every hub call: there is only one call (join), and it would not
    stop event delivery.
  - Polling sessions in a background sweep: more load for no stronger guarantee than the above.

## R4. Workspace authorization on join and removal

- **Decision**:
  - `JoinWorkspace` parses the id as a GUID, then calls `IWorkspaceAccess.GetRoleAsync`, the
    exact source behind `RequireWorkspace`, through a scoped service resolved per hub call.
  - A null role, a malformed id or a missing workspace all produce one `HubException` with the
    code `workspace_not_found`, matching the HTTP API's 404-for-non-members rule.
  - All roles may join presence, because the `Read` permission belongs to every role.
  - On `DELETE /api/workspaces/{id}/members/{userId}` success, the endpoint calls
    `RevokeMembershipAsync`. This removes all of that user's connections in the workspace from
    the group (`Groups.RemoveFromGroupAsync`) and from the tracker, sends them `AccessRevoked`,
    and announces `UserLeft` at once, with reason `removed` and no grace period.
- **Rationale**: Constitution IV says group membership is not authorization. A removed user's
  connection is actively evicted, rather than left in the group until it reconnects.
- **Alternatives**: Checking the role on every outgoing event, which is impossible with group
  sends and costly per event.
- **Race**: a join that authorized just before a removal committed could add the connection after
  the eviction. Mitigation: after `AddToGroupAsync`, the service re-reads the role once. If it is
  now null, it undoes the join and returns `workspace_not_found`. This is tested.

## R5. Tracking model and concurrency

- **Decision**: One `PresenceTracker` singleton guarded by a single `lock`. It holds:
  - `connections`: connection id → `{ userId, sessionId, workspaceId? , user }`;
  - `workspaces`: workspace id → `{ version, users: userId → { user, connectionIds:set, pendingOfflineToken? } }`.

  Operations return transition records:
  - `Join` returns `Joined(first: bool)` or `AlreadyJoined`;
  - `Leave` and `Disconnect` return `LastConnectionGone(token)`, `StillOnline` or `NotJoined`;
  - `ConfirmOffline(token)` returns `Left` or `Cancelled`;
  - `Revoke(user)` returns `Left` or `NotOnline`.

  Versions increment inside the lock on every change to the online set.
- **Rationale**: These are small critical sections over small maps. A single lock is easy to
  prove correct, and the state is never shared outside it, because snapshots are copied. A
  `ConcurrentDictionary` of `ConcurrentDictionary`s cannot update the user and connection sets
  atomically, so a "last connection" decision could race.
- **Idempotency**:
  - joining twice on the same connection returns `AlreadyJoined`;
  - leaving or disconnecting an unknown connection returns `NotJoined`;
  - a duplicate disconnect is a no-op.
- **Grace**: when the last connection goes, the user stays listed with a fresh pending token. A
  re-join within the grace period clears the token, so nothing is announced. `ConfirmOffline`
  with a stale token is a no-op.

## R6. In-memory limits and the distributed path

- **Decision**: Accept a single-instance, in-memory design (NFR-005), and document these limits:
  - presence is lost on restart (clients rejoin);
  - several API instances would each see only their own connections;
  - group sends reach only the local instance.

  The path forward:
  1. Add the SignalR Redis backplane, or Azure SignalR Service, for cross-instance group sends.
  2. Implement `IPresenceTracker` on a shared store, with the same transition semantics using
     atomic scripts, per-instance connection leases and TTL heartbeats so a crashed instance's
     connections expire.
  3. Replace in-process grace timers with expiring keys plus a single-consumer sweeper.

  The hub, service, contracts and frontend are unchanged by that move.
- **Alternatives**: Persisting presence in PostgreSQL, which the brief forbids without a concrete
  need. It would also add write load and stale rows after crashes.

## R7. Where logic lives

- **Decision**:
  - The hub reads `sub` and `sid` from claims, resolves `WorkspacePresenceService`, maps outcomes
    to `HubException` codes, and calls `PresenceBroadcaster`.
  - The service has no SignalR types; it returns `PresenceChange` records.
  - The broadcaster is the only code that talks to `IHubContext`.
  - Grace timers use `TimeProvider` (`Task.Delay(grace, timeProvider)`), so tests control time.
- **Rationale**: This follows the brief ("do not place all presence logic inside the Hub") and
  constitution II. It also allows unit tests without SignalR.

## R8. Event contracts, ordering and de-duplication

- **Decision**:
  - Every payload carries `v: 1`. Server-to-client payloads also carry `workspaceId` and
    `version`.
  - `OnlineUsersUpdated` is the authoritative list. The client replaces its list only when the
    `workspaceId` matches the current one and `version` is greater than the current one.
  - `UserJoined` and `UserLeft` exist for notifications. The client shows one only if the
    `workspaceId` matches, `version` is greater than the version of its join result, and the
    user isn't itself. Each version is notified at most once.
  - The join result carries the snapshot and resets the baseline, which also covers a server
    restart, where versions start over.
  - Payloads are validated in the client before they reach Redux, and invalid ones are dropped
    and logged to the console.
  - The server validates the request shape: SignalR's binder plus an explicit `v == 1` check and
    a GUID parse.
- **Rationale**: Sends to a group from concurrent hub calls can interleave. Versions make the
  outcome deterministic (constitution VI).
- **Alternatives**: Incremental list maintenance from joined and left events only, which drifts
  after any lost or reordered message.

## R9. Frontend: where the connection lives

- **Decision**:
  - A `WorkspaceScope` layout route (`workspaces/:workspaceId`) wraps the overview, files and
    diagram routes. Paths are unchanged.
  - On mount and on `workspaceId` change it calls `presenceClient.join(id)`. On unmount it calls
    `presenceClient.release()`.
  - `release()` leaves the current workspace and stops the connection. A re-acquire within the
    same tick (React StrictMode or a fast remount) cancels the stop, so there is no
    connect/disconnect churn.
  - The client is one instance per store, created lazily by a hook, so there is one connection
    per tab.
- **Rationale**: Moving between Overview, Files and Diagrams of one workspace keeps one join, with
  no leave and join noise. Switching workspaces swaps the group on the same connection, and
  leaving the workspace area closes it (FR-022, FR-023).

## R10. Reconnection

- **Decision**: The client runs its own reconnect loop from `onclose` instead of
  `withAutomaticReconnect`. It uses delays of 0, 2, 5, 10 and 30 seconds, then gives up with
  status `failed` and a manual Retry. During the loop the status is `reconnecting` and the list is
  marked stale. On success the client rejoins the current workspace (if any) and replaces the
  list. Before each attempt the token is refreshed if it is expiring. If `refreshSession` reports
  `ended`, the loop stops; `sessionEnded` is already dispatched, so the app shows sign-in.
- **Rationale**: The server closes the connection at token expiry (R3), and SignalR's built-in
  automatic reconnect does not apply to every server-initiated close. A single loop handles
  network loss, server restart and token expiry the same way, and is easy to test with fake
  timers.
- **Alternatives**: `withAutomaticReconnect` plus an extra `onclose` restart. This gives two
  overlapping retry loops and double the attempts.

## R11. Notifications and status UI

- **Decision**:
  - Reuse the existing single `Toast` (snackbar) for "<Name> joined the workspace." and "<Name>
    left the workspace.".
  - The connection state is a small inline status in the presence bar:
    - "Reconnecting to DevSync…" with a spinner;
    - "Connection restored." for 4 seconds;
    - "Live presence unavailable" with Retry.

    It uses `role="status"` and `aria-live="polite"`.
  - The popover uses a button with `aria-expanded`, a list of names, and closes with Escape and
    on outside click.
- **Rationale**: FR-033 rules out a new design system. The toast is already the product's
  notification pattern.

## R12. Testing SignalR

- **Decision**:
  - **Integration**: `HubConnectionBuilder().WithUrl(server.BaseAddress + "api/hubs/workspace", o => { o.HttpMessageHandlerFactory = _ => server.CreateHandler(); o.Transports = LongPolling; o.AccessTokenProvider = … })`.
    The grace period is set to 200 ms through configuration. Events go to channels that
    `WaitAsync` with timeouts. Token expiry is tested by advancing the existing fake time
    provider and observing the close.
  - **Frontend**: inject a `FakeHubConnection`, an in-memory object with `on`, `off`, `invoke`,
    `start`, `stop` and `onclose`, plus `emit(event, payload)` and `drop(error)` helpers. This
    goes through `setPresenceTransport()` in test setup. Fake timers cover reconnect delays.
- **Rationale**: These tests are deterministic and need no browser WebSocket in jsdom.
