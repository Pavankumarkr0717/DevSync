# Implementation Plan: System Design Canvas

**Branch**: `004-system-design-canvas` (to be created from the committed 003 branch) | **Date**:
2026-10-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/004-system-design-canvas/spec.md`. It was
clarified on 2026-10-31: autosave like files; structural connection rules only; Owner and Editor
create and rename, Owner-only delete.

## Summary

Each workspace gets several architecture diagrams.
- **Storage** (research R1): each diagram is **one row** in a new `diagrams` table. Its content
  is a **versioned JSON document** (nodes, edges, viewport) in a `jsonb` column, replaced
  whole on every save and guarded by a revision compare-and-swap, as file content is in 003.
- **Validation** (R5): the server is authoritative. It parses strictly (no unknown or duplicate
  properties), validates the whole document with a pure domain `DiagramValidator`, then stores a
  canonical re-serialization. Any problem refuses the save with a list of coded problems.
- **Versions** (R4): forward-only, pure upgrade steps applied on read. A stored version newer
  than the app is never overwritten without an explicit "Start over".
- **Canvas** (R2): React Flow (`@xyflow/react` 12.12.0) in a lazy-loaded chunk. One generic
  node renderer is driven by a registry of the 8 component types.
- **Frontend state** (R9): Redux holds the **normalized persisted document**, using entity
  adapters for nodes and edges. React Flow holds **transient interaction** state: drag
  positions, selection and the live viewport. Changes reach Redux only at commit points (drag
  stop, resize end, connect, delete, panel edit), so a drag is one save.
- **Reuse from 003**: the autosave controller, drafts, the `If-Match`/`ETag` helpers, the
  status, conflict and draft UI, and authorization through `RequireWorkspace` with two new
  operations.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (SDK 10.0.402); TypeScript 6.0 on Node 24. Unchanged.

**Primary Dependencies (new)**: `@xyflow/react` 12.12.0 (frontend; MIT; peer React ≥ 17). No
backend packages; JSON uses `System.Text.Json` (.NET 10 `AllowDuplicateProperties = false`,
`UnmappedMemberHandling = Disallow`).

**Storage**: PostgreSQL 18. One additive migration, `SystemDesignCanvas`, with one table
(`diagrams`, including a `jsonb` document column). See [data-model.md](./data-model.md).

**Testing**:
- backend: xUnit v3 unit tests (validator, upgrader, names, permissions) and Testcontainers
  integration tests;
- frontend: Vitest, React Testing Library and MSW, with React Flow rendered in jsdom using
  ResizeObserver and DOMMatrix stubs, plus fake timers for autosave;
- end-to-end: the API through nginx, scripted as in 003.

**Target Platform**: local Docker Compose and native development. No cloud.

**Project Type**: web application. Layout per [ADR 0001](../../docs/adr/0001-repository-layout.md).

**Performance Goals**: none claimed (Principle XII). By design, a list read never loads
documents, a save is one statement, and a drag produces one save.

**Constraints**:
- No real-time synchronization, Redis or RabbitMQ.
- Metadata is inert text.
- Edge references and node types are validated on the server.
- The schema version is stable at 1, with an upgrade path.
- S01–S03 behaviour is unchanged.

**Scale/Scope**:
- 6 new endpoints and 1 table.
- 2 new routes, and a link added to the details page.
- 2 permission operations.
- The 003 editing helpers move to `shared/` (no behaviour change).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Principle | How this plan complies | Pre | Post |
|---|-----------|------------------------|-----|------|
| I | Incremental, spec-driven | Only spec 004 scope. No real-time sync, export, auto-layout, AI or links to files. No undo across saves. | ✅ | ✅ |
| II | Clean architecture | Document types, the validator, upgrader, names and permissions are pure domain code. One new port, `IDiagramRepository`. Strict JSON parsing and HTTP details stay in Api. React Flow is confined to the canvas components. | ✅ | ✅ |
| III | Correctness first | Measurable SC-001–009. Every validation rule has its own code and test. Revision CAS with concurrent tests. Commit points make "one drag = one save" testable. | ✅ | ✅ |
| IV | Security by default | `RequireWorkspace` on every route, with the role read per request. Ids scoped to the workspace. Strict parsing rejects unknown and duplicate properties. Metadata is inert, length-limited, control-character-free text, rendered as text. 1 MiB body cap. `no-store` on reads. | ✅ | ✅ |
| V | Testability | Unit, integration (role matrix, races, every rule, versions, logs) and component tests (canvas, palette, panel, autosave, conflicts, roles). See Test Strategy. | ✅ | ✅ |
| VI | Real-time correctness | N/A: no real-time. Conflicts are detected per save. | — | — |
| VII | Persistence & recoverability | PostgreSQL is authoritative. Additive versioned migration. Single-statement writes. Documents are validated before storing, and stored versions are checked before reading (Principle VII's snapshot rule). Drafts are labelled, non-authoritative recovery. | ✅ | ✅ |
| VIII | Observability | Ids, revisions, counts and problem codes only; never diagram text (NFR-001), checked by a log-scan test. | ✅ | ✅ |
| IX | Simplicity & cost | One table with `jsonb`, no node/edge tables (R1 trade-off); reuses the 003 mechanisms; no new services. | ✅ | ✅ |
| X | Developer experience | No new configuration. Quickstart and README updated. ADR 0004. | ✅ | ✅ |
| XI | Dependency discipline | One frontend package (`@xyflow/react`), justified in R2. Inline SVG icons, no icon library. The JSON Schema is documentation, not a runtime dependency. | ✅ | ✅ |
| XII | Evidence-based performance | No performance figures claimed. | ✅ | ✅ |

**Gate result**: PASS.

## Persistence decision: JSONB document vs relational nodes and edges

Full comparison: [research.md R1](./research.md).

| | JSONB document (chosen) | Relational `diagram_nodes` / `diagram_edges` |
|-|-------------------------|---------------------------------------------|
| Unit of save and conflict | The whole diagram, one CAS statement | Many rows; needs a diff plus a multi-statement transaction |
| Edge → node integrity | Validator on every write, plus tests | Database foreign keys |
| A drag of 50 nodes | One document rewrite on save | Up to 50 row updates |
| Schema evolution | Versioned pure transforms (R4) | EF migrations plus data migrations |
| Cross-diagram queries | Not needed in S04; possible with JSON operators | Plain SQL |

The deciding factors: the spec's atomic unit is the diagram (FR-022, FR-023), there are no
queries inside diagrams, and the user's constraint not to normalize canvas movements. Database
foreign keys are replaced by an authoritative validator that runs on every write, with a test
per rule.

## API design

Full contract: [contracts/openapi.yaml](./contracts/openapi.yaml). Document schema:
[contracts/diagram-document.v1.schema.json](./contracts/diagram-document.v1.schema.json).

| Method & path (under `/api/workspaces/{workspaceId}`) | Operation | Success | Notable errors |
|------------------------------------------------------|-----------|---------|----------------|
| `GET /diagrams` | Read | 200 `DiagramSummary[]` | — |
| `POST /diagrams` `{ name }` | EditDiagrams | 201 summary (revision 1) | 400 name · 409 `diagram_name_taken` / `too_many_diagrams` |
| `GET /diagrams/{id}` | Read | 200 `Diagram` + `ETag` + `no-store` (upgraded document, or `document: null` + `unsupportedSchemaVersion`) | 404 `diagram_not_found` |
| `POST /diagrams/{id}/rename` `{ name }` | EditDiagrams | 200 summary | 400 · 404 · 409 |
| `PUT /diagrams/{id}/document` + `If-Match` [+ `?replaceUnsupported=true`] | EditDiagrams | 200 `{ revision, nodeCount, edgeCount, updatedAt }` + `ETag` | 400 `invalid_diagram` (+`problems[]`) / `unsupported_schema_version` · 404 · 409 `stored_version_unsupported` · **412** `revision_conflict` · 413 · 415 · **428** |
| `DELETE /diagrams/{id}` | DeleteDiagrams | 204 | 404 |

Error-code design:
- **One code for content problems**, `invalid_diagram`, with a detailed `problems` list
  (`code`, `path`, `message`). The client then handles a single status, while every rule still
  has a stable machine code (FR-033).
- **Reused from 003**: `revision_conflict`, `revision_required`, `content_too_large` and
  `unsupported_media_type`, with the same meaning.

## Validation: app vs server

| Rule | App (instant feedback, FR-034) | Server (authoritative, FR-033) |
|------|-------------------------------|--------------------------------|
| No self-connection, no duplicate edge | `isValidConnection` refuses; toast | `self_connection`, `duplicate_edge` |
| Edges reference existing nodes | Deleting a node deletes its edges | `unknown_node_reference` |
| Unique node and edge ids | Generated with `crypto.randomUUID()` | `duplicate_node_id`, `duplicate_edge_id`, `invalid_id` |
| Known component type | Palette and type select offer only the 8 | `unknown_node_type` |
| Text lengths, control characters | `maxLength`, counters, input filtering | `text_required`, `text_too_long`, `invalid_text` |
| Metadata ≤ 20, unique keys ignoring case | Add button disabled at 20; inline duplicate message | `too_many_metadata`, `duplicate_metadata_key` |
| Sizes, positions, zoom | `NodeResizer` min/max, clamped drag, `minZoom`/`maxZoom` | `out_of_range` |
| Node and edge counts | Palette disabled at 500; connections refused at 1,000 | `too_many_nodes`, `too_many_edges` |
| Document shape, unknown or duplicate properties | The app only produces canonical documents | `malformed` (strict parser) |
| Schema version | Always sends 1 | `unsupported_schema_version` |
| Body size | Serialized length warning before sending | 413 `content_too_large` |

## Concurrency and versions

- **Save**: `UPDATE diagrams … WHERE id = @d AND workspace_id = @w AND revision = @expected`.
  Exactly one of N same-revision saves wins; the rest get 412 with `currentRevision`. A
  deleted diagram → 404. Rename doesn't touch the revision.
- **Create**: the workspace row lock serializes creates, so the 50-diagram limit can't be
  overshot. The unique index backs up name uniqueness.
- **Versions** (R4):
  - read → upgrade chain, with nothing written;
  - write → must be the current version;
  - a stored version newer than the app → read-only response and 409 on PUT, unless
    `replaceUnsupported`.

  The production chain is empty at v1; the machinery is proven with a test-only v0 → v1 step.

## Frontend architecture

```text
features/diagrams/
  diagramsApi.ts            RTK Query: list, get, create, rename, delete, saveDocument (If-Match)
  diagramEditorSlice.ts     normalized persisted document (createEntityAdapter for nodes, edges) + status
  document.ts               toDocument(state, viewport) canonical serializer; fromDocument(doc) normalizer
  componentTypes.ts         registry: 8 types → label, SVG icon, accent colour, default title, default size
  connectionRules.ts        isValidConnection (no self, no duplicate, limits); also used by tests
  DiagramListPage.tsx       list, empty state, create, rename, delete (role-aware)
  DiagramPage.tsx           loads diagram, unsupported banner, owns DiagramSession, lazy-loads the canvas
  DiagramCanvas.tsx         (lazy) ReactFlow: Background(grid), Controls, MiniMap, snap, zoom limits,
                            drop target, commit-point handlers, read-only mode
  ComponentNode.tsx         generic node renderer + NodeResizer + 4 handles
  LabelledEdge.tsx          edge with arrow marker and text label
  Palette.tsx               draggable/clickable type buttons
  PropertiesPanel.tsx       node, edge and multi-selection forms with live limits
  diagramSession.ts         AutosaveController + draft writer for one diagram (like FileSession)
