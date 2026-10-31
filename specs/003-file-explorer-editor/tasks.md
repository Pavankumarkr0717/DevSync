---

description: "Task list for feature 003-file-explorer-editor"
---

# Tasks: Project Files and Code Editor

**Input**: Design documents from `specs/003-file-explorer-editor/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/openapi.yaml](./contracts/openapi.yaml),
[contracts/ui-routes.md](./contracts/ui-routes.md), [quickstart.md](./quickstart.md)

**Tests**: Required (spec SC-002–SC-009, NFR-002, NFR-003, constitution III and V). In each
story, write the tests first and confirm they fail.

**Organization**: Phases follow the spec's user stories. US5 (P1) comes before US4 (P2), as
the spec prioritizes. US4 adds its own endpoints to the role matrix. Section headings carry the
requested **Group 1–10** labels:

| Group | Where |
|-------|-------|
| 1 Database entities & migrations | Phase 2 |
| 2 Backend file & folder APIs | Phase 2 (core), Phases 3, 4, 7 |
| 3 Input validation & path security | Phase 2 (domain rules), Phase 3 (API and path-security tests), Phase 7 |
| 4 File explorer UI | Phases 3, 7 |
| 5 Monaco Editor integration | Phase 1, Phases 4, 8 |
| 6 Redux Toolkit state management | Phase 2 (`filesApi`, `editorSlice`), Phases 6, 8 |
| 7 Autosave & save-state management | Phase 4 |
| 8 Optimistic concurrency & conflict handling | Phase 5, Phase 7 (structure races) |
| 9 Authorization & regression tests | Phase 6, Phase 7 (T067), Phase 9 |
| 10 Documentation & end-to-end verification | Phase 9 |

Every task ends with **Done when:** (its completion condition), the requirement ids it serves,
and **Deps:** (the tasks it needs first). "—" means no dependency beyond the phase's
prerequisites.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel with the other [P] tasks in the same group: different files, and
  their Deps are already done.
- **[Story]**: US1 build tree · US2 edit & autosave · US3 conflicts · US4 organize · US5 roles ·
  US6 tabs.

## Path Conventions

Per [ADR 0001](../../docs/adr/0001-repository-layout.md): `src/backend/DevSync.*`,
`tests/backend/DevSync.*Tests`, `src/frontend/src`, `tests/frontend`.

## Scope guard

Do not add:
- real-time or collaborative editing, live cursors, presence, polling of content, or merging;
- version history, trash or undo after delete;
- Git, code execution, terminals, build, or language services (no Monaco TS/JSON/CSS/HTML
  workers);
- search across files, upload or download, binary files, or copy/duplicate;
- cloud or object storage, Redis, or RabbitMQ.

Files are database records addressed by id. No task reads or writes the host filesystem for
project files.

---

## Phase 1: Setup

- [X] T001 Add `"monaco-editor": "0.57.0"` and `"@monaco-editor/react": "4.7.0"` (exact versions, like the existing pins) to `dependencies` in `src/frontend/package.json`, then run `npm install --prefix src/frontend`. **Done when:** `package-lock.json` is updated, and `npm run typecheck`, `npm run build` and `npm test` in `src/frontend` still pass. (plan Technical Context; research R8) **Deps:** —
- [X] T002 [P] In `src/frontend/nginx.conf`, add `client_max_body_size 2m;` inside `location /api/`, with a comment explaining that the default of 1 MiB would block a file at the 1 MiB limit before the API sees it. **Done when:** `docker compose up --build -d web` starts, and `docker compose exec web nginx -t` passes. (FR-016; research R13) **Deps:** —

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ No user-story work can begin until this phase is complete.**

### Group 3: Domain validation rules (tests first)

- [X] T003 [P] Write `tests/backend/DevSync.UnitTests/Domain/Files/ItemNameTests.cs` for `ItemName.Create`.
  - Accepted: lengths 1 and 255; Unicode letters (`résumé.md`); dotfiles (`.eslintrc.json`).
  - Rejected with `invalid_name`, each as its own case:
    - null, empty, and 256 chars;
    - each of `/ \ < > : " | ? *`;
    - U+0000, U+001F and U+007F;
    - a leading space and a trailing space;
    - a trailing `.`;
    - `.` and `..`.
  - Path-traversal attempts, all rejected: `../etc`, `..\\x`, `a/b`, `%2e%2e` taken literally only when it is otherwise valid, and `con:`.
  - Input is not trimmed (`" a.ts"` is rejected, not fixed).
  - `Normalized` equals `ToUpperInvariant()`.
  - `Extension` returns the text after the last dot (`a.b.TS` → `TS`) and none for `Makefile`.

  **Done when:** the tests compile against stubs and fail. (FR-005, FR-028; data-model ItemName) **Deps:** —
- [X] T004 [P] Write `tests/backend/DevSync.UnitTests/Domain/Files/SupportedLanguagesTests.cs`:
  - each of the 11 extensions maps to its language (`cs`→csharp, `js`/`jsx`→javascript, `ts`/`tsx`→typescript, `json`, `html`, `css`, `py`→python, `java`, `md`→markdown), in lower, upper and mixed case;
  - `.txt`, `.yml`, `Dockerfile`, `README` (no extension) and `a.` return none.

  **Done when:** the tests compile and fail. (FR-010, FR-011) **Deps:** —
- [X] T005 [P] Write `tests/backend/DevSync.UnitTests/Domain/Files/FileContentRulesTests.cs`, the **large file rejection** and content-rule cases:
  - empty → ok;
  - exactly 1,048,576 bytes → ok;
  - 1,048,577 bytes → `content_too_large`;
  - a 4-byte UTF-8 character straddling the limit → measured in bytes, not chars;
  - invalid UTF-8 (`0xFF`, truncated sequence, encoded lone surrogate `ED A0 80`) → `invalid_content`;
  - an embedded U+0000 → `invalid_content`;
  - a leading BOM, `\r\n` and lone `\r` are returned unchanged.

  **Done when:** the tests compile and fail. (FR-016, FR-017) **Deps:** —
- [X] T006 [P] Write `tests/backend/DevSync.UnitTests/Domain/Files/ProjectTreeTests.cs`:
  - **nested folder creation** at the top level and to depth 20 (OK), with depth 21 → `too_deep`;
  - **duplicate names in the same folder** ignoring case → `name_taken`, while the same name in different folders and at the top level vs inside a folder is OK;
  - create under a file or a missing parent → `parent_not_found`; unsupported file extension; folders with any valid name;
  - **rename**: keeps id and children; case-only rename of itself is OK; a clash with another sibling → `name_taken`; rename making a descendant's path 1,025 chars → `path_too_long`;
  - **move**: a file; a folder with its subtree; to the top level; into itself or a descendant → `invalid_move`; to the current parent → no-op success; into a file → `parent_not_found`; a name clash in the target; depth(target)+1+height > 20 → `too_deep`; descendant path > 1,024 → `path_too_long`;
  - item 5,000 OK, 5,001 → `too_many_items`;
  - **delete** returns the item plus all descendant ids;
  - `PathOf` joins names with `/`;
  - after every operation, a helper asserts the invariants: no cycles, parents are folders, siblings unique ignoring case.

  **Done when:** the tests compile and fail. (FR-001–FR-008, FR-012–FR-015, FR-029) **Deps:** —
- [X] T007 [P] Extend `tests/backend/DevSync.UnitTests/Domain/WorkspacePermissionsTests.cs` with `EditFiles` (Owner ✓, Editor ✓, Viewer ✗) and `DeleteFiles` (Owner ✓, Editor ✗, Viewer ✗). Keep every existing case. **Done when:** the new cases fail, and the existing ones pass. (FR-024) **Deps:** —
- [X] T008 Create, in `src/backend/DevSync.Domain/Files/`:
  - `ProjectLimits.cs`: `MaxNameLength` 255, `MaxPathLength` 1024, `MaxDepth` 20, `MaxItemsPerWorkspace` 5000, `MaxContentBytes` 1048576;
  - `ItemKind.cs`: `Folder` and `File`;
  - `ItemName.cs`: the rules exactly as in data-model.md ItemName;
  - `SupportedLanguages.cs`;
  - `FileErrors.cs`: code constants matching ErrorCodes in T017.

  Use `Result`/`DomainError` from `Domain/Common`. **Done when:** T003 and T004 pass. (FR-005, FR-010, FR-011) **Deps:** T003, T004
