# Quickstart: Verify Real-Time Workspace Presence

## Prerequisites

As for 001–004: Docker Desktop (or .NET SDK 10.0.402, Node 24 and PostgreSQL 18 for native
runs). No new environment variables. Optionally, set `DevSync__Presence__GracePeriodSeconds` to
change the 5-second offline grace period.

## Run

```powershell
docker compose up --build -d   # web http://localhost:8080, api :5080, db :15432
```

Native: `dotnet run --project src/backend/DevSync.Api` and `npm run dev` in `src/frontend`. Vite
proxies `/api` including WebSockets (`/api/hubs/workspace`).

## Automated checks

```powershell
dotnet test tests/backend/DevSync.UnitTests          # tracker + service
dotnet test tests/backend/DevSync.IntegrationTests   # hub (needs Docker for Testcontainers)
cd src/frontend; npm run typecheck; npm run lint; npm test; npm run build; npm run check:bundle
```

## Manual scenarios (two browsers, or a normal and a private window)

| # | Steps | Expected |
|---|-------|----------|
| 1 | A: register Priya, create "Checkout", invite Alex. B: register Alex, accept | — |
| 2 | Both open Checkout | Each header shows two avatars, "2 online". The popover lists both, with "(you)" on your own name (US1) |
| 3 | B closes the tab | A sees "Alex left the workspace." about 5 s later; the list drops to 1 (US2) |
| 4 | B reopens, then reloads the page | A sees one "joined" for the reopen and nothing for the reload (SC-003) |
| 5 | B opens a second tab and closes one | A sees no change (US3) |
| 6 | In B, DevTools → Network → Offline, wait, then Online | B shows "Reconnecting to DevSync…", then "Connection restored." and the correct list |
| 7 | Stop the API (`docker compose stop api`) for 60 s | B ends at "Live presence unavailable" + Retry; files and diagrams pages still render cached data. Start the API and press Retry: presence is back |
| 8 | B navigates Checkout → Payments → Checkout | A sees Alex leave and rejoin Checkout; Payments members never appear in Checkout |
| 9 | A (Owner) removes Alex while B is open | B: "You no longer have access to this workspace." and the not-found page. A: "Alex left the workspace." at once (US5) |
| 10 | Re-invite Alex. B signs out everywhere | A sees Alex leave after the grace period. B's other tabs are signed out |
| 11 | Mallory (not a member) edits the URL to Checkout | The "Workspace not found" page; nobody in Checkout sees Mallory |

## Where to look

- Hub logs: `docker compose logs api | Select-String presence.` shows structured events with
  `UserId` and `WorkspaceId`, and never tokens.
- The contract is in [contracts/workspace-hub.md](./contracts/workspace-hub.md), and the
  architecture is in `docs/architecture/realtime-presence.md`.
