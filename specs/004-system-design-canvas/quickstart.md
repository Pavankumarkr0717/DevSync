# Quickstart & Validation: System Design Canvas

**Feature**: [spec.md](./spec.md) | **Contracts**: [openapi.yaml](./contracts/openapi.yaml),
[diagram-document.v1.schema.json](./contracts/diagram-document.v1.schema.json),
[ui-routes.md](./contracts/ui-routes.md) | **Data model**: [data-model.md](./data-model.md)

## Setup

Same as 003. No new environment variables or services.

```bash
docker compose up --build -d
docker compose logs api | grep SystemDesignCanvas   # "Applying 1 migration(s): …_SystemDesignCanvas"
```

Existing workspaces, files and accounts are kept. Every workspace starts with no diagrams.

**Setup for the scenarios**: Ana (Owner) creates "Demo"; Ben joins as **Editor** and Cy as
**Viewer** (as in 002 B2–B7).

## Browser scenarios (http://localhost:8080)

| # | Steps | Expected |
|---|-------|----------|
| D1 | Ana: Demo → **Open diagrams** | The empty state, with **New diagram** |
| D2 | Create "Checkout" | An empty canvas: "Drag a component from the palette to start." |
| D3 | Drag Client, API Gateway, Microservice ×2 and Database onto the canvas; resize the Database | Each type has its own icon; positions snap to the 20-unit grid; sizes stay within 40–2,000 |
| D4 | Connect Client→API Gateway ("HTTPS"), Gateway→each service ("REST"), a service→Database ("SQL") | Arrows point at the targets; labels show |
| D5 | Try connecting the Gateway to itself, and a second Gateway→Orders | Refused, with a toast; nothing added |
| D6 | Select a service: set its title, description and metadata `owner=payments`, `replicas=3`; add a second `OWNER` key | Fields update the node. The duplicate key shows an inline message |
| D7 | Drag one node across the canvas, then wait | DevTools → Network shows exactly **one** `PUT …/document` after the drag ends, not one per movement. The status goes Unsaved → Saving… → Saved |
| D8 | Pan and zoom only, and wait 15 s | **No** save is sent (the view is saved with the next content change) |
| D9 | Delete the Database | Its "SQL" edge disappears with it |
| D10 | Reload the browser | The same nodes, edges, labels, positions, sizes and view |
| D11 | Zoom to 10% and 400%, then **Fit**; box-select 3 nodes and move them; toggle **Grid** off and move a node | Zoom is limited to 10–400%; Fit shows everything; the 3 move together; without the grid, positions are free |
| D12 | Create "Target" and add one node; reopen "Checkout" | "Checkout" is unchanged |
| D13 | Ben (Editor) edits and saves "Checkout"; renames "Target" → "Target 2027" | Allowed; Ben has no Delete action |
| D14 | Cy (Viewer) opens "Checkout" | Can pan, zoom, fit and select to read the properties (read-only). No palette, no handles, no rename or delete; the status says "Read only" |
| D15 | Ana and Ben both have "Checkout" open; Ben moves a node (saved); then Ana moves another | Ana sees "Someone else saved a newer version" with Reload latest / Overwrite with mine. Ben's change is kept until Ana chooses |
| D16 | Ana deletes "Target 2027" (the confirmation names it and counts its components) | Gone from the list; Ben's open copy shows "This diagram was deleted" on his next change |
| D17 | Ben moves a node and reloads within 1 s | After the reload, either the change is saved, or "You have unsaved changes…" offers Restore / Discard |

## API scenarios

Use a bearer token as in 003 (`T`, workspace `W`, diagram `D`), with `Content-Type:
application/json`.

| # | Call | Expected |
|---|------|----------|
| A1 | `POST /api/workspaces/$W/diagrams` `{"name":"Checkout"}`; again with `"checkout"` | 201 (revision 1, 0 nodes); then 409 `diagram_name_taken` |
| A2 | `GET …/diagrams/$D` | 200, `ETag: "1"`, document `{"schemaVersion":1,"nodes":[],"edges":[],"viewport":null}` |
| A3 | `PUT …/diagrams/$D/document` with `If-Match: "1"` and a valid document of 3 nodes and 2 edges | 200, revision 2, `nodeCount: 3`, `edgeCount: 2` |
| A4 | PUT with an edge whose `target` is unknown; a duplicate node id; `source == target`; two A→B edges; `"type":"mainframe"`; a 101-character title; a `"script"` property on a node | 400 `invalid_diagram` with one problem each: `unknown_node_reference`, `duplicate_node_id`, `self_connection`, `duplicate_edge`, `unknown_node_type`, `text_too_long`, `malformed`. GET still returns revision 2, unchanged |
| A5 | PUT with `"schemaVersion": 2` (and with 0) | 400 `unsupported_schema_version` |
| A6 | PUT with `If-Match: "1"` (stale) | 412 `revision_conflict`, `currentRevision: 2` |
| A7 | PUT without If-Match; with `text/plain`; a body over 1 MiB | 428; 415; 413 |
| A8 | 10 parallel PUTs with `If-Match: "2"` | Exactly one 200 and nine 412 |
| A9 | As Cy (Viewer): POST create, PUT document, rename, DELETE | 403 each; nothing changed |
| A10 | As Ben (Editor): DELETE a diagram | 403 |
| A11 | As a non-member: `GET …/diagrams` | 404 `not_found` |
| A12 | Another workspace's diagram id under `$W` | 404 `diagram_not_found` |
| A13 | Set a stored document's version to 99 in the database, then GET and PUT | GET: `document: null`, `unsupportedSchemaVersion: 99`. PUT → 409 `stored_version_unsupported`. PUT `?replaceUnsupported=true` → 200 |
| A14 | Create 50 diagrams, then one more | 409 `too_many_diagrams` |
| A15 | `docker compose logs api` after A3–A6 | No diagram names, titles, labels or metadata in the logs |

## Automated tests

```bash
dotnet test --solution src/backend/DevSync.sln
npm run typecheck --prefix src/frontend && npm run lint --prefix src/frontend
npm test --prefix src/frontend
npm run build --prefix src/frontend && npm run check:bundle --prefix src/frontend
```

Expected: everything passes, including every 001–003 test (SC-007). The bundle check also
covers the canvas chunk.

## Evidence

Record results in `specs/004-system-design-canvas/verification.md`, and state explicitly what
wasn't run in a real browser.
