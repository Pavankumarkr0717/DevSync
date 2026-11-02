---

description: "Task list for feature 005-workspace-presence"
---

# Tasks: Real-Time Workspace Presence

**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Required. The brief §8 asks for backend unit, backend integration, frontend and
regression tests, and constitution III and V apply. In each story, write the tests first.

**Format**: `[ID] [P?] [Story] Description`. Every task ends with **Done when:** (its completion
condition), the requirement ids, and **Deps:**.

## Scope guard

Do not add:
- collaborative editing, Monaco or diagram sync, CRDT/OT, Yjs;
- cursors or selections, "who is viewing which file";
- chat;
- Redis, RabbitMQ or a backplane, presence persistence, extra hub methods.

No S01–S04 API contract changes.

---

## Phase 1: Setup

- [X] T001 Add `"@microsoft/signalr": "10.0.11"` (exact) to `src/frontend/package.json` and install. **Done when:** typecheck and build pass. (plan; R1) **Deps:** —
- [X] T002 [P] Add `<PackageVersion Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.14" />` to `Directory.Packages.props` and reference it from `tests/backend/DevSync.IntegrationTests/DevSync.IntegrationTests.csproj`. **Done when:** `dotnet build` passes. (R12) **Deps:** —
- [X] T003 [P] Proxy WebSockets for `/api/`: add `proxy_http_version 1.1`, `Upgrade` and `Connection` headers (with a `map $http_upgrade $connection_upgrade`) and `proxy_read_timeout 120s` in `src/frontend/nginx.conf`; set `'/api': { target: apiTarget, ws: true }` in `src/frontend/vite.config.ts`. **Done when:** the web image builds and `/api` HTTP calls still work. (FR-001) **Deps:** —

## Phase 2: Foundational (blocking)

- [X] T004 Create `src/backend/DevSync.Application/Presence/PresenceContracts.cs`. It holds these records:
  - `PresenceUserDto(Guid UserId, string DisplayName, string? AvatarUrl, string Status)`, where `Status` is always `"online"` and `AvatarUrl` is always null;
  - `JoinWorkspaceRequest(int V, string? WorkspaceId)`;
  - `OnlineUsersDto(int V, Guid WorkspaceId, long Version, IReadOnlyList<PresenceUserDto> Users)`, also used as the join result;
  - `UserJoinedDto(int V, Guid WorkspaceId, long Version, PresenceUserDto User)`;
  - `UserLeftDto(int V, Guid WorkspaceId, long Version, PresenceUserDto User, string Reason)`, where `Reason` is `"left"` or `"removed"`;
  - `AccessRevokedDto(int V, Guid WorkspaceId)`;
  - `PresenceErrorCodes` (`invalid_request`, `workspace_not_found`, `unavailable`);
  - `PresenceGroups.For(Guid)`, which returns `"workspace:" + id.ToString("D")`.

  **Done when:** it compiles, and no DTO has an email, role, session or connection field. (FR-017, contracts) **Deps:** —
- [X] T005 Create `IPresenceTracker` and `PresenceTracker` in `src/backend/DevSync.Application/Presence/`, following data-model.md:
  - one `lock`;
  - `Register(connId, userId, sessionId)`;
  - `Join(connId, workspaceId, PresenceUserDto)`, which returns `JoinOutcome { FirstConnection, AlreadyJoined, PreviousWorkspaceLeave?, Snapshot }`;
  - `Leave(connId)` and `Disconnect(connId)`, which return `LeaveOutcome { None | StillOnline | PendingOffline(workspaceId, userId, token) }`;
  - `ConfirmOffline(workspaceId, userId, token)`, which returns a `Left` change or null;
  - `Revoke(workspaceId, userId)`, which returns the connection ids, plus a change or null;
  - `ConnectionsForSession(sessionId)`, `ConnectionsForUser(userId)` and `Snapshot(workspaceId)`.

  Versions increment on every change to the listed users. Snapshots are copied inside the lock and sorted by display name. **Done when:** it compiles. (FR-007–FR-016, R5) **Deps:** T004
- [X] T006 Create `PresenceOptions` (`GracePeriod`, default 5 s, bound from `DevSync:Presence:GracePeriodSeconds`) and `WorkspacePresenceService` in `src/backend/DevSync.Application/Presence/`. It has these methods:
  - `JoinAsync(connId, userId, rawWorkspaceId, v)`. This checks that `v == 1` (else `invalid_request`), parses the GUID, calls `IWorkspaceAccess.GetRoleAsync` (null → `workspace_not_found`), and looks up the name through `IUserDirectory.GetAsync`. It then calls `tracker.Join` and returns `Outcome<JoinResult>`.
  - `RecheckAsync(connId, userId, workspaceId)`, the post-group-add race check (R4). It undoes the join and reports not found when the role is gone.
  - `Leave`, `Disconnect`, `ConfirmOffline` and `Revoke`, which delegate to the tracker.

  Register it with DI in `src/backend/DevSync.Application/DependencyInjection.cs`: the tracker as a singleton and the service as scoped. **Done when:** it compiles. (FR-005, FR-006, FR-010) **Deps:** T005
