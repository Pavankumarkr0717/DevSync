---

description: "Task list for feature 004-system-design-canvas"
---

# Tasks: System Design Canvas (with the app-wide design amendment)

**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md),
[docs/design/design-system.md](../../docs/design/design-system.md)

**Tests**: Required (SC-002–SC-009, NFR-002, NFR-003, constitution III and V). In each story,
write the tests first.

**Format**: `[ID] [P?] [Story] Description`. Every task ends with **Done when:** (its
completion condition), the requirement ids, and **Deps:**.

## Scope guard

Do not add:
- real-time sync, presence or cursors;
- export, auto-layout, AI, or links from diagrams to files;
- undo across saves;
- Redis or RabbitMQ;
- mockup-only features: search, notifications, favorites, trash, activity, share, present,
  comments, templates, a workspace switcher, or drawing tools.

The product name stays DevSync.

---

## Phase 1: Setup

- [X] T001 Add `"@xyflow/react": "12.12.0"` (exact) to `src/frontend/package.json` and install. **Done when:** typecheck, build and tests pass, and `npm audit` shows no new high or critical advisories. (plan; R2) **Deps:** —
- [X] T002 Move `autosave.ts`, `drafts.ts` and `saveOutcome.ts` from `src/frontend/src/features/files/` to `src/frontend/src/shared/editing/`, and update every import, including tests. **Done when:** all 003 frontend tests pass unchanged. (plan Frontend architecture) **Deps:** —

## Phase 2: Design system (FR-039, FR-040), blocking for UI work

- [X] T003 Replace the tokens in `src/frontend/src/index.css` with [design-system.md](../../docs/design/design-system.md): light and dark variables, the type scale, radius, shadows, focus ring, buttons, inputs, cards, tabs, banners, toast and empty state. **Done when:** the build passes and existing pages render with the new tokens. (FR-039) **Deps:** —
- [X] T004 [P] Create `src/frontend/src/shared/ui/icons.tsx`: inline SVG icons (home, workspace, mail, user, logout, plus, chevron, arrow-left, file, folder, diagram, grid, list, more, pencil, trash, zoom-in, zoom-out, fit, cloud-check, cloud-off, alert, check, x, and one icon for each of the 8 component types). They are `aria-hidden` by default. **Done when:** typecheck passes. (FR-039) **Deps:** —
- [X] T005 [P] Create `src/frontend/src/shared/ui/Avatar.tsx` (initials, with a pastel derived from the id), `Button.tsx` (primary, secondary, ghost, danger; with an icon), `EmptyState.tsx` and `Tabs.tsx` (segmented links). Add `tests/frontend/shared/ui/Avatar.test.tsx`. **Done when:** the test passes. (FR-039) **Deps:** T004
- [X] T006 Restyle `src/frontend/src/app/AppShell.tsx`:
  - a sidebar with the logo and nav (Workspaces, Invitations, Profile);
  - the person's card with Sign out at the bottom;
  - an icon rail below 1100 px and a menu button below 640 px;
  - a top bar with breadcrumbs (`shared/ui/Breadcrumbs.tsx`).

  Keep every accessible name the existing tests use. **Done when:** `AppShell.test.tsx` passes, with queries adjusted only where markup structure changed. (FR-039, FR-040) **Deps:** T003–T005
- [X] T007 Restyle the workspace pages:
  - `WorkspaceListPage` (greeting, Create workspace, cards), `CreateWorkspacePage`,
    `WorkspaceDetailsPage`, `MembersPanel`, `InvitationsPanel` and `EditWorkspaceForm`;
  - add `features/workspaces/WorkspaceHeader.tsx` (name, role chip, tabs Overview · Files ·
    Diagrams).

  **Done when:** every workspace test passes, and behaviour and texts are unchanged. (FR-039, FR-040) **Deps:** T006
