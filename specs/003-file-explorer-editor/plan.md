# Implementation Plan: Project Files and Code Editor

**Branch**: `003-file-explorer-editor` (git repository initialized; first commit pending GPG
signing) | **Date**: 2026-10-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/003-file-explorer-editor/spec.md`. It was
clarified on 2026-10-31: only the Owner deletes; only the 11 listed extensions are allowed; on
leave, the app saves immediately and also keeps a browser draft.

## Summary

Each workspace gets a tree of folders and text files. The files are **records in PostgreSQL,
addressed by id, never host paths**.
- **Storage**: an adjacency-list `workspace_items` table holds the tree. Content lives in a 1:1
  `file_contents` table with an integer `revision`.
- **Structure changes** (create, rename, move, delete) run under a per-workspace row lock. A pure
  domain `ProjectTree` validates names, extensions, depth, path length, item count and cycles,
  so the tree can never be left inconsistent.
- **Content saves** are a single compare-and-swap `UPDATE … WHERE revision = @expected`, exposed
  over HTTP as a raw-text `PUT` with `If-Match`/`ETag`. A stale save gets 412, never a silent
  overwrite.
- **Authorization** reuses S02's `RequireWorkspace` filter, with two new operations:
  `EditFiles` (Owner, Editor) and `DeleteFiles` (Owner).
- **Frontend**:
  - Monaco, bundled locally with tokenizers only, inside a lazy-loaded files page.
  - An explorer tree, and up to 20 tabs.
  - One autosave controller per file: 1 s debounce, 10 s maximum wait, one save in flight.
  - Explicit save states, a conflict bar, and `localStorage` drafts and tab lists keyed by user.
- **Authority**: the server copy is the only authoritative content. The editor buffer and drafts
  are always labelled unsaved.

All decisions are in [research.md](./research.md) (R1–R15).

## Technical Context

**Language/Version**: C# 14 / .NET 10 LTS (SDK 10.0.402); TypeScript 6.0 on Node 24. Unchanged.

**Primary Dependencies (new)**:
- `monaco-editor` 0.57.0 (frontend; released 2026-09-24)
- `@monaco-editor/react` 4.7.0 (frontend; peer range includes React 19 and monaco ≥ 0.25 < 1)
- Backend: none. It uses the existing EF Core 10, Npgsql 10 and ASP.NET Core authorization.

**Storage**: PostgreSQL 18. One new additive EF migration, `ProjectFiles`, with two tables
([data-model.md](./data-model.md)).

**Testing**: as in 001/002:
- backend: xUnit v3 unit tests; integration tests with WebApplicationFactory, Testcontainers
  and `FakeTimeProvider`;
- frontend: Vitest, React Testing Library and MSW, plus fake timers for autosave. Monaco is
  replaced by a textarea stand-in in jsdom.

**Target Platform**: local Docker Compose and native development. No cloud.

**Project Type**: web application. Layout per
[ADR 0001](../../docs/adr/0001-repository-layout.md).

**Performance Goals**: none claimed (Principle XII). The design keeps per-request work bounded:
- A tree read is one indexed query over ≤ 5,000 small rows.
- A save is one statement.
- A structure change loads ≤ 5,000 small rows under a row lock.

**Constraints**:
- Files never touch the host filesystem.
- 1 MiB per file, 5,000 items per workspace, folder depth 20, paths up to 1,024 characters,
  20 open tabs.
- No real-time sync, Redis, RabbitMQ, object storage, Git, execution or language services.
- File content is never logged.

**Scale/Scope**:
- 7 new endpoints; no existing endpoint changes.
- 2 new tables.
- 1 new page; 1 link added to the details page.
- 2 new permission operations.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Principle | How this plan complies | Pre | Post |
|---|-----------|------------------------|-----|------|
| I | Incremental, spec-driven | Only spec 003 scope. No real-time sync, history, search, upload, Git or execution. Monaco language services are deliberately left out (R8). | ✅ | ✅ |
| II | Clean architecture | Tree rules, names, extensions, content rules and permissions are pure domain code. One new port (`IProjectFileRepository`) isolates EF/SQL. HTTP specifics (`If-Match`, raw body) stay in Api. | ✅ | ✅ |
| III | Correctness first | Measurable criteria SC-001–009. Concurrency is explicit and tested: CAS saves, a workspace lock for structure, parallel-save and cycle-race tests. Every rejection has a distinct code. | ✅ | ✅ |
| IV | Security by default | Every endpoint uses `RequireWorkspace(op)` with the role read per request. Items are addressed by (workspace, id) only, so there are no paths to traverse. Server-side validation of names, extensions, size and encoding. `Cache-Control: no-store` on content. Role matrix tests. | ✅ | ✅ |
| V | Testability | Unit tests (domain, application), integration tests (API + PostgreSQL, role matrix, races, limits, logs) and component tests (explorer, tabs, autosave, conflict, drafts). See Test Strategy. | ✅ | ✅ |
| VI | Real-time correctness | N/A: no real-time. Freshness comes from refetch on focus; correctness from revisions (R12). | — | — |
| VII | Persistence & recoverability | PostgreSQL is authoritative. Additive versioned migration. Explicit transactions per operation (data-model.md). Drafts are a labelled, non-authoritative recovery aid. | ✅ | ✅ |
| VIII | Observability | Correlation ids as before. File endpoints log ids, sizes and revisions only, never names or content (NFR-001); a log-scan test checks this. | ✅ | ✅ |
| IX | Simplicity & cost | Two tables in the existing database. A row lock instead of SERIALIZABLE retries. Adjacency list instead of ltree or a closure table. No new services. | ✅ | ✅ |
| X | Developer experience | No new configuration. Monaco is bundled, so development works offline. Quickstart covers the browser and curl. README and an ADR are updated in the docs phase. | ✅ | ✅ |
| XI | Dependency discipline | Two frontend packages, both justified in R8 (the user specified Monaco; the wrapper avoids hand-written lifecycle code). No backend packages. | ✅ | ✅ |
| XII | Evidence-based performance | No performance figures are claimed. | ✅ | ✅ |

**Gate result**: PASS.

## Contract changes

| Change | Kind | Why |
|--------|------|-----|
| `/api/workspaces/{id}/tree`, `/items` (POST), `/items/{itemId}` (DELETE), `/items/{itemId}/rename`, `/items/{itemId}/move`, `/files/{itemId}/content` (GET, PUT) | New | US1–US6 |
| 14 new error codes (`item_not_found` … `revision_required`) | Additive | FR-028 |
| `412`, `413`, `415` and `428` statuses on the content endpoint | New | R5 |
| `WorkspaceOperation.EditFiles` and `DeleteFiles` | Internal | FR-024 |

Every 001/002 request and response shape is unchanged
([contracts/openapi.yaml](./contracts/openapi.yaml)).

## Database design

Full detail is in [data-model.md](./data-model.md).

```text
workspaces 1───* workspace_items (id, workspace_id, parent_id → workspace_items, kind, name, normalized_name, …)
                       │  UNIQUE (workspace_id, parent_id, normalized_name) NULLS NOT DISTINCT
                       │  parent_id ON DELETE CASCADE  → deleting a folder removes its subtree atomically
                       └─1───1 file_contents (item_id PK/FK, content text, size_bytes, revision, updated_at, updated_by)
                                CHECK size_bytes ≤ 1048576 AND size_bytes = octet_length(content)