- [X] T009 [P] Create `src/backend/DevSync.Domain/Files/FileContentRules.cs` with `Validate(ReadOnlySpan<byte>) → Result<string>`. Check the size first, then decode with `new UTF8Encoding(false, throwOnInvalidBytes: true)`, then reject U+0000. Never trim or normalize line endings. **Done when:** T005 passes. (FR-016, FR-017) **Deps:** T005
- [X] T010 Create `src/backend/DevSync.Domain/Files/WorkspaceItem.cs` and `FileContent.cs` as in data-model.md:
  - **WorkspaceItem**: `Id` (UUIDv7), `WorkspaceId`, `ParentId` (null = top level), `Kind` (immutable), `Name` and `NormalizedName`, `CreatedAt`, `UpdatedAt`, `CreatedByUserId`.
  - **FileContent**: `ItemId`, `Content`, `SizeBytes`, `Revision` ("starts at **1** on create (empty content), +1 per successful save"), `UpdatedAt`, `UpdatedByUserId`.

  Use private setters and factory methods, as in `Workspace`. **Done when:** the domain project builds with 0 warnings. (FR-003, FR-018) **Deps:** T008
- [X] T011 Create `src/backend/DevSync.Domain/Files/ProjectTree.cs`. It wraps one workspace's `IReadOnlyList<WorkspaceItem>`, with `Create`, `Rename`, `Move`, `Delete` and `PathOf`. Checks run in the order given in data-model.md's ProjectTree table and return the listed codes. It mutates the tracked entities in place, and `Create` returns the new item (plus a new `FileContent` for files). **Done when:** T006 passes. (FR-001–FR-008, FR-012–FR-015, FR-029) **Deps:** T006, T010
- [X] T012 [P] Add `EditFiles` and `DeleteFiles` to `src/backend/DevSync.Domain/Workspaces/WorkspaceOperation.cs`, with doc comments. Map them in `WorkspacePermissions.cs`: EditFiles → Owner or Editor; DeleteFiles → Owner. **Done when:** T007 passes. (FR-024) **Deps:** T007

### Group 1: Database entities and migrations

- [X] T013 [P] Create `src/backend/DevSync.Infrastructure/Persistence/Configurations/WorkspaceItemConfiguration.cs`, table `workspace_items`:
  - `kind` varchar(8), CHECK in (`folder`,`file`);
  - `name` varchar(255), CHECK `char_length(name) BETWEEN 1 AND 255`;
  - `normalized_name` varchar(255) NOT NULL;
  - FK `workspace_id` → workspaces ON DELETE CASCADE;
  - FK `parent_id` → workspace_items ON DELETE CASCADE;
  - FK `created_by_user_id` → users ON DELETE SET NULL;
  - unique index `ux_workspace_items_sibling_name` on (`workspace_id`, `parent_id`, `normalized_name`) with `AreNullsDistinct(false)`;
  - indexes `ix_workspace_items_parent_id` and `ix_workspace_items_created_by_user_id`.

  All names are explicit and snake_case. **Done when:** the model builds. (FR-001, FR-003, FR-004, FR-015; data-model `workspace_items`) **Deps:** T010
- [X] T014 [P] Create `src/backend/DevSync.Infrastructure/Persistence/Configurations/FileContentConfiguration.cs`, table `file_contents`:
  - PK `item_id`, FK → workspace_items ON DELETE CASCADE;
  - `content` text NOT NULL, default `''`;
  - `size_bytes` integer with CHECK `size_bytes BETWEEN 0 AND 1048576` and `size_bytes = octet_length(content)`;
  - `revision` bigint, CHECK `revision >= 1`;
  - `updated_at`;
  - FK `updated_by_user_id` → users ON DELETE SET NULL, with index `ix_file_contents_updated_by_user_id`.

  **Done when:** the model builds. (FR-016–FR-018; data-model `file_contents`) **Deps:** T010
- [X] T015 Add `DbSet<WorkspaceItem> WorkspaceItems` and `DbSet<FileContent> FileContents` to `src/backend/DevSync.Infrastructure/Persistence/DevSyncDbContext.cs`. Generate the migration `ProjectFiles` under `Persistence/Migrations/`. Check the generated SQL contains `NULLS NOT DISTINCT`, every CHECK above, and no PascalCase index names. **Done when:** `dotnet ef migrations script` for `ProjectFiles` shows exactly two new tables and changes nothing existing. (NFR-004; research R15) **Deps:** T013, T014
- [X] T016 Write `tests/backend/DevSync.IntegrationTests/Persistence/ProjectFilesMigrationTests.cs`:
  - migrating a database at `IdentityAndPermissions` that holds 002 data (accounts, workspace, members, invitation) keeps every row;
  - both tables, the indexes and the CHECKs exist (query `pg_indexes` / `pg_constraint`);
  - a raw insert of two top-level siblings named `A`/`a` fails;
  - an insert with a `size_bytes` that doesn't match the content fails;
  - deleting a workspace or a folder cascades to items and contents.

  **Done when:** it passes. (FR-004, FR-015, FR-038; research R15) **Deps:** T015

### Group 2: API and repository core

- [X] T017 Add 14 codes to `src/backend/DevSync.Application/Common/ErrorCodes.cs`:
  - 400: `invalid_name`, `unsupported_extension`, `too_deep`, `path_too_long`, `invalid_move`, `invalid_content`;
  - 404: `item_not_found`, `parent_not_found`;
  - 409: `name_taken`, `too_many_items`;
  - 412: `revision_conflict`;
  - 413: `content_too_large`;
  - 415: `unsupported_media_type`;
  - 428: `revision_required`.

  Add the matching entries (status + title from contracts/ui-routes.md) to `src/backend/DevSync.Api/Errors/ErrorCatalog.cs`. Add `Problems.Error(string code, IReadOnlyDictionary<string, object?> extra)` to `src/backend/DevSync.Api/Errors/Problems.cs`, merging `extra` into the extensions. **Done when:** the solution builds, and the existing error tests pass. (FR-028) **Deps:** T008
- [X] T018 Create `src/backend/DevSync.Application/Files/IProjectFileRepository.cs`:
  - `Task<ProjectTree> LoadTreeForUpdateAsync(Guid workspaceId, CancellationToken)`: inside a transaction the caller completes via `SaveChangesAsync`;
  - `Task<IReadOnlyList<ItemDto>> GetTreeAsync(Guid workspaceId, CancellationToken)`;
  - `Task<FileContentDto?> GetContentAsync(Guid workspaceId, Guid itemId, CancellationToken)`;
  - `Task<SaveContentResult> TrySaveContentAsync(Guid workspaceId, Guid itemId, long expectedRevision, string content, int sizeBytes, Guid userId, DateTimeOffset now, CancellationToken)`, returning `Saved(revision, updatedAt)`, `Conflict(currentRevision)` or `NotFound`;
  - `Add(WorkspaceItem, FileContent?)`, `Remove(WorkspaceItem)`, `SaveChangesAsync`.

  Also create `src/backend/DevSync.Application/Files/FileDtos.cs` (`ItemDto`, `TreeDto` with `Limits`, `FileContentDto`, `SaveResultDto`), shaped per contracts/openapi.yaml. Extend `tests/backend/DevSync.UnitTests/Application/Fakes.cs` with an in-memory `FakeProjectFileRepository`. **Done when:** the solution builds. (FR-001, FR-018, FR-019; plan Structure) **Deps:** T011, T017
- [X] T019 Create `src/backend/DevSync.Infrastructure/Files/ProjectFileRepository.cs` and register it in `src/backend/DevSync.Infrastructure/DependencyInjection.cs`.
  - **`LoadTreeForUpdateAsync`**: begins a transaction, runs `SELECT id FROM workspaces WHERE id = @w FOR UPDATE`, then loads all tracked items of the workspace.
  - **`GetTreeAsync`**: no-tracking projection, with no content.
  - **`GetContentAsync`**: joins on `workspace_id = @w AND id = @item AND kind = 'file'`.
  - **`TrySaveContentAsync`**: one `ExecuteUpdateAsync` with `WHERE item_id = @item AND revision = @expected AND item belongs to @w`. On 0 rows, a follow-up read returns `Conflict(current)` or `NotFound`.
  - A unique violation (SQLSTATE 23505) on `ux_workspace_items_sibling_name` maps to `name_taken`.
  - It never logs content or names.

  **Done when:** it builds, and T016 still passes. (FR-019, FR-027, FR-029, NFR-001; research R2, R4) **Deps:** T015, T018