- [X] T008 [P] Restyle the auth pages (Login, Register, Profile) and `InvitationsPage` as centered cards on the new tokens. **Done when:** the auth and invitation tests pass. (FR-039, FR-040) **Deps:** T006
- [X] T009 Restyle `FilesPage` to use `WorkspaceHeader` and the card panels for the explorer and editor. **Done when:** all file tests pass. (FR-039, FR-040) **Deps:** T007
- [X] T010 **Checkpoint**: the full frontend suite, typecheck, lint, build and `check:bundle` all pass. The design system has changed no behaviour. **Done when:** green. (FR-040, SC-007) **Deps:** T001–T009

## Phase 3: Foundational (diagrams)

### Domain, tests first

- [X] T011 [P] Write `tests/backend/DevSync.UnitTests/Domain/Diagrams/DiagramValidatorTests.cs` covering every rule in research R5 at its boundaries. Several problems are reported together, and paths are correct. **Done when:** it compiles against stubs and fails. (FR-012–FR-018, FR-033; NFR-003) **Deps:** —
- [X] T012 [P] Write `DiagramDocumentUpgraderTests.cs`:
  - the current version passes through;
  - a test-only v0→v1 step applies;
  - a newer version → unsupported;
  - garbage → unreadable.

  Also write `DiagramNameTests.cs`, and extend `WorkspacePermissionsTests` with EditDiagrams and DeleteDiagrams. **Done when:** they fail. (FR-004, FR-027–FR-030) **Deps:** —
- [X] T013 Implement `src/backend/DevSync.Domain/Diagrams/`: `DiagramLimits`, `ComponentType`, `DiagramName`, the document records (`DiagramDocument`, `DiagramNode`, `MetadataEntry`, `DiagramEdge`, `Viewport`), `DiagramSchema`, `DiagramProblem`, `DiagramValidator`, `DiagramDocumentUpgrader`, `Diagram` and `DiagramErrors`. **Done when:** T011 and T012 pass. (FR-003–FR-018, FR-027–FR-029, FR-033) **Deps:** T011, T012
- [X] T014 [P] Add `EditDiagrams` and `DeleteDiagrams` to `WorkspaceOperation` and `WorkspacePermissions`. **Done when:** the permission tests pass. (FR-030) **Deps:** T012

### Persistence

- [X] T015 Create `DiagramConfiguration.cs`, matching data-model.md: `document` jsonb with a CHECK that it is an object, count CHECKs, `ux_diagrams_workspace_name`, and FK indexes. Add the `DbSet` and the migration `SystemDesignCanvas`, and save its SQL script. **Done when:** the script shows one new table and nothing else. (FR-001, FR-004, FR-022; NFR-004) **Deps:** T013
- [X] T016 Write `Persistence/SystemDesignCanvasMigrationTests.cs`: a 003 database (with files) migrates and keeps everything; the constraints and indexes exist; a workspace delete cascades. Extend `MigrationTests` with the new table. **Done when:** it passes. (FR-037) **Deps:** T015

### API core