```

- **Why an adjacency list** (R1): rename and move touch one row, and identity never changes.
  No stored path can drift or carry `..`.
- **Why separate content** (R3): tree reads never load content, rename and move never change
  the revision, and large values are compressed automatically (TOAST).
- **Database backstops** for the domain rules: the unique sibling-name index, size and revision
  CHECKs, and cascading FKs.

## API design

Full detail is in [contracts/openapi.yaml](./contracts/openapi.yaml). Every endpoint lives under
`/api/workspaces/{workspaceId}`:

| Method & path | Operation | Success | Notable errors |
|---------------|-----------|---------|----------------|
| `GET /tree` | Read | 200 `{ items, limits }` | — |
| `POST /items` `{ parentId, kind, name }` | EditFiles | 201 `Item` | 400 name/extension/depth/path · 404 parent · 409 `name_taken` / `too_many_items` |
| `POST /items/{itemId}/rename` `{ name }` | EditFiles | 200 `Item` | 400 · 404 · 409 `name_taken` |
| `POST /items/{itemId}/move` `{ parentId }` | EditFiles | 200 `Item` | 400 `invalid_move` / depth / path · 404 · 409 |
| `DELETE /items/{itemId}` | DeleteFiles | 204 | 404 |
| `GET /files/{itemId}/content` | Read | 200 `text/plain` + `ETag` + `no-store` | 404 |
| `PUT /files/{itemId}/content` + `If-Match` | EditFiles | 200 `{ revision, sizeBytes, updatedAt }` + `ETag` | 400 `invalid_content` · 404 · **412** `revision_conflict` (+`currentRevision`) · 413 · 415 · **428** |

Design notes:
- **Rename and move are separate endpoints**, not a `PATCH`. That avoids the absent-vs-null
  ambiguity of `parentId`. In `move`, `parentId` is required (`[JsonRequired]`), and `null`
  means the top level.
- **Content is raw text, not JSON** (R5). The bytes stored are the bytes sent, the 1 MiB limit is
  exact, and `If-Match` makes stating the base revision mandatory.
- **The tree returns `limits`**, so the client validates with the server's numbers rather than a
  copy.
- **`Problems`** gains an overload that adds extension members, used for `currentRevision` on 412.

## Path and content security

- **No paths in requests.** Items are addressed only by `(workspaceId, itemId)`, and every query
  filters by both. The workspace id was already authorized by `RequireWorkspace`. An item id
  from another workspace, or a malformed id, is `item_not_found` (FR-027).
- **Names can't express traversal**: `/` and `\` are rejected, as are `.`, `..`, control
  characters and `< > : " | ? *` (R6). Names are rejected, never sanitized.