- [X] T020 [P] Create `src/backend/DevSync.Api/Files/ContentBody.cs`:
  - **`ReadAsync(HttpRequest)`** checks the content type first: anything but `text/plain` (charset absent or `utf-8`) → `unsupported_media_type`. It then reads `BodyReader` up to `MaxContentBytes + 1` bytes; more → `content_too_large`, without buffering the rest. Finally it calls `FileContentRules.Validate`.
  - **`TryParseIfMatch`**: missing → `revision_required`; `*`, weak (`W/`) or non-numeric → `malformed_request`; otherwise the revision as a `long`.
  - **`ETag(long revision)`** returns `"<n>"`.

  **Done when:** it builds. (FR-016, FR-019; research R5, R13) **Deps:** T009, T017
- [X] T021 Create `src/backend/DevSync.Api/Endpoints/FileEndpoints.cs` with `MapFileEndpoints()`: a group `/api/workspaces/{workspaceId}` with `RequireAuthorization()`, and no routes yet. Call it from `src/backend/DevSync.Api/Program.cs` after `MapInvitationEndpoints()`. **Done when:** the API starts, and the 001/002 tests pass. (FR-024, FR-025) **Deps:** T019
- [X] T022 [P] Create `tests/backend/DevSync.IntegrationTests/Infrastructure/FileTestData.cs`, with extension methods over `HttpClient`:
  - `CreateItemAsync(ws, parentId, kind, name)`
  - `GetTreeAsync(ws)`
  - `GetContentAsync(ws, id)`, returning text and revision
  - `SaveContentAsync(ws, id, revision, text, contentType = "text/plain; charset=utf-8")`
  - `RenameAsync`, `MoveAsync`, `DeleteAsync`

  Also add `TestData.TeamWorkspaceAsync(factory)`, returning an owner, editor and viewer client plus a non-member client and the workspace id, and `AssertTreeInvariantsAsync(factory, ws)`, which loads rows via `CreateDbScope` and checks no cycles, unique siblings, and parents that are folders. **Done when:** it compiles. (NFR-002, NFR-003) **Deps:** T021

### Group 6: Frontend state foundation

- [X] T023 [P] Add `ItemKind`, `Item`, `Tree` (`items`, `limits`), `SaveResult`, and the 14 new `Problem['code']` values to `src/frontend/src/shared/api/types.ts`. **Done when:** `npm run typecheck` passes. (contracts/openapi.yaml) **Deps:** —
- [X] T024 Create `src/frontend/src/features/files/filesApi.ts` via `baseApi.injectEndpoints`, and add the `FileTree` and `FileContent` tag types in `src/frontend/src/shared/api/baseApi.ts`.
  - **`getTree(ws)`**: tag `FileTree:ws`, with `refetchOnFocus` and `refetchOnReconnect` (research R12).
  - **`getFileContent({ws,id})`**: `responseHandler: 'text'`, returning `{ text, revision }` parsed from `ETag`; tag `FileContent:id`.
  - **`createItem`, `renameItem`, `moveItem`, `deleteItem`**: invalidate `FileTree:ws`.
  - **`saveFileContent({ws,id,revision,text})`**: `PUT` with `body: text`, `Content-Type: text/plain; charset=utf-8` and `If-Match: "<revision>"`. On success it updates the `getFileContent` cache via `updateQueryData` (text + new revision) and never invalidates.

  **Done when:** typecheck passes. (FR-002, FR-017–FR-019; research R5, R9) **Deps:** T023
- [X] T025 Create `src/frontend/src/features/files/editorSlice.ts`, register it in `src/frontend/src/app/store.ts` as `editor`, and keep the state serializable:
  - `byWorkspace[ws]: { openTabs: string[], activeId: string | null, expanded: Record<string, boolean> }`;
  - `files[id]: { status: 'saved' | 'dirty' | 'saving' | 'failed', reason?: 'conflict' | 'deleted' | 'forbidden' | 'tooLarge' | 'invalidContent' | 'offline', baseRevision: number, currentRevision?: number }`.

  Add actions for open, close, activate, toggle-expanded and each status transition. The buffer text is **never** stored here. **Done when:** typecheck passes, and the existing store tests pass. (FR-022, FR-031, FR-032; research R9) **Deps:** T024
- [X] T026 [P] Create `tests/frontend/support/handlers/files.ts`: MSW handlers backed by an in-memory per-workspace tree and content store that implements the contract. It covers revisions and `If-Match`/412/428, the name rules via a shared helper, 413 above 1,048,576 bytes, and role-based 403 (a configurable role). It also offers helpers `seedTree(ws, spec)`, `setRole(role)`, `failNext(path, status | 'network')` and `bumpRevision(id)`, the last to simulate another person's save. **Done when:** a smoke test in `tests/frontend/features/files/filesApi.test.ts` exercises create → save → stale save 412. (contracts/openapi.yaml) **Deps:** T023, T024
- [X] T027 **Checkpoint**: run `dotnet test --solution src/backend/DevSync.sln`, plus `npm run typecheck`, `npm run lint` and `npm test` in `src/frontend`. **Done when:** everything is green, apart from the intentionally failing T003–T007 stubs, which now pass. (SC-007) **Deps:** T001–T026

---

## Phase 3: User Story 1 — Build a Project Tree (P1) 🎯 MVP · Groups 2, 3, 4

**Goal**: Owners and Editors create nested folders and files. Every member sees the same tree
after a reload.

**Independent Test**: In an empty workspace, create `src/`, `src/api/` and
`src/api/server.ts`, reload, and see the same tree (quickstart F1–F3, P1–P3, P10).

### Tests (write first)

- [X] T028 [P] [US1] Write `tests/backend/DevSync.IntegrationTests/Files/TreeTests.cs`:
  - an empty workspace returns `items: []` and `limits` with the exact values;
  - **nested folder creation**: `src` → `src/api` → `src/api/server.ts`, then `GET /tree` returns all three with correct `parentId`s, and a second client (fresh request) sees the same;
  - a new file has empty content and `ETag "1"`;
  - the tree contains no content field.

  **Done when:** it fails for the missing endpoints. (FR-001, FR-002, FR-012, US1-AS1–AS3, AS5) **Deps:** T022
- [X] T029 [P] [US1] Write `tests/backend/DevSync.IntegrationTests/Files/CreateItemTests.cs`:
  - **duplicate names in the same folder**, `Main.cs` then `main.CS` → 409 `name_taken`; the same name in another folder and at the top level → 201;
  - a file whose name clashes with a folder of the same name → 409;
  - unsupported extensions (`notes.txt`, `Dockerfile`) → 400 `unsupported_extension`;
  - `parentId` is a file, missing, or from another workspace → 404 `parent_not_found`;
  - a malformed `parentId`, missing `kind` or missing `name` → 400;
  - depth 21 → 400 `too_deep`; path 1,025 → 400 `path_too_long`;
  - the 5,001st item, seeded to 5,000 via `CreateDbScope` → 409 `too_many_items`;
  - **two parallel creates of the same name** → exactly one 201 and one 409, after which `AssertTreeInvariantsAsync` passes.

  **Done when:** it fails. (FR-004–FR-008, FR-010, FR-028, FR-029, US1-AS4) **Deps:** T022
- [X] T030 [P] [US1] Write `tests/backend/DevSync.IntegrationTests/Files/PathSecurityTests.cs`, covering **invalid path and path traversal attempts**:
  - creating items named `..`, `.`, `../etc`, `..\\etc`, `a/b`, `a\\b`, `con:`, `x?.ts`, `" x.ts"`, `"x.ts "`, `x.ts.`, and a name with U+0001 → 400 `invalid_name` each, with the tree unchanged;
  - a route item id that is URL-encoded traversal (`..%2F..`, `%2e%2e`), a malformed UUID, or a valid id of an item in **another workspace** where the caller is also a member → 404 `item_not_found`, with no data from the other workspace in the response;
  - an `EndpointDataSource` test asserting that no `/api/workspaces/{workspaceId}/…` route has a parameter other than `workspaceId` and `itemId`, so no path parameters exist.

  **Done when:** it fails where endpoints are missing, and the name cases fail. (FR-005, FR-027, FR-028; research R6) **Deps:** T022