- [X] T017 Add 6 error codes (`diagram_not_found`, `diagram_name_taken`, `too_many_diagrams`, `invalid_diagram`, `unsupported_schema_version`, `stored_version_unsupported`) to `ErrorCodes` and `ErrorCatalog`. **Done when:** it builds. (FR-033) **Deps:** T013
- [X] T018 Create `Application/Diagrams/IDiagramRepository.cs` and the DTOs (`DiagramSummaryDto`, `DiagramDto`, `SaveDiagramResultDto`), and extend `Fakes.cs` with `FakeDiagramRepository`. **Done when:** it builds. (FR-001–FR-006, FR-022, FR-023) **Deps:** T013, T017
- [X] T019 Create `Infrastructure/Diagrams/DiagramRepository.cs`: list (projection without the document), a workspace lock on create, rename (unique index → name taken), CAS save via `ExecuteUpdate`, and delete. Register it in DI. **Done when:** it builds and T016 passes. (FR-023; research R6, R7) **Deps:** T015, T018
- [X] T020 [P] Create `Api/Diagrams/DiagramJson.cs`: strict `JsonSerializerOptions` (no duplicate or unknown properties, no comments or trailing commas), wire records with snake_case type values, mapping to and from the domain, and a canonical writer. Add a unit-free integration fixture that the JSON Schema example parses. **Done when:** it builds. (FR-033; research R5) **Deps:** T013
- [X] T021 Generalize `Api/Files/ContentBody.ReadAsync` into a bounded reader that takes an accepted media type and a byte limit (files: `text/plain`, 1 MiB; diagrams: `application/json`, 1 MiB). **Done when:** all 003 content tests pass. (research R12) **Deps:** —
- [X] T022 Create `Api/Endpoints/DiagramEndpoints.cs` (an empty group), map it in `Program.cs`, and add `tests/backend/DevSync.IntegrationTests/Infrastructure/DiagramTestData.cs` (create, get, save and document builders). **Done when:** it builds and the 001–003 tests pass. (FR-031) **Deps:** T019

### Frontend foundation

- [X] T023 [P] Add the diagram types to `shared/api/types.ts`, and create `features/diagrams/diagramsApi.ts` (list, get, create, rename, delete, saveDocument with `If-Match`; tags `DiagramList` and `Diagram`). **Done when:** typecheck passes. (FR-002, FR-022, FR-023) **Deps:** T001
- [X] T024 Create `features/diagrams/componentTypes.ts` (the 8-type registry: label, icon, accent, default title, default size), `document.ts` (`fromDocument`, and the canonical `toDocument`), `connectionRules.ts`, and `diagramEditorSlice.ts` (entity adapters for nodes and edges, viewport, status). Add tests `document.test.ts` and `connectionRules.test.ts`. **Done when:** the tests pass. (FR-010, FR-016, FR-017, FR-022; research R9) **Deps:** T023
- [X] T025 [P] Create `tests/frontend/support/handlers/diagrams.ts` (an in-memory API with revisions, roles and validation of the server rules that matter to the UI) and `tests/frontend/support/reactFlowEnv.ts` (ResizeObserver and DOMMatrix stubs loaded in setup). **Done when:** a smoke test renders a React Flow canvas in jsdom. (constitution V) **Deps:** T023

---

## Phase 4: US1 Create a diagram and lay out components (P1) 🎯 MVP

- [X] T026 [P] [US1] Integration test `Diagrams/DiagramCrudTests.cs`: create (revision 1, empty document, 201 with Location); list sorted ignoring case, without documents; a duplicate name ignoring case → 409; an invalid name → 400; the 51st diagram → 409, including a parallel create race; get with `ETag` and `no-store`. **Done when:** it fails. (FR-001–FR-007) **Deps:** T022
- [X] T027 [P] [US1] Unit tests for the List, Create, Get and Rename handlers with the fake repository. **Done when:** they fail. **Deps:** T018
- [X] T028 [US1] Implement those handlers in `Application/Diagrams/` and register them. Map `GET/POST /diagrams` and `GET /diagrams/{id}` (Read and EditDiagrams). **Done when:** T026 and T027 pass. (FR-001–FR-007) **Deps:** T026, T027
- [X] T029 [P] [US1] Frontend test `features/diagrams/DiagramListPage.test.tsx`: the empty state, create (and a name error), cards sorted, opening navigates. **Done when:** it fails. (US1-AS1, AS2, AS6) **Deps:** T025
- [X] T030 [P] [US1] Frontend test `features/diagrams/DiagramCanvas.test.tsx`: the empty canvas hint; the palette adds each of the 8 types (centred, snapped, default size and title); nodes render with a type icon and title; delete removes a node. **Done when:** it fails. (US1-AS2–AS5; FR-010, FR-013) **Deps:** T025
- [X] T031 [US1] Implement `DiagramListPage.tsx` (cards, create inline, empty state) and the routes `/workspaces/:id/diagrams` and `/workspaces/:id/diagrams/:diagramId`, and add the **Open diagrams** link and the Diagrams tab. **Done when:** T029 passes. (FR-035) **Deps:** T028, T007
- [X] T032 [US1] Implement `DiagramPage.tsx` (load, header, toolbar, lazy canvas), `DiagramCanvas.tsx` (ReactFlow with Background grid, MiniMap, snap 20, zoom 0.1–4, a drop target, and commit points for drag stop, resize end and delete), `ComponentNode.tsx` (card style, NodeResizer 40–2,000, 4 handles) and `Palette.tsx` (drag and click). **Done when:** T030 passes. (FR-010–FR-014, FR-019, FR-021) **Deps:** T024, T031