- [X] T007 Create the API realtime adapter in `src/backend/DevSync.Api/Realtime/`:
  - `IWorkspaceClient`, with `OnlineUsersUpdated`, `UserJoined`, `UserLeft` and `AccessRevoked`;
  - `ConnectionRegistry`, a singleton mapping a connection id to its `HubCallerContext` so a connection can be aborted;
  - `PresenceBroadcaster`, a singleton that uses `IHubContext<WorkspaceHub, IWorkspaceClient>` and `TimeProvider`. It handles group add and remove, joined and left sends, grace scheduling via `Task.Delay(grace, time)` then `ConfirmOffline`, `RevokeMembershipAsync` and `EndSessionsAsync`;
  - `WorkspaceHub : Hub<IWorkspaceClient>`, marked `[Authorize]`. `OnConnectedAsync` registers the connection from the `sub` and `sid` claims. `JoinWorkspace(JoinWorkspaceRequest)` and `LeaveWorkspace()` are thin calls into the service and broadcaster, and `OnDisconnectedAsync` handles cleanup;
  - `RealtimeSetup.AddDevSyncRealtime()`, which configures SignalR with camelCase JSON, `EnableDetailedErrors=false`, `MaximumReceiveMessageSize=4096`, `KeepAliveInterval=15s` and `ClientTimeoutInterval=30s`;
  - `MapDevSyncHubs()`, which maps `/api/hubs/workspace` with `CloseOnAuthenticationExpiration=true`.

  Wire both into `Program.cs`. Errors become `HubException(code)` only. Log structured `presence.*` events. **Done when:** the API starts and existing tests pass. (FR-001, FR-034, R7) **Deps:** T006
- [X] T008 In `src/backend/DevSync.Api/Auth/AuthSetup.cs`, add `OnMessageReceived`. It takes `access_token` from the query string only when `Request.Path.StartsWithSegments("/api/hubs")`. **Done when:** existing auth tests pass. (FR-002, R2) **Deps:** —
- [X] T009 [P] Create the frontend foundation in `src/frontend/src/features/presence/`:
  - `presenceTypes.ts` with the TS payload types;
  - `presenceEvents.ts`, with validators that reject `v !== 1` and wrong shapes;
  - `presenceSlice.ts`, with the state from data-model.md "Frontend state" and these reducers: `statusChanged`, `joinRequested(workspaceId)`, `joinSucceeded(snapshot)` (resets the baseline and version), `snapshotReceived` (only if the workspace matches and the version is newer), `accessRevoked`, `presenceReset`, plus selectors.

  Register the reducer in `src/frontend/src/app/store.ts`. Clear presence on `signedOut` and `sessionEnded`. **Done when:** typecheck passes. (FR-027, R8) **Deps:** T001
- [X] T010 Create `src/frontend/src/features/presence/presenceClient.ts`:
  - `getPresenceClient(store)` returns one instance per store;
  - transport seam `setPresenceTransport(factory)` for tests, where the default builds `HubConnectionBuilder().withUrl('/api/hubs/workspace', { accessTokenFactory })`;
  - `accessTokenFactory` refreshes via `refreshSession` when 30 s or less is left;
  - handlers are registered exactly once per connection object;
  - `join(id)` uses a generation counter and drops stale results; `leave()`; `release()` uses a microtask-deferred stop that cancels if re-acquired;
  - its own reconnect loop: delays 0, 2000, 5000, 10000, 30000 ms, then `failed`, with `retry()`; it rejoins after reconnect and stops when the session has ended.

  **Done when:** typecheck passes. (FR-022–FR-026, R9, R10) **Deps:** T009

**Checkpoint**: the hub accepts connections, and the client can connect and join.

---

## Phase 3: User Story 1 — See who is online (P1) 🎯 MVP

**Goal**: members see the online list in every workspace header.
**Independent test**: two members in one workspace each see "2 online" and both names.

### Tests (write first)