- [X] T031 [P] [US1] Write `tests/backend/DevSync.UnitTests/Application/Files/CreateItemHandlerTests.cs` and `GetTreeHandlerTests.cs` against `FakeProjectFileRepository`: a create of a file adds a `FileContent` with revision 1, empty content and `updated_by = caller`; the returned DTO is correct; errors are passed through unchanged; the tree DTO includes `limits`. **Done when:** they fail. (FR-012, FR-018) **Deps:** T018
- [X] T032 [P] [US1] Write `tests/frontend/features/files/fileRules.test.ts`, mirroring T003/T004's main cases: forbidden characters, `.`/`..`, trailing dot/space, length, the extension allow-list ignoring case, and sibling duplicates ignoring case. **Done when:** it fails. (FR-005, FR-010) **Deps:** —
- [X] T033 [P] [US1] Write `tests/frontend/features/files/FileExplorer.test.tsx`:
  - renders a seeded nested tree with folders first, then case-insensitive name order;
  - expand/collapse by click and arrow keys, with `role="tree"` and `aria-expanded`;
  - an empty workspace shows "No files yet" with New file / New folder;
  - inline **nested folder creation** (`src` → `api` → `server.ts`) updates the tree;
  - client validation messages appear before submit;
  - server `name_taken`, `unsupported_extension`, `too_deep`, `path_too_long` and `too_many_items` show the ui-routes.md messages;
  - an item named `<img src=x onerror=alert(1)>.md` renders as literal text.

  **Done when:** it fails. (FR-002, FR-012, FR-030; US1-AS1–AS4) **Deps:** T026
- [X] T034 [P] [US1] Write `tests/frontend/features/files/FilesPage.test.tsx`:
  - `/workspaces/:id/files` shows the explorer while loading, then the tree;
  - a 404 shows "Workspace not found";
  - a tree error shows `ErrorMessage` with Retry;
  - signed out → redirect to `/login?returnTo=…`;
  - the details page has an **Open files** link for every role.

  **Done when:** it fails. (FR-030; ui-routes Routes) **Deps:** T026

### Backend: Group 2

- [X] T035 [US1] Create `src/backend/DevSync.Application/Files/GetTree.cs` (`GetTreeHandler`) and `src/backend/DevSync.Application/Files/CreateItem.cs`. `CreateItemHandler` calls `LoadTreeForUpdateAsync`, then `ProjectTree.Create`, then `Add`, then `SaveChangesAsync`, using `ICurrentUser` and `TimeProvider`. Register both in `src/backend/DevSync.Application/DependencyInjection.cs`. **Done when:** T031 passes. (FR-001, FR-002, FR-012, FR-029) **Deps:** T018, T031
- [X] T036 [US1] In `src/backend/DevSync.Api/Endpoints/FileEndpoints.cs`, map:
  - `GET /tree` → `.RequireWorkspace(WorkspaceOperation.Read)`;
  - `POST /items` → `.RequireWorkspace(WorkspaceOperation.EditFiles)`, with request `CreateItemRequest(Guid? ParentId, string? Kind, string? Name)`. `ParentId` is `[property: JsonRequired]`, so an absent property is `malformed_request`. The response is 201 with `Location`.

  Log only workspace and item ids. **Done when:** T028 and T029 pass, and T030's name cases pass. (FR-001–FR-012, FR-024, FR-028; NFR-001) **Deps:** T035, T021

### Frontend: Group 4

- [X] T037 [P] [US1] Create `src/frontend/src/features/files/fileRules.ts`: `validateName(name, kind, siblings, limits) → string | null` and `languageFor(name)`. Use the same rules and messages as the server. **Done when:** T032 passes. (FR-005, FR-010, FR-011) **Deps:** T032
- [X] T038 [US1] Create `src/frontend/src/features/files/FileExplorer.tsx` and `TreeNode.tsx`:
  - build nested nodes from the flat list and sort them;
  - keep expanded state in `editorSlice`;
  - support keyboard navigation per ui-routes.md;
  - add New file / New folder buttons at the top level and per folder, with an inline input validated by `fileRules`, calling `createItem`;
  - map server errors per ui-routes.md;
  - a new file opens as the active file;
  - never use `dangerouslySetInnerHTML`.

  **Done when:** T033 passes. (FR-002, FR-012, FR-030) **Deps:** T037, T025
- [X] T039 [US1] Create `src/frontend/src/features/files/FilesPage.tsx`. It loads `getWorkspace` (for `myRole` and the name) and `getTree`, uses the same not-found view as `WorkspaceDetailsPage`, and lays out the explorer plus an editor area (placeholder until US2). Add the route `workspaces/:workspaceId/files` inside the protected routes in `src/frontend/src/app/router.tsx`. Add an **Open files** link in `src/frontend/src/features/workspaces/WorkspaceDetailsPage.tsx`. **Done when:** T034 passes, and the existing `WorkspaceDetailsPage.test.tsx` passes. (FR-030; US1) **Deps:** T038

**Checkpoint**: US1 is demonstrable. Run quickstart F1–F3 and P1–P3, P10.

---

## Phase 4: User Story 2 — Open and Edit Files with Autosave (P1) · Groups 2, 5, 7

**Goal**: Open a file in Monaco, type, autosave after pauses, see the status, keep changes after
a reload, and recover unsaved edits from a draft.

**Independent Test**: Open `server.ts`, type, wait for "Saved", reload, and see the text
(quickstart F4, F5, F13–F15, P4–P9, P17).

### Tests (write first)

- [X] T040 [P] [US2] Write `tests/backend/DevSync.IntegrationTests/Files/ContentTests.cs`:
  - `GET` of a new file → 200, empty, `ETag: "1"`, `Cache-Control: no-store`, `text/plain; charset=utf-8`;
  - `PUT` with `If-Match: "1"` → 200 `{revision: 2, sizeBytes, updatedAt}` + `ETag: "2"`;
  - a byte-exact round-trip of `\r\n`, a lone `\r`, a leading BOM, emoji and a 4-byte CJK character;
  - **large file rejection**: exactly 1,048,576 bytes → 200, and 1,048,577 bytes → 413 `content_too_large` with the stored content unchanged;
  - a NUL byte or invalid UTF-8 → 400 `invalid_content`;
  - `Content-Type: application/json` → 415;
  - missing `If-Match` → 428 `revision_required`; `If-Match: *` or `W/"1"` → 400 `malformed_request`;
  - a folder id → 404 `item_not_found`.

  **Done when:** it fails. (FR-016–FR-019, FR-028; SC-002, SC-006) **Deps:** T022
- [X] T041 [P] [US2] Write `tests/backend/DevSync.IntegrationTests/Security/FileLogScanTests.cs`, using `CapturingLoggerProvider`: create a file named `secret-marker-NAME.ts`, save content containing `secret-marker-CONTENT`, read it, trigger a 412 and a 413, and then assert that neither marker appears in any captured log message or scope. **Done when:** it fails or passes for the right reason. (NFR-001; constitution VIII) **Deps:** T022
- [X] T042 [P] [US2] Write `tests/backend/DevSync.UnitTests/Application/Files/SaveFileContentHandlerTests.cs` and `GetFileContentHandlerTests.cs`. Map `Saved` → result DTO, `Conflict(n)` → `revision_conflict` carrying `currentRevision = n`, and `NotFound` → `item_not_found`. The size passed equals the UTF-8 byte count, and `updated_by` is the caller. **Done when:** they fail. (FR-018, FR-019) **Deps:** T018
- [X] T043 [P] [US2] Write `tests/frontend/features/files/autosave.test.ts`, with fake timers and a fake `save` function:
  - **debounce and max wait**: no save before 1 s idle; exactly one save after a 1 s pause; continuous typing (an edit every 200 ms) for 60 s → at most 7 saves and the first at ≤ 10 s; never two saves in flight.
  - **during an in-flight save**: edits made meanwhile are saved right after it resolves.
  - **no-op saves**: if the text equals the last saved text, nothing is sent and the status returns to `saved`.
  - **flush**: `flush()` saves immediately.
  - **failed autosave and retry**: a network error or 503 → `failed/offline`, then automatic retries at 2 s, 4 s and 8 s, capped at 30 s, ending `saved` once the fake succeeds; a new edit also triggers a retry.
  - **non-retried failures**: 413 → `failed/tooLarge` and 400 → `failed/invalidContent`, with no automatic retry until the next edit; 403 → `failed/forbidden` and stop.
  - **revision tracking**: a successful response sets `baseRevision` to the new revision.

  **Done when:** it fails. (FR-020–FR-022; SC-003; research R10) **Deps:** —
- [X] T044 [P] [US2] Write `tests/frontend/features/files/drafts.test.ts`:
  - `writeDraft` and `readDraft` are keyed `devsync:draft:<userId>:<itemId>`;
  - drafts are throttled to one write per 500 ms while dirty;
  - `pagehide` writes synchronously, and `keepalive` save is attempted only when the body is ≤ 65,536 bytes;
  - a draft is removed after a successful save of that text;
  - `clearUserDrafts(userId)` removes only that user's keys;
  - drafts older than 7 days are purged on init;
  - a `localStorage` that throws (quota or disabled) is tolerated with no exception escaping.

  **Done when:** it fails. (FR-035, FR-037; research R11) **Deps:** —