- **Content rules**:
  - strict UTF-8 decoding (invalid bytes and lone surrogates are rejected);
  - no U+0000;
  - at most 1,048,576 bytes, checked by the API while reading the body, with nginx
    `client_max_body_size 2m` so the API's own 413 is what the client sees (R13).
- **Content is never executed or rendered as HTML.** Monaco renders it as text; Markdown is
  highlighted, not previewed.
- **`Cache-Control: no-store`** on content keeps source code out of the browser's HTTP cache.

## Concurrency strategy

| Race | Mechanism | Result |
|------|-----------|--------|
| Two saves from the same revision | `UPDATE … WHERE revision = @expected` (atomic CAS) | One 200; the others get 412 with `currentRevision` |
| Save vs delete | The CAS matches 0 rows after the delete, then a follow-up existence check | Save → 404 "deleted"; or save first, then delete waits on the row lock |
| Save vs rename or move | Different rows/tables; content identity is the item id | Both succeed; the revision is unchanged by rename or move |
| Two structure changes (e.g. A→B and B→A moves, two creates with the same name) | `SELECT … FOR UPDATE` on the workspace row, then validation over the loaded tree | Serialized; the second sees the first's result and is validated against it |
| Structure change vs save | No shared lock | Never blocked by each other |
| Same person, two tabs | Same CAS | The second tab gets a conflict, as for two people (US3-AS4) |

**Why not PostgreSQL `xmin`** (R4): it's opaque, couples the API to storage internals, and can
change for reasons other than content. An explicit revision is readable, testable and maps
cleanly to an `ETag`.

**Trade-off**: per-workspace structure changes are serialized. That's fine for the expected
human-paced editing, and saves, the hot path, don't take the lock.

## Frontend design

### State boundaries (R9)