- [X] T011 [P] [US1] Unit tests in `tests/backend/DevSync.UnitTests/Application/Presence/PresenceTrackerTests.cs`:
  - first join → `FirstConnection`, version 1, snapshot with the user;
  - second connection of the same user → listed once, not first;
  - a repeated join → `AlreadyJoined` with no version change;
  - a join of another workspace → leaves the previous one;
  - a snapshot is a copy;
  - workspace isolation.

  **Done when:** they pass. (FR-007, FR-010, FR-012) **Deps:** T005
- [X] T012 [P] [US1] Unit tests in `tests/backend/DevSync.UnitTests/Application/Presence/WorkspacePresenceServiceTests.cs`, using a fake `IWorkspaceAccess`, a fake `IUserDirectory` and the real tracker:
  - a member of any role joins;
  - a non-member, a malformed id and an empty id → `workspace_not_found`;
  - `v=2` → `invalid_request`;
  - the display name comes from the directory;
  - the race re-check undoes the join.

  **Done when:** they pass. (FR-005, FR-006, US4) **Deps:** T006
- [X] T013 [P] [US1] Integration tests in `tests/backend/DevSync.IntegrationTests/Presence/PresenceHubTests.cs`, with a helper `HubClient` in `Presence/HubClient.cs` (long polling through `CreateHandler()`, event channels):
  - an authenticated member connects and joins, and gets the snapshot with themselves;
  - a second member joins, and the first receives `UserJoined` and `OnlineUsersUpdated`, version 2;
  - the payload JSON has no `email` or `role`.

  **Done when:** they pass. (FR-008, FR-013, FR-017) **Deps:** T007, T002
- [X] T014 [P] [US1] Frontend tests in `tests/frontend/features/presence/PresenceBar.test.tsx`, with the fake hub in `tests/frontend/support/fakeHub.ts` installed in `setup.ts`:
  - connects and joins on opening a workspace page;
  - renders the avatars, "2 online", "+N" overflow with 6 users;
  - the popover lists names with "(you)" and closes with Escape;
  - the bar appears on the overview, files, diagrams list and canvas.

  **Done when:** they pass. (FR-028, FR-029) **Deps:** T010

### Implementation

- [X] T015 [US1] Create `src/frontend/src/features/presence/WorkspaceScope.tsx`, a layout route that calls `join(workspaceId)` on mount and param change and `release()` on unmount. Nest the four workspace routes under `<Route path="workspaces/:workspaceId" element={<WorkspaceScope/>}>` in `src/frontend/src/app/router.tsx`, keeping the same URLs. **Done when:** all existing route tests pass. (FR-022, FR-023) **Deps:** T010
- [X] T016 [US1] Create `src/frontend/src/features/presence/PresenceBar.tsx`: up to 4 `Avatar`s with an online dot, "+N", "N online", the popover "Online now" (keyboard, Escape, outside click, focus return), and dimming when stale. Add styles to `src/frontend/src/index.css` using the existing tokens. Place it in `WorkspaceNav.tsx` (tabs row, right end) and in the `CanvasHeader` of `DiagramPage.tsx`. **Done when:** T014 passes. (FR-028, FR-029, FR-033) **Deps:** T015

**Checkpoint**: US1 independently demonstrable.

---

## Phase 4: User Story 2 — Joining and leaving notifications (P1)

**Goal**: one notification per join and per leave, with the grace period absorbing reloads.
**Independent test**: A sees exactly one "joined" and one "left" when B opens and then closes.

- [X] T017 [P] [US2] Unit tests in `PresenceTrackerTests.cs` for the leave rules:
  - the last connection leaving → `PendingOffline` while the user is still listed;
  - `ConfirmOffline` → `Left` and a version bump;
  - a rejoin within the grace period → the token is cleared and a stale `ConfirmOffline` is a no-op;
  - a disconnect of an unknown connection → none;
  - a duplicate disconnect → none.

  **Done when:** they pass. (FR-014, FR-015, FR-016) **Deps:** T005
- [X] T018 [P] [US2] Integration tests in `PresenceHubTests.cs`, with the grace period at 200 ms:
  - stopping B's connection → A receives `UserLeft` (reason `left`) after the grace period;
  - B's reconnect within the grace period → A receives no `UserLeft` or `UserJoined`;
  - `LeaveWorkspace` twice → no error.

  **Done when:** they pass. (FR-009, FR-014, SC-003) **Deps:** T013
- [X] T019 [P] [US2] Frontend tests in `tests/frontend/features/presence/PresenceNotifications.test.tsx`:
  - `UserJoined` → the toast "Alex Kim joined the workspace.";
  - `UserLeft` → the toast "Alex Kim left the workspace.";
  - no toast about self;
  - the same version emitted twice → one toast;
  - an event with a version at or below the baseline, or for another workspace → ignored.

  **Done when:** they pass. (FR-030, FR-027) **Deps:** T010