- [X] T045 [P] [US2] Write `tests/frontend/features/files/FileEditing.test.tsx`, using the CodeEditor stub (T049):
  - open `server.ts` → the content appears with language `typescript` passed to the editor;
  - typing → "Unsaved changes", then "Saving…", then "Saved" with one `PUT`;
  - re-rendering with a fresh store (simulated reload) shows the saved text;
  - `failNext('network')` → "Save failed: Couldn't reach the server — retrying", then Retry → "Saved";
  - pasting text over 1 MiB → "File is larger than 1 MiB", and the text is kept;
  - a draft that differs from the server → DraftBar with Restore/Discard; Restore marks the file dirty; Discard clears the draft;
  - after `signedOut`, the drafts are gone, while after `sessionEnded` they remain.

  **Done when:** it fails. (FR-011, FR-017, FR-020–FR-022, FR-035–FR-037; US2-AS1–AS8; SC-002, SC-009) **Deps:** T026

### Backend: Group 2

- [X] T046 [US2] Create `src/backend/DevSync.Application/Files/GetFileContent.cs` and `SaveFileContent.cs` (handlers), using `ICurrentUser` and `TimeProvider`, and register them. **Done when:** T042 passes. (FR-017–FR-019) **Deps:** T018, T042
- [X] T047 [US2] In `FileEndpoints.cs`, map:
  - `GET /files/{itemId}/content` (Read): a text response with `ETag` and `Cache-Control: no-store`;
  - `PUT /files/{itemId}/content` (EditFiles): `ContentBody.TryParseIfMatch` and `ContentBody.ReadAsync`, then the handler. It returns 200 with an `ETag` and `SaveResult`, or 412 via `Problems.Error(RevisionConflict, { currentRevision })`.

  A malformed `itemId` → 404 `item_not_found`. Log ids, sizes and revisions only. **Done when:** T040 and T041 pass. (FR-016–FR-019, FR-024, FR-028; NFR-001) **Deps:** T046, T020, T036

### Frontend: Groups 5 and 7