```text
RTK Query  filesApi          getTree(ws) [tag FileTree:ws] · getFileContent(ws,id) [tag FileContent:id] · create/rename/move/delete (invalidate FileTree)
                              · saveFileContent (updates the FileContent cache with the new revision; never invalidates → no refetch of what we just sent)
Redux      editorSlice       byWorkspace[ws]: { openTabs[], activeId, expanded{} } · files[id]: { status, reason?, baseRevision, draftOffered? }
Monaco     models            one per open file (URI devsync://<ws>/<itemId>): text, undo stack, cursor, scroll — NOT in Redux
Plain TS   AutosaveController one per open file: timers, in-flight flag, last-sent text; dispatches status actions
Storage    localStorage       devsync:tabs:<user>:<ws>, devsync:draft:<user>:<item> (try/catch; failure is non-fatal)
```

- **Why the buffer stays out of Redux**: up to 1 MiB × 20 tabs, changing per keystroke.
  Dispatching it would copy megabytes per key and re-render subscribers. Redux receives only
  status *transitions* (saved → dirty happens once, not per key).
- **S02 integration**: the existing listener that resets the RTK Query cache on account change
  also covers file data. `signedOut` additionally clears the user's tabs and drafts.
  `permissionDenied` also invalidates `FileTree`.

### Components (`src/frontend/src/features/files/`)

| Component | Responsibility |
|-----------|----------------|
| `FilesPage` | Route `/workspaces/:workspaceId/files`. Loads the workspace (role) and tree; 404 → the existing not-found view. Lazy-loads the editor pane. |
| `FileExplorer` / `TreeNode` | Builds nested nodes from the flat list. Sorting, keyboard navigation, role-aware actions, inline create and rename, "Move to…" dialog, delete confirmation with the subtree count |
| `EditorTabs` | Tab strip: dedupe, 20-tab limit, path disambiguation, status markers, close with flush |
| `CodeEditor` | The only module that imports Monaco. Language from extension, `readOnly` for Viewers, one model per file id. Replaced in jsdom tests. |
| `SaveStatus`, `ConflictBar`, `DraftBar` | Status display and the conflict/draft choices |
| `autosave.ts` | `AutosaveController` (R10) |
| `drafts.ts`, `tabStorage.ts` | Typed `localStorage` access, with 7-day draft purge |
| `fileRules.ts` | Client copy of the name/extension rules, for instant feedback only |
| `monacoSetup.ts` | `loader.config({ monaco })`, the 9 tokenizer contributions, and the editor worker (R8) |

### Autosave and save status (R10)

```text
           edit                       1 s idle  or  10 s since first unsaved edit
  Saved ─────────► Unsaved changes ─────────────────────────────────────────► Saving
    ▲                  ▲   │ flush (tab switch/close, route leave, page hidden)  │
    │                  │   └────────────────────────────────────────────────────►│
    │  200, text == sent                                                         │
    ├────────────────────────────────────────────────────────────────────────────┤
    │  200, text changed meanwhile → Unsaved changes → (save again)              │
    │                                                                            │
  Save failed: conflict (412) ── paused ── Reload latest | Overwrite with mine | Copy mine
  Save failed: deleted (404) ─── stopped; buffer copyable
  Save failed: no permission (403) ─ read-only; buffer copyable
  Save failed: too large (413) / invalid content (400) ─ waits for next edit
  Save failed: offline (network/5xx) ─ auto-retry 2 s, 4 s, 8 s … ≤ 30 s, and on next edit
```

- There is at most one save in flight per file. Saves are never aborted; the response always
  updates the base revision.
- "Overwrite with mine" saves the buffer with `If-Match` set to the `currentRevision` from the
  412. That is a deliberate, informed overwrite (FR-023).
- **On `pagehide`**: write the draft synchronously, and send a `keepalive` save only if the body
  is ≤ 64 KiB (the browser limit; R11).

### Loading, errors and retry

