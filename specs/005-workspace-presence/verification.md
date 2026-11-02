# Verification Evidence: Real-Time Workspace Presence

**Date**: 2026-11-01 | **Feature**: [spec.md](./spec.md) | **Tasks**: T033, T037

## Environment

| Tool | Version |
|------|---------|
| OS | Windows 11 (10.0.26300) |
| .NET SDK | 10.0.402 |
| Node.js | 24.21.0 |
| Docker / Compose | 29.8.0 |
| PostgreSQL | `postgres:18-alpine` |
| New packages | `@microsoft/signalr` 10.0.11 (frontend); `Microsoft.AspNetCore.SignalR.Client` 10.0.14 (integration tests only). The API uses the SignalR server already in the shared framework. |

## Automated suites (T033)

| Command | Result |
|---------|--------|
| `dotnet build src/backend/DevSync.sln` | Succeeded, **0 warnings** |
| `dotnet test tests/backend/DevSync.UnitTests` | **372 passed**, 0 failed (31 new presence tests; 341 before) |
| `dotnet test tests/backend/DevSync.IntegrationTests` | **401 passed**, 0 failed (25 new hub tests; 376 before) |
| `npm run typecheck` (app + tests) | Passed |
| `npm run lint` (app + tests) | Passed, no findings |
| `npm test` | **280 of 281 passed** (41 files; 27 new presence tests; 254 before). The one failure is the existing 004 test below |
| `npm run build` | Succeeded. The SignalR client is in the entry chunk (529 kB, 161 kB gzipped) |
| `npm run check:bundle` | Monaco and React Flow are still lazy-loaded, and there are no CDN references |

**Load note**: in two full-suite runs on a loaded machine, a different existing test timed out
each time: `InvitationsPanel` once, and `DiagramSaving`'s drag-and-reload test once. Both passed
when re-run on their own. `DiagramSaving` passed 13/13 three times in a row, and the whole suite
passed 280/280 in two later runs. This matches the load-related timeouts recorded for 004.
In the final check (with only about 0.9 GB of RAM free), `DiagramSaving` "one drag is one save
… survives a reload" failed every time, including on its own. **It fails the same way on the
committed pre-S05 code** (checked in a temporary worktree of `6fb504a`), so it is an existing
004 test that is sensitive to timing on a loaded machine, not a regression from this feature.

### What the new tests cover

| Area | Tests |
|------|-------|
| Tracker (unit) | First join, multiple tabs listed once, idempotent join, switching workspaces, isolation, sorted copies, unknown or foreign connections, pending offline and confirmation, rejoin within grace, stale tokens, duplicate disconnects, a 200-operation parallel stress test, simultaneous last disconnects producing exactly one pending departure, session and user lookups, revocation (at once, pending users, not online) |
| Service (unit) | All three roles allowed; non-member equals missing; 5 malformed ids; schema `v` 0 and 2; missing request; membership re-read each time |
| Hub (integration, real SignalR over the test server) | Join snapshot; `UserJoined` and `OnlineUsersUpdated` with consistent versions; no email, role, session or connection fields; `UserLeft` after the grace period; explicit leave twice; silent reconnect within grace with a new connection id; two tabs; switching workspaces |
| Security (integration) | No token, garbage token, ended session → 401; query-string token accepted only on `/api/hubs`; non-member, missing and malformed ids → `workspace_not_found` with nothing broadcast; `v: 2` → `invalid_request`; no cross-workspace events; tokens absent from captured logs |
| Revocation (integration) | Removal evicts both tabs (`AccessRevoked`), others see `UserLeft` `removed`, no later events reach the removed member, and they cannot rejoin; a role change emits nothing; logout closes only that session's connection; logout-all closes all and blocks reconnect; the access token expiring on the app clock closes the connection |
| Frontend | Connect and join payload; avatars, self first, "+N", the summary with "(you)", Escape; snapshot versioning and invalid payloads; overview, files, diagrams and canvas pages; refused join; no connection outside workspace pages; notifications (once, not about self, not about the past, not for other workspaces or schemas); access revoked → not found; reconnecting and stale state, restored (including after a server restart with lower versions); giving up and Retry; workspace usable without presence; token refresh before connecting; stop when the session ends; handlers registered once across reconnects; one join across pages; switching on one connection; stale slow join discarded; foreign snapshot ignored; stop when leaving workspace pages |

## End to end through nginx (T037)

`docker compose up --build -d`, then a Node script with `@microsoft/signalr` over **WebSockets
through nginx** (`http://localhost:8080`). The nginx log shows `101` upgrades for
`/api/hubs/workspace`.

| Check | Result |
|-------|--------|
| Joiner receives the list including themselves | PASS |
| `UserJoined` reaches the other member (SC-001, under 2 s) | PASS, 5–21 ms |
| No email or role in payloads | PASS |
| Closing one of two tabs announces nothing (SC-004) | PASS |
| Reload within the grace period is silent (SC-003) | PASS |
| `UserLeft` after the 5 s grace period (SC-002) | PASS, 5.0–6.0 s |
| Non-member join → `workspace_not_found` (SC-005) | PASS |
| Invalid token cannot connect | PASS |
| Removed member gets `AccessRevoked`, and others see the removal (SC-006, under 2 s) | PASS, 14–39 ms |
| Logout closes the live connection (SC-006) | PASS, 11–31 ms |

These timings are from one local machine. They are not a performance claim (constitution XII).

**Browser** (headless Chrome over CDP, five other members joining from Node clients):
- the overview, Files and canvas headers show the avatars, the "+N" overflow and "N online";
- the summary popover lists every name, with "(you)" on the viewer's own;
- toasts read "Alex Kim joined the workspace." and "Alex Kim left the workspace.";
- stopping the API container shows "Reconnecting to DevSync…" with the list dimmed, and starting
  it again restores the connection.

**Second finding fixed during verification**: Docker runs the API in Development without a
fixed signing key, so an API restart invalidates every access token. The reconnect loop kept
presenting the old, still-unexpired token and gave up after five 401s. The client now renews the
token after a 401 before its next attempt. This is covered by the test "renews a token the
server rejects … and reconnects". It wasn't re-checked in the browser because Docker Desktop
had stopped.

**Finding fixed during verification**: browsers send the hub token as `?access_token=`, and
nginx's default access log records query strings. `nginx.conf` now uses a `devsync_no_query`
log format. A probe request with `access_token=SECRETPROBE` left no trace in the nginx log, and
the API logs contained no JWTs (`eyJ…`).