- [X] T048 [US2] Create `src/frontend/src/features/files/monacoSetup.ts`:
  - import `monaco-editor/esm/vs/editor/editor.api` and the `basic-languages/*/*.contribution` modules for csharp, javascript, typescript, html, css, python, java and markdown, plus a JSON tokenizer (Monaco's `basic-languages` has none, so register a minimal Monarch JSON tokenizer here);
  - set `self.MonacoEnvironment.getWorker` to the editor worker via `monaco-editor/esm/vs/editor/editor.worker?worker`;
  - call `loader.config({ monaco })` from `@monaco-editor/react`, so there is no CDN.

  Then create `src/frontend/src/features/files/CodeEditor.tsx`. It is the only module importing Monaco, and it is lazy-loaded via `React.lazy`. It takes `{ workspaceId, itemId, language, value, readOnly, onChange }` and uses `path = devsync://<ws>/<itemId>` so each file gets its own model, undo stack and view state. Add `data-testid="code-editor"`. **Done when:** `npm run build` emits a separate editor chunk, and `grep -r "cdn.jsdelivr" src/frontend/dist` finds nothing. (FR-011; research R8) **Deps:** T001
- [X] T049 [P] [US2] Create `tests/frontend/support/codeEditorStub.tsx`: a `<textarea data-testid="code-editor">` that honours the `value`, `readOnly`, `language` (as `data-language`) and `onChange` props, and keeps one value per `itemId`, emulating per-file models. Register `vi.mock('@/features/files/CodeEditor', …)` in `tests/frontend/support/setup.ts`. **Done when:** existing frontend tests still pass. (constitution V) **Deps:** T048
- [X] T050 [P] [US2] Create `src/frontend/src/features/files/autosave.ts` with `class AutosaveController`.
  - **Constructor**: `{ save: (text, baseRevision) => Promise<SaveOutcome>, onStatus, delayMs = 1000, maxWaitMs = 10000, retryDelaysMs = [2000, 4000, 8000], maxRetryDelayMs = 30000 }`.
  - **Methods**: `change(text)`, `flush()`, `pause()`, `resume()`, `setBase(revision, savedText)`, `dispose()`.
  - **Behaviour**: exactly the R10 algorithm. Saves are never aborted.

  **Done when:** T043 passes. (FR-020–FR-022; research R10) **Deps:** T043
- [X] T051 [P] [US2] Create `src/frontend/src/features/files/drafts.ts`: `writeDraft`, `readDraft`, `removeDraft`, `clearUserDrafts` and `purgeOldDrafts`, with every storage access in try/catch, plus a `usePageLeave(onLeave)` hook for `pagehide` and `visibilitychange → hidden`. Also add `sendKeepaliveSave(ws, id, revision, text, accessToken)`, which uses `fetch(…, { keepalive: true })` only when the UTF-8 size is ≤ 65,536 bytes. **Done when:** T044 passes. (FR-035–FR-037; research R11) **Deps:** T044
- [X] T052 [US2] Create `src/frontend/src/features/files/SaveStatus.tsx` and `DraftBar.tsx`. Wire a single active file into `FilesPage` (tabs come in US6):
  - opening a file loads `getFileContent` and creates an `AutosaveController` whose `save` dispatches `saveFileContent`;
  - `onChange` → `controller.change`, plus a throttled `writeDraft`;
  - switching files or leaving the route → `flush`;
  - `usePageLeave` → write the draft and send a keepalive save;
  - offer the draft on open when it differs.

  In `src/frontend/src/app/store.ts`, add a listener so that `signedOut` → `clearUserDrafts(userId)` (not on `sessionEnded`). **Done when:** T045 passes. (FR-020–FR-022, FR-035–FR-037; US2) **Deps:** T048–T051, T039, T047

**Checkpoint**: US1 + US2 make a usable editor (quickstart F4, F5, F13–F15; P4–P9, P17).

---

## Phase 5: User Story 3 — Never Lose Someone Else's Newer Work (P1) · Group 8

**Goal**: Concurrent saves conflict instead of overwriting, and the person chooses what happens.

**Independent Test**: Two Editors save the same file. The second gets a conflict, and the
first's content is still stored (quickstart F7, P6, P16).

### Tests (write first)

- [X] T053 [P] [US3] Write `tests/backend/DevSync.IntegrationTests/Files/ContentConcurrencyTests.cs`, covering **concurrent file updates**:
  - 10 parallel `PUT`s with `If-Match` set to the same revision and different bodies → exactly one 200 and nine 412, each with `currentRevision` equal to the winner's revision, and the stored content equals the winner's body;
  - a stale `If-Match` → 412, with content and revision unchanged;
  - the same user with two clients behaves the same (US3-AS4);
  - a rename and a move by another member, then a save at the old revision → 200, and the revision was not bumped by the rename or move (US3-AS5);
  - a delete then a save → 404 `item_not_found` (US3-AS6);
  - 20 iterations of a save racing a delete → every response is 200 or 404, never 500, and `AssertTreeInvariantsAsync` passes.

  **Done when:** it fails or exposes gaps. (FR-018, FR-019, FR-029; SC-004; NFR-003) **Deps:** T047
- [X] T054 [P] [US3] Write `tests/frontend/features/files/ConflictBar.test.tsx`:
  - after `bumpRevision`, typing → 412 → "Someone else saved a newer version", and autosave is paused, so further typing sends no `PUT`;
  - **Reload latest** refetches, shows the server text, and returns the status to `saved`;
  - **Overwrite with mine** sends a `PUT` with `If-Match` set to the 412's `currentRevision` and ends `saved`;
  - **Copy mine** writes the buffer to a mocked `navigator.clipboard`;
  - a 404 on save → "This file was deleted" with Copy mine, and autosave stops;
  - restoring a stale draft (`baseRevision` < server) → the next save gets 412, not an overwrite.

  **Done when:** it fails. (FR-019, FR-023, FR-036; US3-AS2, AS3, AS6) **Deps:** T026, T052

### Implementation

- [X] T055 [US3] Harden `ProjectFileRepository.TrySaveContentAsync` and the PUT endpoint for the races found by T053. A save on an item deleted between the `UPDATE` and the follow-up read returns `NotFound`; a `DbUpdateException` or FK violation during a concurrent delete maps to `item_not_found`, not 500. The 412 response also carries `ETag` set to the current revision. **Done when:** T053 passes 20 consecutive runs (`--filter` loop). (FR-019, FR-029) **Deps:** T053
- [X] T056 [US3] Create `src/frontend/src/features/files/ConflictBar.tsx` (`data-testid="conflict-bar"`) and wire it into `FilesPage`:
  - on 412, store `currentRevision` in `editorSlice` and call `controller.pause()`;
  - **Reload latest**: refetch `getFileContent`, replace the buffer, `setBase`, `resume`, and remove the draft;
  - **Overwrite with mine**: `setBase(currentRevision)`, `flush`, `resume`;
  - **Copy mine**: `navigator.clipboard.writeText`;
  - on 404, the deleted state with Copy mine.

  **Done when:** T054 passes. (FR-023; US3) **Deps:** T054, T050, T052

**Checkpoint**: quickstart F7, P6 and P16 pass.

---

## Phase 6: User Story 5 — Role-Appropriate Access to Files (P1) · Groups 6, 9

**Goal**: Viewers read only; Editors can't delete; non-members can't see the files exist. Each
of these is enforced on the server.

**Independent Test**: As a Viewer, every file-changing API call is refused, and the UI offers
no change actions (quickstart F11, F12, P13–P15).

### Tests (write first)

- [X] T057 [US5] Extend `tests/backend/DevSync.IntegrationTests/Authorization/RoleMatrixTests.cs`: add `Operation` values `GetTree`, `CreateItem`, `ReadContent` and `SaveContent`, with cases × {Owner, Editor, Viewer, NonMember, Anonymous} from the FR-024 table. Expected:
  - Read operations: 200/200/200/404/401;
  - write operations: 2xx/2xx/403/404/401.

  Each refused case asserts the tree and content are unchanged. This covers **Viewer write restrictions** through direct API calls. **Done when:** it passes. (FR-024–FR-026; NFR-002; SC-005) **Deps:** T047
- [X] T058 [P] [US5] Extend `tests/backend/DevSync.IntegrationTests/Authorization/PrivilegeEscalationTests.cs`:
  - an Editor downgraded to Viewer → their next `PUT` content and `POST` item → 403;
  - a member removed → 404 on the tree and content;
  - an access token with extra `role` claims doesn't let a Viewer save;
  - a Viewer's `PUT` with a valid `If-Match` → 403 *before* the revision check (no 412 leaks), with the content unchanged.

  **Done when:** it passes. (FR-025, FR-026; US5-AS2, AS5, AS6) **Deps:** T047
- [X] T059 [P] [US5] Write `tests/frontend/features/files/FilesRoles.test.tsx`:
  - **Viewer**: no New file/New folder/Rename/Move/Delete anywhere; the editor stub is `readOnly`, with a "Read only" label; typing doesn't change the value; and no `PUT` is sent;
  - **Editor**: create, rename and move are offered, but no Delete;
  - **Owner**: all actions;
  - a save → 403 → "You no longer have permission to edit"; the editor becomes read-only, the text stays and is copyable, and the workspace role is refetched;
  - the tree → 404 → "Workspace not found".

  **Done when:** it fails. (FR-024, FR-030; US5-AS1, AS4–AS6) **Deps:** T052

### Implementation

- [X] T060 [US5] Gate the UI by `myRole` from `getWorkspace`:
  - in `FileExplorer.tsx`, hide create/rename/move for Viewers and delete for non-Owners;
  - in `CodeEditor`, use `readOnly` for Viewers and on `failed/forbidden`, and show a "Read only" label in `FilesPage.tsx`.

  In `src/frontend/src/app/store.ts`, extend the `permissionDenied` listener to also invalidate `FileTree`. **Done when:** T059 passes, along with all existing frontend tests. (FR-030; US5) **Deps:** T059

**Checkpoint**: quickstart F11, F12 and P13–P15 pass.

---

## Phase 7: User Story 4 — Organize: Rename, Move and Delete (P2) · Groups 2, 3, 4, 8, 9

**Goal**: Editors rename and move; the Owner deletes, with a confirmation. The tree is never
left half-changed.

**Independent Test**: As an Editor, rename `server.ts`→`app.ts` and move `src/api` under `lib/`.
As the Owner, delete `lib/` (quickstart F8–F10, P11, P14).

### Tests (write first)

- [X] T061 [P] [US4] Write `tests/backend/DevSync.IntegrationTests/Files/RenameMoveTests.cs`.
  - **Rename**:
    - keeps the id, content, revision and children;
    - `.ts`→`.py` is OK; `.ts`→`.txt` → 400 `unsupported_extension`;
    - a case-only rename (`app.ts`→`App.ts`) is OK;
    - a clash with another sibling in any case → 409 `name_taken`;
    - renaming an ancestor so a descendant's path exceeds 1,024 → 400 `path_too_long`;
    - invalid and traversal names → 400 `invalid_name`.
  - **Move**:
    - a file to another folder; a folder with a 3-level subtree; to the top level (`parentId: null`);
    - into itself or a descendant → 400 `invalid_move`;
    - to the current parent → 200 with no change;
    - into a file, a missing id, or another workspace's folder → 404 `parent_not_found`;
    - a name clash in the target → 409;
    - depth overflow caused by the subtree's height → 400 `too_deep`;
    - a body without the `parentId` property → 400 `malformed_request`;
    - a missing item → 404 `item_not_found`.

  **Done when:** it fails. (FR-003, FR-013, FR-014, FR-028; US4-AS1–AS5) **Deps:** T022
- [X] T062 [P] [US4] Write `tests/backend/DevSync.IntegrationTests/Files/DeleteItemTests.cs`, covering **file deletion**:
  - the Owner deletes a file → 204, gone from the tree, and its `file_contents` row is gone;
  - the Owner deletes a folder with a nested subtree of 12 items → every descendant item and content row is removed (checked in the database);
  - deleting a missing or another workspace's id → 404;
  - a second delete of the same id → 404;
  - **an Editor's DELETE → 403** with nothing removed.

  **Done when:** it fails. (FR-015, FR-024; US4-AS6, AS7) **Deps:** T022
- [X] T063 [P] [US4] Write `tests/backend/DevSync.IntegrationTests/Files/StructureConcurrencyTests.cs`:
  - parallel moves of A→into B and B→into A → exactly one success and one 400 `invalid_move`, with no cycle;
  - a parallel rename and delete of the same item → each request fully applies or is refused with 404;
  - parallel creates that would push the count past 5,000 → never more than 5,000 rows;
  - `AssertTreeInvariantsAsync` after each race, repeated 10 times.

  **Done when:** it fails or exposes gaps. (FR-008, FR-029; US4-AS8; NFR-003) **Deps:** T022
- [X] T064 [P] [US4] Write `tests/backend/DevSync.UnitTests/Application/Files/RenameMoveDeleteHandlerTests.cs` against the fake repository: error passthrough, `UpdatedAt` set on rename and move, and delete removing the root item only (the database cascades the rest). **Done when:** it fails. (FR-013–FR-015) **Deps:** T018
- [X] T065 [P] [US4] Write `tests/frontend/features/files/OrganizeItems.test.tsx`:
  - inline rename via the menu and F2, with server errors mapped;
  - the Move dialog lists folders plus "Top level", with the item and its descendants disabled, and moving updates the tree;
  - the Owner's delete confirmation reads "Delete *src* and 12 items inside? This can't be undone." and on confirm the item disappears and the open file is closed;
  - Cancel does nothing;
  - an Editor sees no Delete;
  - a 404 on any action → toast plus a tree refetch.

  **Done when:** it fails. (FR-013–FR-015, FR-030, FR-034; US4) **Deps:** T026

### Backend

- [X] T066 [US4] Create `src/backend/DevSync.Application/Files/RenameItem.cs`, `MoveItem.cs` and `DeleteItem.cs` (handlers using `LoadTreeForUpdateAsync` and `ProjectTree`), and register them. **Done when:** T064 passes. (FR-013–FR-015, FR-029) **Deps:** T064, T018
- [X] T067 [US4] In `FileEndpoints.cs`, map:
  - `POST /items/{itemId}/rename` (EditFiles), body `{ name }`;
  - `POST /items/{itemId}/move` (EditFiles), body `MoveItemRequest([property: JsonRequired] Guid? ParentId)`;
  - `DELETE /items/{itemId}` (**DeleteFiles**) → 204.

  Extend `RoleMatrixTests` with `RenameItem`, `MoveItem` and `DeleteItem` (Editor DELETE → 403). **Done when:** T061, T062 and T063 pass, and the extended matrix passes. (FR-013–FR-015, FR-024; NFR-002; SC-005) **Deps:** T066, T057

### Frontend: Group 4

- [X] T068 [P] [US4] Create `src/frontend/src/features/files/MoveDialog.tsx`: an accessible dialog with a folder picker built from the tree, "Top level" as the first option, the item and its descendants disabled, and a server error shown inline. **Done when:** the Move cases of T065 pass. (FR-014, FR-034) **Deps:** T065
- [X] T069 [US4] Add Rename (inline, F2), Move to… (dialog) and Delete (Owner only) to the item menu in `FileExplorer.tsx` / `TreeNode.tsx`. Delete uses a confirmation with the subtree count computed from the tree. After a delete, dispose the autosave controllers and editor state for the removed ids. **Done when:** T065 passes. (FR-013–FR-015, FR-030) **Deps:** T068, T060

**Checkpoint**: quickstart F8–F10, P11 and P14 pass.

---

## Phase 8: User Story 6 — Work Across Several Open Files (P2) · Groups 5, 6

**Goal**: Up to 20 tabs, each with its own buffer, cursor and status, restored after a refresh.

**Independent Test**: Open three files, edit two, switch between them, refresh, and see the same
tabs (quickstart F5, F6).

### Tests (write first)

- [X] T070 [P] [US6] Write `tests/frontend/features/files/tabStorage.test.ts`: the key is `devsync:tabs:<userId>:<workspaceId>`, with save/load of `{ itemIds, activeId }`; ids missing from the tree are dropped on load; `clearUserTabs(userId)`; storage that throws is tolerated. **Done when:** it fails. (FR-033) **Deps:** —
- [X] T071 [P] [US6] Write `tests/frontend/features/files/EditorTabs.test.tsx`:
  - opening an open file focuses its tab, with no duplicate;
  - the 21st file → toast "Close a tab to open another file";
  - two `index.ts` files show `index.ts — src/web` and `index.ts — src/api`;
  - switching tabs keeps each tab's unsaved text (stub per-file values) and flushes the one being left (one `PUT`);
  - tab markers `●` for dirty and `!` for failed;
  - closing a dirty tab flushes, and a close whose save fails keeps the tab with "Save failed";
  - after a re-render with a fresh store, the same tabs and active tab are restored, and a deleted id is dropped silently;
  - a rename by someone else (MSW tree change plus a focus event) updates the tab label;
  - `signedOut` clears the tab storage.

  **Done when:** it fails. (FR-031–FR-033; US6-AS1–AS5) **Deps:** T026, T052

### Implementation

- [X] T072 [P] [US6] Create `src/frontend/src/features/files/tabStorage.ts`. **Done when:** T070 passes. (FR-033) **Deps:** T070
- [X] T073 [US6] Add tab reducers to `editorSlice.ts`: open with the 20-tab limit, close, activate, and `restoreTabs` filtered by the tree. Create `src/frontend/src/features/files/EditorTabs.tsx` (`data-testid="editor-tabs"`, `role="tablist"`), with path disambiguation and status markers. **Done when:** the T071 cases for opening, the limit, disambiguation and markers pass. (FR-031) **Deps:** T072, T025
- [X] T074 [US6] Make `FilesPage` multi-tab:
  - one `AutosaveController` per open tab, held in a ref map and disposed on close;
  - flush on tab switch and close; the active tab's `CodeEditor` uses per-file `path`, so Monaco keeps each model's undo stack, cursor and scroll;
  - persist the tab list with `tabStorage` on change, and restore it on mount after the tree loads;
  - the `signedOut` listener also calls `clearUserTabs`;
  - the T069 delete cleanup closes tabs for removed ids.

  **Done when:** T071 passes, and T045, T054, T059 and T065 still pass. (FR-021, FR-031–FR-033) **Deps:** T073, T056, T069

**Checkpoint**: quickstart F5 and F6 pass with several tabs.

---

## Phase 9: Polish — Regression, Documentation and End-to-End · Groups 9, 10

### Group 9: Regression and security

- [X] T075 Run the full suites: `dotnet build src/backend/DevSync.sln --no-incremental` (0 warnings), then `dotnet test --solution src/backend/DevSync.sln`, then `npm run typecheck`, `npm run lint`, `npm test` and `npm run build` in `src/frontend`. Fix regressions without weakening any 001/002 test. **Done when:** everything passes, and the counts are recorded for verification.md. (FR-038; SC-007) **Deps:** T001–T074
- [X] T076 [P] Add `src/frontend/scripts/check-bundle.mjs` and the npm script `check:bundle`. After `npm run build`, it fails if any file in `dist/` contains `cdn.jsdelivr.net`, `unpkg.com` or `cdnjs`, or if Monaco code is in the entry chunk instead of a lazy chunk. Call it from the `frontend` job in `.github/workflows/ci.yml`. **Done when:** it passes locally, and CI config validates. (research R8; threat "Third-party script loading") **Deps:** T048

### Group 10: Documentation and end-to-end verification

- [X] T077 [P] Write `docs/adr/0003-project-file-storage.md`. Cover:
  - the decision: database records with an adjacency list, content in `file_contents`, revision CAS over `If-Match`, a per-workspace lock for structure, Monaco with tokenizers only;
  - the rejected alternatives: materialized path, `xmin`, JSON content, host filesystem, Monaco language workers;
  - the consequences: no real-time sync, no history, the trade-offs from plan.md.

  Link it from `docs/adr/README.md`. **Done when:** the file exists and is linked. (constitution XI; plan Trade-offs) **Deps:** —
- [X] T078 [P] Update `README.md` with an S03 section: what was delivered, the limits (1 MiB, 5,000 items, depth 20, path 1,024, 20 tabs), the supported extensions, autosave timing, how conflicts look, drafts and their shared-computer risk (R11), the 64 KiB `keepalive` limit, and that no new environment variables are needed. Update `docs/architecture/overview.md` with the `Files` module and an "Editing and saving" flow diagram. **Done when:** both are updated and link to this spec. (constitution X) **Deps:** —
- [X] T079 [P] Create `specs/003-file-explorer-editor/checklists/traceability.md`, mapping every US acceptance scenario, FR, NFR and SC to the tests that verify it, in the format of 002's traceability file. **Done when:** every FR-001–FR-038, NFR-001–NFR-004 and SC-001–SC-009 has at least one test or an explicit "manual" entry. (constitution V) **Deps:** T075
- [X] T080 End-to-end against Docker Compose (`docker compose up --build -d` over the existing 002 volume):
  - confirm the `ProjectFiles` migration is applied and the 002 data kept;
  - script quickstart P1–P17 with curl through `http://localhost:8080`, including P8 through nginx and P16's 10 parallel saves;
  - run browser scenarios F1–F16 in a real browser;
  - write `specs/003-file-explorer-editor/verification.md`: environment, suite counts from T075, per-scenario results, defects found and fixed, and anything not verified, stated explicitly (e.g. Safari/Firefox `keepalive`).

  **Done when:** verification.md is complete, and every failure is fixed or recorded with a reason. (SC-001–SC-009; quickstart) **Deps:** T075, T002
- [X] T081 Mark completed tasks `[X]` in this file and add an "Implementation notes" section listing any deviations from task text, as in 002. **Done when:** every task is `[X]` or explained. (Spec Kit workflow) **Deps:** T080

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** → **Foundational (Phase 2)** → stories.
- Phase 2 internal order:
  - **Domain**: tests T003–T007 first, then T008 → T010 → T011; T009 and T012 in parallel.
  - **Persistence**: T013 and T014 [P] → T015 → T016.
  - **API core**: T017 → T018 → T019 → T021 → T022; T020 [P] after T009/T017.
  - **Frontend**: T023 → T024 → T025; T026 after T024.
  - **Checkpoint**: T027.
- **US1 (Phase 3)** needs Phase 2.
- **US2 (Phase 4)** needs US1 (files are opened from the explorer, and `FileEndpoints` is shared).
- **US3 (Phase 5)** needs US2 (the save path and `AutosaveController`).
- **US5 (Phase 6)** needs US2 for its backend matrix (T057/T058). Its frontend T059/T060 needs
  T052.
- **US4 (Phase 7)** needs US1. T067 needs T057 (it extends the matrix), and T069 needs T060
  (role gating). Its backend tests T061–T064 can start right after Phase 2.
- **US6 (Phase 8)** needs US2, US3 and US4 (T074 wires the conflict and delete cleanup into
  tabs). T070, T072 and T073 can start after Phase 2.
- **Polish (Phase 9)** needs all stories. T080 runs after T075, and T081 is last.

### Shared files (sequential across tasks)

- `src/backend/DevSync.Api/Endpoints/FileEndpoints.cs`: T021 → T036 → T047 → T055 → T067
- `tests/backend/DevSync.IntegrationTests/Authorization/RoleMatrixTests.cs`: T057 → T067
- `src/backend/DevSync.Infrastructure/Files/ProjectFileRepository.cs`: T019 → T055
- `src/backend/DevSync.Application/DependencyInjection.cs`: T035 → T046 → T066
- `src/frontend/src/features/files/FilesPage.tsx`: T039 → T052 → T056 → T060 → T074
- `src/frontend/src/features/files/FileExplorer.tsx` / `TreeNode.tsx`: T038 → T060 → T069
- `src/frontend/src/features/files/editorSlice.ts`: T025 → T073
- `src/frontend/src/app/store.ts`: T025 → T052 → T060 → T074
- `tests/frontend/support/setup.ts`: T049

## Parallel Examples

```text
Phase 2: T003 + T004 + T005 + T006 + T007 (separate test files); T013 + T014; T009 + T012; T020 + T022; T023 + T026 (after T024)
US1:     T028 + T029 + T030 + T031 + T032 + T033 + T034 (all tests); T037 alongside T035
US2:     T040 + T041 + T042 + T043 + T044 + T045; T049 + T050 + T051 (after T048)
US3:     T053 + T054
US5:     T058 + T059 (after T057 starts the matrix)
US4:     T061 + T062 + T063 + T064 + T065; T068 alongside T066
US6:     T070 + T071; T072 alongside T073 prep
Polish:  T076 + T077 + T078; then T079 after T075
```

## Implementation Strategy

1. **Foundation** (Phase 2): the schema migrates over 002 data, the domain rules are proven by
   unit tests, the repository locks and does CAS, and the frontend state is in place. Stop and
   validate with T027.
2. **MVP** = US1 + US2 + US3 + US5 (all P1): create a tree, edit with autosave, conflicts never
   overwrite, and roles are enforced. Validate with quickstart F1–F5, F7, F11–F15 and P1–P10,
   P12–P17.
3. **Organization** = US4: rename, move, and Owner-only delete.
4. **Productivity** = US6: multiple tabs restored after a refresh.
5. **Polish**: regression, bundle check, ADR, docs, traceability, and end-to-end evidence.

## Requirement coverage

| Requirement | Tasks |
|-------------|-------|
| FR-001–FR-003 tree, listing, stable identity | T006, T010, T011, T013, T028, T035, T036, T038 |
| FR-004 unique names (ignoring case) | T006, T013, T016, T029, T061 |
| FR-005 name rules · FR-006 path length · FR-007 depth · FR-008 item limit | T003, T006, T008, T011, T029, T030, T061, T063 |
| FR-010/FR-011 extensions and highlighting | T004, T008, T029, T037, T045, T048 |
| FR-012–FR-015 create, rename, move, delete | T006, T011, T028, T029, T035, T036, T061, T062, T064–T069 |
| FR-016–FR-019 content limits, exact storage, revisions, conflicts | T005, T009, T014, T019, T020, T040, T042, T046, T047, T053, T055 |
| FR-020–FR-023 autosave, flush, status, conflict choice | T043, T045, T050, T052, T054, T056 |
| FR-024–FR-027 authorization, not-found, fresh role, scoping | T007, T012, T030, T036, T047, T057, T058, T059, T060, T067 |
| FR-028 distinct errors · FR-029 atomic structure | T017, T019, T029, T053, T055, T063 |
| FR-030–FR-034 web app: explorer, tabs, refresh, move UI | T033, T034, T038, T039, T060, T065, T068–T074 |
| FR-035–FR-037 drafts | T044, T045, T051, T052, T054 |
| FR-038 S01/S02 unchanged | T016, T027, T075 |
| NFR-001 no content in logs | T019, T036, T041, T047 |
| NFR-002 role matrix · NFR-003 concurrency/timing tests | T057, T067, T053, T063, T043 |
| NFR-004 no new infrastructure | T015, T077 |
| SC-001–SC-009 | T080 (end-to-end), plus the tests above per criterion; T079 traceability |

## Requested test coverage

| Requested test | Tasks |
|----------------|-------|
| Nested folder creation | T006, T028, T033 |
| Duplicate file names in the same directory | T006, T029 (including the parallel create race), T061 |
| Rename and move operations | T006, T061, T063, T064, T065 |
| File deletion | T006, T062, T065 |
| Large file rejection | T005, T040, T045, quickstart P8 in T080 |
| Invalid path and path traversal attempts | T003, T030, T061 |
| Concurrent file updates | T053, T063, T054 |
| Failed autosave and retry | T043, T045 |
| Viewer write restrictions | T057, T058, T059 |

## Implementation notes (2026-10-31)

All 81 tasks are complete. Evidence: [verification.md](./verification.md) (559 backend tests, 179
frontend tests, 22/22 end-to-end API checks through nginx) and
[checklists/traceability.md](./checklists/traceability.md). **The browser scenarios F1–F16 were
not run in a real browser**, because none was available in this session. See verification.md
"Not verified manually".

Deviations from the task text:

- **T001**: `monaco-editor` 0.57.0 pins `dompurify` 3.4.15, which has published XSS advisories.
  An npm `overrides` entry forces 3.4.17 (patch release). Recorded in ADR 0003.
- **T048**: Monaco 0.57 reorganized its modules:
  - the editor core is `monaco-editor/editor/editor.api`;
  - editor contributions are `features/register.all`;
  - tokenizers are `languages/definitions/<lang>/register`, and `basic-languages/*` is gone.

  There is no tokenizer-only JSON definition, so `monacoSetup.ts` registers a small Monarch
  JSON tokenizer.
- **T017**: `Outcome<T>` gained optional `Detail` and `Extra` (additive), and `Problems.Error`
  an overload taking a detail and extra members. Used for `invalid_name` messages and
  `currentRevision` on 412.
- **T018/T019**: the port exposes `BeginTreeEditAsync` → `ITreeEdit` (`Tree`, `Add`, `Remove`,
  `CommitAsync`, `DisposeAsync` = rollback) instead of `LoadTreeForUpdateAsync` plus
  `SaveChangesAsync`. That makes the lock and transaction lifetime explicit. Deletes run as one
  `ExecuteDelete` of the root, relying on the database cascade.
- **T020**: the `If-Match` helper is `ContentBody.ParseIfMatch`. It also rejects lists and
  `W/` tags as `malformed_request`.
- **T030**: `%2e%2e` in a route is resolved to `..` by the HTTP client before sending, so it
  matches no item route (404, or 405 on the workspace route). It is now a separate test,
  `Dot_segments_in_an_item_id_never_reach_another_route`.
- **T039–T052**: the editor area is split into `EditorPane` (active file), `FileSession` (one
  open file's autosave controller and draft) and `useFileSessions` (sessions kept while tabs
  are open). A basic `EditorTabs` was created in US1, because opening a file shows a tab.
  `saveOutcome.ts` maps HTTP errors to save failures.
- **T049**: the stand-in's per-file values live in `tests/frontend/support/editorModels.ts`,
  shared through `globalThis`, because the mocked module and the test setup's import can be
  separate instances.
- **T052**: `visibilitychange → hidden` does a normal flush plus a draft; only `pagehide` sends
  the `keepalive` save. A keepalive on a mere tab switch would bump the revision behind the
  controller's back.
- **T053**: rename, move and delete in the content-concurrency test are done with SQL, so US3
  does not depend on US4's endpoints.
- **T060**: role gating was built into the US1/US2 components as they were written. T060 added
  the `FileTree` invalidation on `permissionDenied`.
- **T069**: delete confirmation is its own `DeleteDialog` (`role="alertdialog"`).
- **T074**: `AutosaveController.settle()` (save now and wait) lets a tab close only once its
  save succeeded. Tab persistence is the `useTabPersistence` hook.
- **T076**: `check-bundle.mjs` allows exactly one CDN literal: `@monaco-editor/loader`'s default
  `paths.vs`. It is never used because `loader.config({ monaco })` supplies the bundled
  instance, and the script also checks that call is present.
- **Extra**:
  - `setupListeners` in `main.tsx`, so `refetchOnFocus` works;
  - `ErrorMessage` gained `onRetry`, and `ApiError` gained `detail`;
  - `WorkspaceNotFound` was extracted and reused;
  - `uiSlice.toastShown` and the `useAppStore` hook were added;
  - the existing schema test (`MigrationTests`) was extended with the new tables.

## Notes

- [P] tasks touch different files and depend only on completed tasks.
- Write each story's tests first and watch them fail.
- Never log file names or content; log ids, sizes and revisions only.
- Commit after each task or logical group, once the git repository has its first commit.