| Situation | Behaviour |
|-----------|-----------|
| Tree loading | `Loading` in the explorer; the editor area is empty |
| Tree error | `ErrorMessage` with Retry (existing component). 404 → "Workspace not found" |
| Opening a file | The tab shows a spinner until content arrives. 404 → tab removed + toast "File no longer exists" + tree refetch |
| Structure mutation error | Inline error next to the input or dialog. 404 → toast + tree refetch. 403 → `permissionDenied` + refetch role |
| Token expiry during any call | S02 `baseQueryWithReauth` refreshes and retries transparently. If the session ended, edits are kept as drafts (not cleared on `sessionEnded`) |
| Unmount / navigation | Queries are aborted by RTK Query; pending saves are flushed, not aborted |

## Threat considerations

| Threat | Mitigation | Verified by |
|--------|------------|-------------|
| Path traversal / host file access | No paths in the API; ids only; no filesystem I/O at all; names can't contain separators | Name rule unit tests; P10/P12 integration tests |
| IDOR: another workspace's item id | Every query filters by the route's workspace id | Cross-workspace integration test |
| Viewer or Editor exceeding rights via direct calls | `RequireWorkspace(EditFiles / DeleteFiles)`, role read per request | Role matrix (7 endpoints × 5 callers) |
| Lost updates | CAS revision; `If-Match` mandatory (428) | Parallel-save test, 412 tests |
| Tree corruption by races (cycles, duplicates, limit overshoot) | Workspace row lock + domain validation + unique index | Concurrent move/create integration tests |
| Oversized payloads / memory pressure | The API reads at most 1 MiB + 1 bytes; nginx 2 MiB; 5,000-item cap | 413 boundary tests |
| Invalid or binary content corrupting storage | Strict UTF-8, no NUL; DB CHECK on size | Content-rule unit tests and API tests |
| XSS through file names or content | React escapes names; Monaco renders text only; no Markdown/HTML preview; no `dangerouslySetInnerHTML` | Lint rule; component test with `<script>` in names and content |
| Source code leaking into logs or caches | No body logging; ids and sizes only; `Cache-Control: no-store` | Log-scan test; header assertion |
| Drafts readable on a shared computer | Keyed by user; cleared on sign-out; purged after 7 days | Draft component tests; accepted residual risk documented (R11) |
| Third-party script loading | Monaco bundled; no CDN | Build check: no external URLs in the editor chunk |

## Project Structure

### Documentation (this feature)

```text
specs/003-file-explorer-editor/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/openapi.yaml, contracts/ui-routes.md
├── checklists/requirements.md
└── tasks.md                     # /speckit-tasks
```

### Source code (additions to the existing layout)

```text
src/backend/
├── DevSync.Domain/
│   ├── Files/                    ProjectLimits, ItemKind, ItemName, SupportedLanguages, WorkspaceItem,
│   │                             ProjectTree, FileContent, FileContentRules, FileErrors
│   └── Workspaces/               WorkspaceOperation (+EditFiles, DeleteFiles), WorkspacePermissions extended
├── DevSync.Application/
│   ├── Common/                   ErrorCodes (+14 codes)
│   └── Files/                    IProjectFileRepository; GetTree, CreateItem, RenameItem, MoveItem, DeleteItem,
│                                 GetFileContent, SaveFileContent handlers; FileDtos
├── DevSync.Infrastructure/
│   ├── Persistence/              WorkspaceItemConfiguration, FileContentConfiguration, DbSets; migration ProjectFiles
│   └── Files/                    ProjectFileRepository (workspace lock + tree load; CAS UPDATE via ExecuteUpdate)
└── DevSync.Api/
    ├── Endpoints/                FileEndpoints (tree, items, rename, move, delete, content GET/PUT)
    ├── Files/                    ContentBody (bounded raw-body reader, If-Match/ETag helpers)
    └── Errors/                   ErrorCatalog (+14 entries), Problems (+extensions overload)

tests/backend/
├── DevSync.UnitTests/            Domain/Files: ItemName, SupportedLanguages, ProjectTree, FileContentRules;
│                                 WorkspacePermissions (new ops); Application/Files handlers with fakes
└── DevSync.IntegrationTests/     Files/: TreeTests, ItemCrudTests, MoveTests, ContentTests, ConcurrencyTests,
                                  LimitsTests; Authorization/RoleMatrixTests extended; Security/FileLogScanTests;
                                  Persistence/ProjectFilesMigrationTests

src/frontend/src/
├── features/files/               FilesPage, FileExplorer, TreeNode, MoveDialog, EditorTabs, CodeEditor, SaveStatus,
│                                 ConflictBar, DraftBar, filesApi, editorSlice, autosave, drafts, tabStorage,
│                                 fileRules, monacoSetup
├── features/workspaces/          WorkspaceDetailsPage (+ "Open files" link)
├── app/                          router (+ files route), store (+ editorSlice, listeners for signedOut/permissionDenied)
└── shared/api/                   types (+ file types)
tests/frontend/features/files/    explorer, tabs, autosave (fake timers), saveStatus, conflict, drafts, roles, filesApi
nginx.conf                        client_max_body_size 2m on /api/
```