- [X] T020 [US2] Implement the notification handling in `presenceClient.ts`: validate `UserJoined` and `UserLeft`, check the workspace and `version > baseline`, skip self, de-duplicate notified versions, then `dispatch(toastShown(...))`. **Done when:** T019 passes. (FR-030) **Deps:** T019

---

## Phase 5: User Story 3 — Tabs, devices and reconnection (P1)

**Goal**: correct presence across tabs and devices; reconnection restores presence.
**Independent test**: two tabs as one user, one tab closed → still online; network drop → reconnecting, then restored, with no duplicates.

- [X] T021 [P] [US3] Concurrency unit test in `PresenceTrackerTests.cs`. Run 200 parallel join, leave and disconnect operations for 10 users and 3 workspaces (`Parallel.ForEachAsync`). Afterwards check that every listed user has at least 1 connection or a pending token, that no connection appears twice, and that versions are strictly increasing per workspace. Add a test that two connections of one user ending at once give exactly one `PendingOffline`. **Done when:** they pass reliably 20 times in a row. (FR-016, NFR-003) **Deps:** T005
- [X] T022 [P] [US3] Integration tests in `PresenceHubTests.cs`:
  - one user with two connections → listed once; closing one sends A no `UserLeft`, closing both sends A exactly one;
  - a new connection after a stop → rejoin, the snapshot is correct, and no old connection id is reused.

  **Done when:** they pass. (FR-012, FR-024) **Deps:** T013
- [X] T023 [P] [US3] Frontend tests in `tests/frontend/features/presence/PresenceConnection.test.tsx`, using fake timers:
  - a drop → "Reconnecting to DevSync…" and the list marked stale;
  - success → rejoin and the list replaced, then "Connection restored.";
  - all retries failing → "Live presence unavailable" and Retry, which reconnects;
  - an expiring token → `refreshSession` is called before connecting;
  - an ended session → the loop stops;
  - handlers registered once across 3 reconnects (count calls to `on`).

  **Done when:** they pass. (FR-024–FR-027, FR-031) **Deps:** T010
- [X] T024 [US3] Create `src/frontend/src/features/presence/ConnectionStatus.tsx` (`role="status"`, polite) and render it beside `PresenceBar`. Finish the reconnect-loop behaviour in `presenceClient.ts` so that T023 passes. **Done when:** T023 passes. (FR-031) **Deps:** T023, T016

---

## Phase 6: User Story 4 — Only members (P1)

**Goal**: unauthenticated users and non-members cannot connect, join or observe.
**Independent test**: anonymous connect is refused; a non-member join gets `workspace_not_found` and no events.

- [X] T025 [P] [US4] Integration tests in `tests/backend/DevSync.IntegrationTests/Presence/PresenceSecurityTests.cs`:
  - negotiating with no token → 401;
  - an expired or garbage token → 401;
  - a token whose session has been revoked → 401;
  - a non-member join → `HubException` with message `workspace_not_found`, and members receive nothing;
  - a malformed id → `workspace_not_found`;
  - `v: 2` → `invalid_request`;
  - group isolation: A's events never reach a member of workspace B;
  - a `?access_token=` query on a regular `/api/workspaces` request → 401 (the query token works only for hubs);
  - the captured logs never contain the token string.

  **Done when:** they pass. (FR-002, FR-003, FR-005, FR-006, FR-011, NFR-002, SC-005) **Deps:** T013, T008
- [X] T026 [P] [US4] Frontend test in `PresenceBar.test.tsx`: a join refused with `workspace_not_found` → no presence bar content and no crash; the page shows its usual not-found handling. **Done when:** it passes. (FR-006) **Deps:** T014

---

## Phase 7: User Story 5 — Revocation (P2)

**Goal**: removal and session end take effect on live connections.
**Independent test**: remove an online member → they get `AccessRevoked` and the others see them leave at once; logout-all → connections closed.

- [X] T027 [P] [US5] Unit test in `PresenceTrackerTests.cs`: `Revoke` removes all of the user's connections in that workspace, bumps the version once, returns the connection ids, and has no grace period. **Done when:** it passes. (FR-020) **Deps:** T005
- [X] T028 [P] [US5] Integration tests in `tests/backend/DevSync.IntegrationTests/Presence/PresenceRevocationTests.cs`:
  - the Owner deletes a member → the member's two connections receive `AccessRevoked`, A receives `UserLeft` (reason `removed`) right away, and the member then gets no further group events;
  - a role change → no events;
  - logout → that session's connection closes;
  - logout-all → all the user's connections close and reconnecting with the old token fails;
  - advancing the fake clock past token expiry closes the connection.

  **Done when:** they pass. (FR-004, FR-020, FR-021, SC-006) **Deps:** T013
