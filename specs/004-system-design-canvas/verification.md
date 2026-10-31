# Verification Evidence: System Design Canvas

**Date**: 2026-10-31 | **Feature**: [spec.md](./spec.md) | **Tasks**: T050, T051, T053

## Environment

| Tool | Version |
|------|---------|
| OS | Windows 11 (10.0.26300) |
| .NET SDK | 10.0.402 (Microsoft.Testing.Platform) |
| Node.js / npm | 24.21.0 / 10.9.0 |
| Docker / Compose | 29.8.0 / v5.5.1 |
| PostgreSQL | `postgres:18-alpine` |
| New packages | `@xyflow/react` 12.12.0 |

## Automated suites (T050, T051)

| Command | Result |
|---------|--------|
| `dotnet build src/backend/DevSync.sln -c Release` | Succeeded, **0 warnings** |
| `dotnet test --solution src/backend/DevSync.sln -c Release` | **717 passed**, 0 failed (341 unit, 376 integration; up from 559 after 003) |
| `npm run typecheck` (app + tests) | Passed |
| `npm run lint` (app + tests) | Passed, no findings |
| `npm test` | **247 passed**, 0 failed (36 files; up from 179 after 003, which became 185 with the design-system checkpoint) |
| `npm run build` | Succeeded. React Flow is in the lazy `DiagramEditor` chunk (≈199 KB, ≈64 KB gzipped) with its own CSS; the entry chunk doesn't contain it |
| `npm run check:bundle` | Monaco only in the lazy `CodeEditor` chunk, React Flow only in the lazy `DiagramEditor` chunk, no CDN references |

What the new tests cover (traceability: [checklists/traceability.md](./checklists/traceability.md)):

- **Domain**: every validator rule with a test each, names, the upgrade chain (with a test-only
  v0 → v1 step) and canonical serialization.
- **API**: create, list, get, rename, delete and save, every error code and status, strict JSON
  (duplicate and unknown properties, comments, non-finite numbers), several problems at once,
  the 1 MiB boundary, `If-Match`/`ETag`, and stored newer or unreadable versions.
- **Concurrency**: 10 parallel saves from one revision → exactly one 200; a save racing a
  delete → 200 or 404, never 500; parallel creates can't exceed 50 diagrams.
- **Isolation**: changing or deleting one diagram never touches another; another workspace's
  diagram id is 404 even for a member of both.
- **Authorization**: the role matrix grew by 6 diagram operations × 5 callers, plus the
  snapshot.
- **Migration**: `SystemDesignCanvas` over a 003 database keeps every row.
- **Logs**: a scan finds no diagram names, titles, labels or metadata.
- **Frontend** (62 new tests in `tests/frontend/features/diagrams`): the list, canvas, editing,
  saving, navigation, roles and management. React Flow runs in jsdom with ResizeObserver and
  DOMMatrix stubs (`tests/frontend/support/reactFlowEnv.ts`). Drags are simulated with mouse
  events as d3-drag sees them, so "one drag → one save" and "multi-select moves together"
  exercise React Flow's own drag handling.

## End-to-end against Docker Compose (T053)

**Upgrade path**: `docker compose up --build -d` over the existing 003 database volume.

| | Before | After |
|-|--------|-------|
| Migrations | `InitialWorkspaceFoundation`, `IdentityAndPermissions`, `ProjectFiles` | + `20261031081026_SystemDesignCanvas` (logged "Applying 1 migration(s)") |
| Users / workspaces / items / file contents | 11 / 5 / 4 / 3 | 11 / 5 / 4 / 3 |
| Diagrams | — | 0 |
| `/health/ready` | Healthy | Healthy |

**API scenarios**: quickstart A1–A15, scripted with real HTTP requests through nginx
(`http://localhost:8080`) by four freshly registered accounts (Owner, Editor, Viewer,
non-member). **Result: 33/33 checks passed.**

| Check | Result |
|-------|--------|
| A1 create → 201, revision 1, 0 nodes, `Location`; the same name in other case → 409 `diagram_name_taken` | Pass |
| A2 get → `ETag "1"`, `no-store`, empty v1 document | Pass |
| A3 valid save of 3 nodes and 2 edges → revision 2, counts 3/2, `ETag "2"`; the document round-trips exactly | Pass |
| A4 `unknown_node_reference`, `duplicate_node_id`, `self_connection`, `duplicate_edge`, `unknown_node_type`, `text_too_long`, `malformed` (a `script` property) → 400 `invalid_diagram` each; still revision 2 | Pass (8 checks) |
| A5 `schemaVersion` 2 and 0 → 400 `unsupported_schema_version` | Pass |
| A6 stale `If-Match` → 412, `currentRevision: 2` | Pass |
| A7 no `If-Match` → 428; `text/plain` → 415; over 1 MiB → 413 Problem Details from the API (not nginx) | Pass |
| A8 10 parallel saves → exactly one 200 and nine 412 | Pass |
| A9 Viewer create, save, rename, delete → 403 each; nothing changed | Pass |
| A10 Editor delete → 403 | Pass |
| A11 non-member list → 404 `not_found` | Pass |
| A12 another workspace's diagram id → 404 `diagram_not_found` | Pass |
| A13 stored version set to 99 in the database: GET → `document: null`, `unsupportedSchemaVersion: 99`; PUT → 409 `stored_version_unsupported`; PUT `?replaceUnsupported=true` → 200 | Pass |
| A14 the 51st diagram → 409 `too_many_diagrams` | Pass |
| Owner delete → 204, then 404 | Pass |
| A15 `docker compose logs api`: no diagram names, titles, labels or metadata markers | Pass |
| nginx serves the SPA for `/workspaces/{id}/diagrams` and `/workspaces/{id}/diagrams/{diagramId}` | Pass |

The script leaves its test accounts and workspaces (named `E2E diagrams …` and `E2E other …`)
in the Compose database, as the 003 run did.

## Not run in a real browser

No browser automation was available in this environment, so these were **not** checked by
eye. They are covered by component tests in jsdom, which don't lay out or paint:

- The quickstart browser scenarios D1–D17: dragging from the palette with a real pointer, drawing a
  connection between handles, resizing with the corner handles, keyboard deletion, the minimap
  and grid rendering, and dark mode on the canvas.
- The restyled pages against the reference mockups (FR-039), at desktop, tablet and phone widths.
- SC-008 (a diagram at 500 nodes and 1,000 edges stays usable): no performance figure is
  claimed (constitution XII).

Recommended before release: open `http://localhost:8080`, create a diagram, and walk through
quickstart D1–D17 in a browser.