shared/editing/             autosave.ts, drafts.ts, saveOutcome.ts moved from features/files (003)
                            plus SaveStatus/ConflictBar/DraftBar made generic (text via props)
```

**Data flow**:
1. Load: RTK Query → `fromDocument` → Redux (normalized) → selectors → React Flow nodes and
   edges.
2. Interaction: React Flow changes are applied to local state. At a **commit point**, a Redux
   action fires (`nodeMoved`, `nodeResized`, `edgeAdded`, `elementsDeleted`, `nodeUpdated`
   and so on).
3. The Redux change is serialized by `toDocument` (canonical, with the current React Flow
   viewport) and passed to `session.change(json)`. The `AutosaveController` takes it from
   there: 1 s pause, 10 s maximum wait, one save in flight, retry, conflict pause.
4. Pan and zoom alone are not commits. The viewport rides along with the next content save
   (R9).

**Read-only mode** (Viewer, an unsupported version, or a failure of `forbidden` or `deleted`):
- nodes can't be dragged or connected, there's no resizer and no palette;
- the panel is disabled and the Delete key does nothing;
- pan, zoom, fit and selection still work for inspection.

## Threat considerations

| Threat | Mitigation | Verified by |
|--------|------------|-------------|
| Viewer, non-member or anonymous modifying diagrams | `RequireWorkspace(EditDiagrams/DeleteDiagrams)`, role read per request | Role matrix (6 endpoints × 5 callers) |
| IDOR: another workspace's diagram id | Every query filters by the route's workspace id | Integration test |
| Smuggling data or code via unknown fields | Strict parser rejects unknown and duplicate properties; canonical re-serialization stores only known fields | Validator and API tests (`malformed`) |
| "Executable" metadata (script text, `__proto__` keys) | Metadata is an array of inert strings, never evaluated; React renders text; keys are never used as object properties on the client | Component test with `<script>` and `__proto__` values; API accepts them only as plain text |
| Dangling references / corrupt graph | Validator on every write | One test per rule |
| Oversized payloads | 1 MiB cap on the bounded reader; count limits | 413 and limit tests |
| Lost updates | Revision CAS; `If-Match` required | Parallel-save test |
| Silent overwrite of a newer format | 409 `stored_version_unsupported` unless explicitly replaced | Version test |
| Diagram text in logs | Ids, counts and codes only | Log scan |

## Project Structure

### Documentation (this feature)

```text
specs/004-system-design-canvas/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/openapi.yaml, contracts/diagram-document.v1.schema.json, contracts/ui-routes.md
├── checklists/requirements.md
└── tasks.md                     # /speckit-tasks
```

### Source code (additions)

```text
src/backend/
├── DevSync.Domain/
│   ├── Diagrams/                 DiagramLimits, ComponentType, DiagramName, DiagramDocument (+ records), DiagramSchema,
│   │                             DiagramValidator, DiagramProblem, DiagramDocumentUpgrader, Diagram, DiagramErrors
│   └── Workspaces/               WorkspaceOperation (+EditDiagrams, DeleteDiagrams), WorkspacePermissions
├── DevSync.Application/
│   ├── Common/ErrorCodes         (+6 codes)
│   └── Diagrams/                 IDiagramRepository; List/Create/Get/Rename/SaveDocument/Delete handlers; DTOs
├── DevSync.Infrastructure/
│   ├── Persistence/              DiagramConfiguration (jsonb), DbSet, migration SystemDesignCanvas
│   └── Diagrams/                 DiagramRepository (workspace lock on create; CAS save via ExecuteUpdate)
└── DevSync.Api/
    ├── Endpoints/DiagramEndpoints.cs
    ├── Diagrams/DiagramJson.cs   strict JsonSerializerOptions, wire ↔ domain mapping, canonical writer
    └── Files/ContentBody.cs      bounded reader generalized to a media type (used by files and diagrams)