**Structure decision**: a new `Files` module folder in each backend layer, mirroring
`Workspaces` and `Auth`, with one new port. That port isolates the row lock and the CAS SQL,
which can't live in pure application code (Principle II). No new projects.

## Test Strategy

| Layer | Covers |
|-------|--------|
| **Unit: Domain** | `ItemName`: every FR-005 rule at its boundary (0/1/255/256 chars, each forbidden char, leading/trailing space, trailing dot, `.`/`..`, Unicode letters allowed). `SupportedLanguages`: 11 extensions in mixed case, unsupported and no extension, dotfiles. `ProjectTree`: create/rename/move/delete happy paths; duplicate ignoring case; case-only self-rename allowed; move into self or descendant; move to the same parent is a no-op; depth 20 vs 21 (create and move with subtree height); path 1,024 vs 1,025 (including descendants on rename/move); item 5,000 vs 5,001; delete returns the whole subtree. `FileContentRules`: 1,048,576 vs 1,048,577 bytes; multibyte chars at the boundary; invalid UTF-8; lone surrogate; NUL; BOM and CRLF preserved. `WorkspacePermissions`: full matrix including the new operations |
| **Unit: Application** | Handlers with a fake repository: outcome mapping (saved / conflict with current revision / not found); create file also creates content at revision 1; delete authorization isn't the handler's job (it's the filter's) |
| **Integration: Files API** | CRUD round-trips; tree shape and `limits`; byte-exact content round-trip (CRLF, BOM, emoji, 4-byte chars); every error code and status in the contract; 412/415/428; 413 at the boundary; `ETag` and `no-store` headers; cross-workspace item id → 404; malformed ids → 404; folder id on content → 404 |
| **Integration: Concurrency** | 10 parallel saves from the same revision → exactly one 200; concurrent A→B / B→A moves → one 400, no cycle; concurrent creates with the same name → one 201, one 409; save racing delete → 200-then-gone or 404, never a 500; tree invariants re-checked after each race |
| **Integration: Authorization** | Role matrix extended with the 7 new endpoints × {Owner, Editor, Viewer, non-member, anonymous}, including Editor DELETE → 403; downgrade takes effect on the next save |
| **Integration: Persistence** | `ProjectFiles` migration on a 002 database keeps all 002 data; indexes and CHECKs present; deleting a workspace or folder cascades; the DB rejects a `size_bytes` mismatch |
| **Integration: Security** | Log scan: a marker string written as content and as a file name never appears in captured logs |
| **Integration: Regression** | All 001/002 tests still pass (SC-007) |
| **Component (FE)** | Explorer: sorting, nesting, keyboard navigation, role-based actions (Viewer none, Editor no delete), inline create/rename validation and server-error mapping, move dialog disables self and descendants, delete confirmation count. Tabs: dedupe, limit, disambiguation, restore from storage, drop missing. Autosave (fake timers): no save before 1 s; one save after a pause; at most 7 saves in 60 s of typing; never two in flight; no save when unchanged; flush on switch/close/hidden; retry backoff. Status: every state and reason. Conflict bar: reload / overwrite (uses `currentRevision`) / copy. Drafts: written while dirty, offered on reopen only when different, restore leads to 412 when stale, discard clears, cleared on `signedOut` but not on `sessionEnded`, other users' drafts ignored, quota errors tolerated. 403 → read-only; 404 → deleted state |
| **Manual / end-to-end** | quickstart.md F1–F16 (real Monaco in a browser) and P1–P17 (curl through nginx) |