## Phase 5: US2 Connect components and describe them (P1)

- [X] T033 [P] [US2] Frontend test `DiagramEditing.test.tsx`:
  - connecting two nodes adds an edge;
  - a self-connection or a duplicate is refused with a toast;
  - the edge label is edited in the panel;
  - node title, description, type and metadata are edited (a duplicate key message, the 20-entry limit, text limits);
  - deleting a node removes its edges;
  - `<script>` and `__proto__` text is rendered literally.

  **Done when:** it fails. (US2; FR-015–FR-018, FR-034, FR-036) **Deps:** T025
- [X] T034 [US2] Implement `LabelledEdge.tsx`, `onConnect` with `isValidConnection` and toasts, and `PropertiesPanel.tsx` (node, edge and multi-selection forms with live limits). **Done when:** T033 passes. (FR-012, FR-013, FR-015–FR-018) **Deps:** T032, T033

## Phase 6: US3 Save, reopen and keep work safe (P1)

- [X] T035 [P] [US3] Integration test `Diagrams/DiagramDocumentTests.cs`:
  - a valid PUT round-trip (GET equals PUT, canonical);
  - every A4 rule through HTTP (`invalid_diagram`, with each problem's code and path), and the stored copy unchanged;
  - strict parsing (an unknown property, a duplicate key, a comment, a wrong type);
  - versions 0 and 2 → `unsupported_schema_version`;
  - 413, 415 and 428;
  - a 412 with `currentRevision`;
  - a stored version of 99 or an unreadable document → a GET with `document: null`, a PUT → 409, and replace → 200;
  - metadata `__proto__` and `<script>` stored as plain text.

  **Done when:** it fails. (FR-022–FR-029, FR-033) **Deps:** T022
- [X] T036 [P] [US3] Integration test `Diagrams/DiagramConcurrencyTests.cs`: 10 parallel PUTs → exactly one 200; a save racing a delete → 200 or 404, never 500. **Done when:** it fails. (FR-023; SC-004; NFR-003) **Deps:** T022
- [X] T037 [P] [US3] Unit tests `SaveDiagramDocumentHandlerTests.cs`: the CAS outcomes; a stored unsupported version is refused unless replace; the counts are mirrored. **Done when:** it fails. **Deps:** T018
- [X] T038 [US3] Implement `SaveDiagramDocumentHandler` and `PUT /diagrams/{id}/document` (bounded JSON reader, `If-Match`, strict parse, version check, validate, canonical store, `ETag`; problems in the Problem Details), plus the upgrade on GET. **Done when:** T035–T037 pass. (FR-022–FR-029, FR-033) **Deps:** T035–T037, T020, T021
- [X] T039 [P] [US3] Frontend test `DiagramSaving.test.tsx` (fake timers):
  - one drag → exactly one PUT after the pause;
  - pan and zoom only → no PUT;
  - the statuses Unsaved → Saving → Saved;
  - a reload shows the same document;
  - a 412 conflict bar with Reload latest and Overwrite with mine;
  - deleted and forbidden → read-only;
  - an invalid save → the problem message and Details;
  - offline → retry;
  - a draft is offered and restored, and a stale draft → conflict;
  - the unsupported banner, and Start over.

  **Done when:** it fails. (US3; FR-024–FR-029; SC-002, SC-009) **Deps:** T025
- [X] T040 [US3] Implement `diagramSession.ts` (`AutosaveController` and the draft writer on the canonical JSON), save status, conflict and draft bars, and the unsupported banner on `DiagramPage`. Commit points dispatch slice actions and feed the session. **Done when:** T039 passes. (FR-024–FR-029, FR-026a) **Deps:** T034, T038, T002

## Phase 7: US5 Role-appropriate access (P1)

- [X] T041 [US5] Extend `Authorization/RoleMatrixTests.cs` with ListDiagrams, CreateDiagram, GetDiagram, SaveDiagram, RenameDiagram and DeleteDiagram × 5 callers. The snapshot includes the diagrams. **Done when:** it passes once T042 is in. (FR-030, FR-031; NFR-002; SC-005) **Deps:** T038
- [X] T042 [US5] Map `POST /diagrams/{id}/rename` (EditDiagrams) and `DELETE /diagrams/{id}` (DeleteDiagrams), with `RenameDiagram` and `DeleteDiagram` handlers. Add an integration test `Diagrams/DiagramIsolationTests.cs`: changing or deleting A leaves B identical, and another workspace's diagram id → 404. **Done when:** T041 and the isolation test pass. (FR-006, FR-030–FR-032; SC-006) **Deps:** T041
- [X] T043 [P] [US5] Frontend test `DiagramRoles.test.tsx`:
  - **Viewer**: no palette, no handles, no rename or delete, a read-only panel, and selection still works;
  - **Editor**: create and rename, but no delete;
  - **Owner**: all actions;
  - a downgrade during editing → read-only.

  **Done when:** it fails, then passes after T044. (US5) **Deps:** T025
- [X] T044 [US5] Apply read-only mode in `DiagramCanvas`, `PropertiesPanel`, `Palette` and `DiagramListPage` by role and save failure. **Done when:** T043 passes. (FR-030) **Deps:** T043, T040

## Phase 8: US4 Navigate large diagrams (P2)

- [X] T045 [P] [US4] Frontend test `DiagramNavigation.test.tsx`: zoom buttons clamp at 10% and 400%, and the % is shown; Fit calls fit-view; the grid toggle is stored per person; multi-select moves together (one commit); a saved viewport is restored on open, and a missing one → fit. **Done when:** it fails, then passes after T046. (US4; FR-019, FR-020) **Deps:** T025
- [X] T046 [US4] Implement the toolbar zoom −/%/+, Fit and the grid toggle (`localStorage`, try/catch), viewport restore and fit on open. **Done when:** T045 passes. (FR-019, FR-020) **Deps:** T045, T032

## Phase 9: US6 Several independent diagrams (P2)

- [X] T047 [P] [US6] Frontend test `DiagramManage.test.tsx`: rename inline (errors), delete with the confirmation stating the name and component count, the 50 limit message, and opening one diagram doesn't change another. **Done when:** it fails, then passes after T048. (US6) **Deps:** T029
- [X] T048 [US6] Implement the rename and delete menu on the cards and the title pencil on the canvas header. **Done when:** T047 passes. (FR-004, FR-006) **Deps:** T047, T042

## Phase 10: Polish, regression, docs, end-to-end

- [X] T049 [P] Write `Security/DiagramLogScanTests.cs`: diagram names, titles, labels and metadata markers never appear in the logs. **Done when:** it passes. (NFR-001) **Deps:** T038
- [X] T050 Update `check-bundle.mjs` so React Flow is also asserted to be in a lazy chunk. **Done when:** it passes. (research R2) **Deps:** T032
- [X] T051 Run the full suites (backend build with 0 warnings, all tests; the frontend typecheck, lint, test, build and check:bundle). **Done when:** all green, with counts recorded. (SC-007) **Deps:** T001–T050
- [X] T052 [P] Docs: ADR `docs/adr/0004-diagram-document-storage.md` (+ index), the README (S04 section and the design system), and `docs/architecture/overview.md`. **Done when:** written and linked. (constitution X) **Deps:** —
- [X] T053 End-to-end: rebuild Compose over the 003 volume (migration applies, data kept); script quickstart A1–A15 through nginx; write `verification.md` with what wasn't run in a browser; add `checklists/traceability.md`. **Done when:** verification.md is complete. (SC-001–SC-009) **Deps:** T051
- [X] T054 Mark tasks `[X]` and add implementation notes. **Done when:** done. **Deps:** T053

## Dependencies

- Setup (T001–T002) → Design system (T003–T010) and Foundational (T011–T025, the backend in parallel with the design system).
- US1 → US2 → US3 (they share the canvas and session), then US5 → US4 → US6, then Polish.
- **Shared files** (sequential across tasks):
  - `DiagramEndpoints.cs`: T022 → T028 → T038 → T042
  - `DiagramCanvas.tsx`: T032 → T034 → T040 → T044 → T046
  - `index.css`: T003 → T006–T009
  - `RoleMatrixTests.cs`: T041

## Implementation notes (2026-10-31)

All 54 tasks are complete. Evidence: [verification.md](./verification.md) (717 backend tests,
247 frontend tests, 33/33 end-to-end API checks through nginx, the migration applied over the
003 volume with every row kept) and [checklists/traceability.md](./checklists/traceability.md).
**The browser scenarios D1–D17 were not run in a real browser**, because none was available in
this session. See verification.md "Not run in a real browser".

Deviations from the task text:

- **T032–T046**: there is no separate `DiagramCanvas.tsx`. The canvas, its commit points, the
  toolbar and the autosave session live in `DiagramEditor.tsx`, the lazily loaded default export
  that `DiagramPage.tsx` imports with `React.lazy`. Supporting modules: `DiagramStatusBars.tsx`
  (conflict, problem and draft bars), `saveStatus.ts` (status text), `canvasContext.ts` (resize
  commits from inside custom nodes), `diagramNames.ts` (name rules and messages) and
  `diagramSession.ts`.
- **T034**: besides dragging between handles, the properties panel has a **Connect to…** select
  for the selected component, so connections can be made without a pointer. It uses the same
  `connectionProblem` rules and toasts as `isValidConnection`/`onConnect`.
- **T033**: drawing a connection by dragging between handles is not simulated in jsdom. The
  self and duplicate rules are tested through `connectionRules` (shared by `isValidConnection`,
  `onConnect` and the panel) and through the panel select.
- **T040**: the loaded document is dispatched in a layout effect, not during render, and the
  canvas renders once the store holds this diagram. Dispatching from a state initializer updated
  the header while the editor was rendering.
- **T036, T042, T049**: the concurrency, isolation and log-scan tests share one file,
  `Diagrams/DiagramConcurrencyAndIsolationTests.cs` (the log scan is the `DiagramLogScanTests`
  class in it), rather than `DiagramConcurrencyTests.cs`, `DiagramIsolationTests.cs` and
  `Security/DiagramLogScanTests.cs`.
- **T025**: `tests/frontend/support/diagramCanvas.ts` adds helpers to open a canvas, select
  nodes and edges, and drag nodes. d3-drag needs `event.view`, which jsdom refuses in the event
  init, so the helper defines it on the event. The first render of React Flow in a test file is
  slow, so the canvas test files warm the lazy chunk in `beforeAll` and use a 20 s timeout.
- **T033/T039**: panel text fields commit every valid intermediate value as the person types,
  and never an invalid one (an empty title, a duplicate metadata key). Autosave then debounces
  those commits into one save.