tests/backend/
├── DevSync.UnitTests/Domain/Diagrams/       DiagramValidatorTests (every rule), DiagramUpgraderTests, DiagramNameTests
├── DevSync.UnitTests/Application/Diagrams/  handler tests with a fake repository
└── DevSync.IntegrationTests/Diagrams/       Crud, Document (rules, sizes, versions), Concurrency, Isolation;
                                             RoleMatrixTests extended; Security/DiagramLogScanTests; Persistence migration test

src/frontend/src/features/diagrams/   (see Frontend architecture)
src/frontend/src/shared/editing/      moved from features/files (imports updated; 003 tests unchanged)
tests/frontend/features/diagrams/     list, canvas, palette, panel, connections, autosave/commit points,
                                      conflict, unsupported, roles, document serializer
tests/frontend/support/               reactFlowEnv.ts (ResizeObserver/DOMMatrix stubs), handlers/diagrams.ts
```

**Structure decision**: a new `Diagrams` module folder in each backend layer, mirroring `Files`.
The 003 editing helpers move to `shared/editing` because two features now use them; that is
reuse, not a new abstraction.

## Test Strategy

| Layer | Covers |
|-------|--------|
| **Unit: Domain** | `DiagramValidator`: every rule in research R5 at its boundary (499/500/501 nodes; 1,000/1,001 edges; title 100/101; metadata 20/21; key case duplicates; size 39/40/2,000/2,001; coordinates ±100,000; zoom 0.1/4; NaN/∞; control characters; lone surrogates), several problems reported at once, paths correct. `DiagramDocumentUpgrader`: current passes through; test-only v0→v1 applies; newer → unsupported; garbage → unreadable. `DiagramName`. `WorkspacePermissions` with the new operations |
| **Unit: Application** | Handlers with a fake repository: create limit and name; save maps CAS results (saved / conflict / not found); stored-unsupported refusal and replace; counts mirrored |
| **Integration: API** | CRUD round-trip; the canonical document round-trip (what GET returns equals what was PUT, ignoring property order); every A4 rule through HTTP with the stored diagram unchanged; strict parsing (unknown property, duplicate key, comments, wrong types); 413/415/428; `no-store`/`ETag` |
| **Integration: Concurrency** | 10 parallel PUTs from one revision → one 200; create at the 50 limit in parallel → never 51; save racing delete → 200 or 404, never 500 |
| **Integration: Isolation** | Saving, renaming and deleting diagram A leaves diagram B's document, revision and name byte-identical (SC-006) |
| **Integration: Versions** | A stored `schema_version` 99 → GET read-only form, PUT 409, replace → 200; a stored unreadable document → same |
| **Integration: Authorization** | Role matrix extended: 6 diagram endpoints × {Owner, Editor, Viewer, non-member, anonymous}, with refused calls leaving the diagrams snapshot unchanged |
| **Integration: Security / persistence** | Log scan for diagram text markers; migration over a 003 database keeps all data; `jsonb` CHECK and unique index present; workspace delete cascades |
| **Integration: Regression** | Every 001–003 test passes (SC-007) |
| **Component (FE)** | Serializer: canonical and stable; `fromDocument`/`toDocument` round-trip. Canvas: renders nodes by type, read-only for Viewers. Palette adds a node at the centre, snapped. Panel edits title, description and metadata (duplicate key message, 20 limit) and type. Connections: self and duplicate refused with a toast; deleting a node removes its edges. **Commit points**: simulated drag → exactly one `PUT` after the pause; pan and zoom → no `PUT`. Conflict bar reload/overwrite. Unsupported banner and Start over. Draft restore. List: create, rename, delete per role; empty states |
| **End-to-end** | quickstart A1–A15 scripted through nginx; D1–D17 in a real browser if available, otherwise recorded as not run |

## Local development and verification

- **Compose**: `docker compose up --build -d`. The migration runs on start; no new variables.
- **Native**: as in 003. React Flow is bundled; no network needed.
- **Tests**: `dotnet test --solution src/backend/DevSync.sln`, then the frontend `typecheck`,
  `lint`, `test`, `build` and `check:bundle`.
- **Evidence**: `specs/004-system-design-canvas/verification.md`.

## Trade-offs

| Choice | Gain | Cost / accepted risk |
|--------|------|----------------------|
| JSONB whole-document storage | Atomic save and conflict per diagram; no diff logic; easy versioning | No database FKs inside diagrams (validator instead); whole document rewritten per save |
| Strict parsing + canonical re-serialization | No unknown data stored; stable comparisons | Clients must send exactly the schema; adding a field needs a new version |
| Viewport saved only with content changes | Panning never causes conflicts or extra revisions | A view change with no edit is not remembered |
| Shared viewport per diagram | Simple; matches the spec assumption | Co-editors' last save sets everyone's opening view |
| Redux for the persisted document, React Flow for transient state | One save per drag; Redux stays small and serializable | Two state holders; the local state is re-seeded from Redux after each commit |
| One generic node renderer + registry | Adding a type is one registry entry | All types share one layout (icon + title + description) |
| Read-side version upgrades, never write-back on read | Reads are side-effect free; no read/save races | Old-format documents stay old until edited |
| React Flow tested in jsdom | Fast component tests | Real drag geometry and rendering verified only manually or in the browser checks |

## Visual design amendment (FR-039, FR-040)

The app adopts [docs/design/design-system.md](../../docs/design/design-system.md), taken from the
reference mockups.

**Implementation**:
- design tokens in `index.css` (light and dark);
- inline SVG icons in `shared/ui/icons.tsx`;
- shared UI components (`Button`, `Avatar`, `Card`, `EmptyState`, `Tabs`);
- an `AppShell` with sidebar, icon rail and top bar, plus breadcrumbs;
- a `WorkspaceHeader` with tabs;
- existing pages restyled in place.

**Constraints**:
- **No behaviour changes**: every existing test must pass with at most changes to
  queries that depend on markup structure; accessible names and texts stay.
- **No web font download**: Inter is used only when installed; no CDN (the bundle check
  still applies).
- **Mockup-only features are not built.**

## Phased implementation

0. **Design system**: tokens, icons, shared components, shell, and restyle of the existing
   pages, keeping all 001–003 tests green.
1. **Domain**: limits, types, names, document records, validator, upgrader, permissions, with
   unit tests.
2. **Persistence**: `Diagram` configuration (`jsonb`), the migration, the repository (lock on
   create, CAS save), and the migration test.
3. **Backend API**: strict JSON options and mapping, generalized bounded reader, endpoints,
   error codes. Integration tests: CRUD, rules, versions, concurrency, isolation, role matrix,
   log scan.
4. **Frontend foundation**: install `@xyflow/react`; move the editing helpers to
   `shared/editing`; `diagramsApi`, `diagramEditorSlice`, the serializer, routes and links.
5. **Diagram list**: create, rename, delete, empty states, roles.
6. **Canvas**: React Flow setup, node and edge components, the registry, palette, grid, zoom,
   fit, selection.
7. **Editing**: connections and rules, properties panel, delete, commit points.
8. **Saving**: `DiagramSession` (autosave and drafts), status, conflict bar, unsupported banner
   and Start over.
9. **Docs and verification**: ADR 0004, README, architecture overview, the bundle check covering
   the canvas chunk, the quickstart run, and `verification.md`.

## Complexity Tracking

No constitution violations. The one new port (`IDiagramRepository`) isolates the row lock, the
CAS SQL and `jsonb` mapping. Moving the 003 helpers to `shared/editing` reduces duplication
rather than adding layers.