## Local development and verification

- **Compose**: `docker compose up --build -d`, then follow
  [quickstart.md](./quickstart.md). The migration runs on start; no new variables.
- **Native**: the API as in 002 (`dotnet run --project src/backend/DevSync.Api`); the frontend
  with `npm run dev --prefix src/frontend`. Monaco is served by Vite from `node_modules`, with no
  network needed.
- **Tests**: `dotnet test --solution src/backend/DevSync.sln` (Docker running for
  Testcontainers), then `npm test`, `npm run typecheck`, `npm run lint` and `npm run build` in
  `src/frontend`.
- **Evidence**: `specs/003-file-explorer-editor/verification.md`, as in 002.

## Trade-offs

| Choice | Gain | Cost / accepted risk |
|--------|------|----------------------|
| Adjacency list + in-memory validation under a lock | Simple, exhaustive, unit-testable rules; atomic moves | Each structure change loads ≤ 5,000 small rows; structure changes per workspace are serialized |
| Raw-text content API with `If-Match` | Byte-exact storage, exact size limit, standard conditional semantics | Two content types in the API (JSON and text); `fetchBaseQuery` needs a text response handler for that endpoint |
| Explicit revision, not `xmin` | Clear contract, portable | One extra column |
| Tokenizers only, no Monaco language workers | Small bundle; no misleading cross-file errors; stays out of the "language servers" scope | No JSON or CSS validation or IntelliSense in the editor |
| No polling; refetch on focus | No background load; revisions still guarantee correctness | Another person's rename or delete appears only on focus, a mutation or the next save |
| `localStorage` drafts | Reliable in `pagehide`; survives a reload | ~5 MB quota per origin, so large drafts may not fit (skipped silently); residual shared-computer risk after expiry |
| Permanent delete, Owner only | Matches the clarified spec; simple | No undo; mitigated by a confirmation that shows the count |
| Whole tree in one response | One request; the client can sort, filter and count | Up to ~5,000 rows per load; acceptable at this cap, but a later lazy-loading feature could change this |

## Phased implementation

1. **Domain**: limits, `ItemName`, `SupportedLanguages`, `ProjectTree`, `FileContentRules` and
   the permission operations, with unit tests.
2. **Persistence**: entity configurations, the `ProjectFiles` migration, the repository
   (workspace lock and CAS), and the migration test.
3. **Backend API**: handlers, `FileEndpoints`, the raw-body reader and `If-Match`, error codes,
   and the nginx body size. Integration tests: CRUD, content, limits, concurrency, role matrix,
   log scan. Run the 001/002 regression.
4. **Frontend foundation**: Monaco packages and `monacoSetup`, `filesApi`, `editorSlice`, the
   route and the "Open files" link.
5. **Explorer**: tree, create, rename, move and delete UI, with role-aware actions.
6. **Editor and tabs**: `CodeEditor`, tabs, and persistence across refresh.
7. **Autosave and conflicts**: `AutosaveController`, save status, conflict bar, drafts and
   `pagehide`.
8. **Docs and verification**: README, ADR 0003 "Project file storage and concurrency", the
   architecture overview, `.env.example` (no changes expected), a quickstart run, and
   `verification.md`.

## Complexity Tracking

No constitution violations. The one new port (`IProjectFileRepository`) is justified in the
structure decision.