- [X] T029 [US5] Hook revocation into the endpoints:
  - call `PresenceBroadcaster.RevokeMembershipAsync` after a successful member removal in `src/backend/DevSync.Api/Endpoints/WorkspaceEndpoints.cs`;
  - call `EndSessionsAsync` for the current session after logout in `src/backend/DevSync.Api/Endpoints/AuthEndpoints.cs`, and for all the user's sessions after logout-all.

  **Done when:** T028 passes. (FR-004, FR-020) **Deps:** T028
- [X] T030 [US5] Frontend: on `AccessRevoked` for the current workspace, dispatch `accessRevoked`, show the toast "You no longer have access to this workspace.", and invalidate the `Workspace`, `FileTree` and `DiagramList` tags. Add a test in `PresenceNotifications.test.tsx`. **Done when:** the test passes. (FR-032) **Deps:** T020

---

## Phase 8: User Story 6 — Navigation (P2)

**Goal**: no stale or duplicate presence across navigation.
**Independent test**: A → B → A shows only the current workspace's members, with handlers registered once.

- [X] T031 [P] [US6] Frontend tests in `tests/frontend/features/presence/PresenceNavigation.test.tsx`:
  - overview → files → diagrams in one workspace → one join, no leave;
  - A → B → `LeaveWorkspace` is not needed (switching) and the join for B is invoked, with the list replaced by B's;
  - a slow join for A that resolves after navigating to B is discarded;
  - an A snapshot arriving while on B is ignored;
  - going to `/profile` → `stop()` is called;
  - `on` is registered once per connection.

  **Done when:** they pass. (FR-022, FR-023, FR-027, SC-004) **Deps:** T015
- [X] T032 [US6] Fix any gaps that T031 exposes in `WorkspaceScope.tsx` and `presenceClient.ts`. **Done when:** T031 passes. **Deps:** T031

---

## Phase 9: Polish and cross-cutting

- [X] T033 Regression: run every backend and frontend test, typecheck, lint, the build and `check:bundle`. **Done when:** everything passes, and the counts are recorded in `specs/005-workspace-presence/verification.md`. (NFR-006, SC-007) **Deps:** all
- [X] T034 [P] Write `docs/architecture/realtime-presence.md`, covering:
  - the architecture, hub endpoint and group naming;
  - the event contracts (with a link to the contract);
  - the authentication flow and the workspace authorization flow;
  - the connection lifecycle and reconnection strategy;
  - multi-connection handling and the grace period;
  - the in-memory limits and the distributed path;
  - local testing;
  - Mermaid diagrams (the required sequence diagram plus the lifecycle).

  **Done when:** it exists and is linked from the README. (FR-035) **Deps:** —
- [X] T035 [P] Write `docs/adr/0005-realtime-presence.md` (SignalR, query-string token on hubs only, in-memory tracker, versioned snapshots, own reconnect loop) and add it to `docs/adr/README.md`. **Done when:** written. (XI) **Deps:** —
- [X] T036 [P] Update `README.md`: the 005 row, a "Real-time presence" section (how to run and test it in two browsers, the optional `DevSync__Presence__GracePeriodSeconds`), and mark S05 done in the roadmap. Add any presence configuration to `.env.example` if it is introduced. **Done when:** written. (SC-008, X) **Deps:** —
- [X] T037 Rebuild Docker (`docker compose up --build -d`), and run quickstart scenarios 2, 3, 5 and 11 through two scripted clients or headless browsers against `http://localhost:8080`. Record the evidence in `verification.md`. **Done when:** recorded. (SC-001–SC-004) **Deps:** T033

---

## Dependencies

- Setup (T001–T003) → Foundational (T004–T010) → the stories.
- US1 (T011–T016) is the MVP. US2, US3 and US4 depend on the US1 test harness (T013, T014) and
  the bar (T016). US5 depends on US2's client event handling (T020). US6 depends on T015.
- Polish runs last.

## Parallel examples

- Phase 1: T002 and T003 alongside T001.
- US1: T011, T012, T013 and T014 together, then T015 → T016.
- US3: T021, T022 and T023 together.
- Polish: T034, T035 and T036 together.

## Implementation strategy

1. MVP = Phases 1–3: connect, join and see who is online.
2. Add US2 (notifications) and US3 (reconnection), the P1 stories needed for real use.
3. Harden with US4 (security tests), US5 (revocation) and US6 (navigation).
4. Polish: docs, regression and end-to-end evidence.
