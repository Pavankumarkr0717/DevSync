# Research: Project Files and Code Editor

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-10-31

Each decision gives the choice, why, and the alternatives that were rejected. The Technical
Context had no open unknowns after these decisions.

## R1. Tree storage: adjacency list in one `workspace_items` table

**Decision**: Store folders and files in a single `workspace_items` table, with a `kind` of
`folder` or `file` and a nullable `parent_id` (null = top level). Uniqueness comes from a unique
index on (`workspace_id`, `parent_id`, `normalized_name`) with `NULLS NOT DISTINCT`, so two
top-level items can't share a name either. `parent_id` references `workspace_items` with
`ON DELETE CASCADE`, so deleting a folder deletes its subtree in one statement.

**Rationale**:
- Rename and move change one row. An item's identity (its UUID) never changes (FR-003).
- Paths are never stored, so a stale path can't be trusted by mistake, and no row can contain
  `../` (user requirement: application-managed records, not host paths).
- Cascade delete is atomic and needs no application recursion.
- `NULLS NOT DISTINCT` is available in PostgreSQL 15+, and the project uses 18.

**Alternatives considered**:
- *Materialized path column* (`/src/api/server.ts`): fast subtree queries, but every move or
  folder rename must rewrite every descendant's path, and the path becomes a second source of
  truth that could drift. Rejected.
- *`ltree` / closure table*: more machinery for a tree capped at 5,000 items. Rejected
  (Principle IX).
- *Separate `folders` and `files` tables*: needs two parent FKs, or a polymorphic parent, and
  uniqueness can't span both tables with one index. Rejected.

## R2. Structure changes: lock the workspace, load the tree, validate in the domain

**Decision**: Create, rename, move and delete run in one transaction that:
1. Locks the workspace row: `SELECT … FROM workspaces WHERE id = @id FOR UPDATE`.
2. Loads all of the workspace's items (id, parent, kind, name; at most 5,000 small rows).
3. Lets a pure domain `ProjectTree` validate the change: names, duplicates, depth, path length,
   item count and cycles.
4. Applies the change and commits.

**Rationale**:
- Depth, cycle and path-length checks need ancestors and descendants. Checking them in memory is
  simple, exhaustive and unit-testable without a database (Principle V).
- The per-workspace lock serializes structure changes, so concurrent moves can't create a cycle
  (A into B while B into A), and the count and depth limits can't be overshot by a race
  (FR-029).
- Content saves don't take this lock (R4), so editing isn't slowed by structure changes.
- The unique index remains as a backstop. If it is ever hit, the change maps to `name_taken`.

**Alternatives considered**:
- *Recursive CTEs per check without a lock*: still racy for cycles and limits unless it runs
  `SERIALIZABLE` with retries. More SQL, less testable. Rejected.
- *`SERIALIZABLE` isolation + retry*: correct but adds retry plumbing. A row lock is simpler and
  enough for one database. Rejected.

## R3. Content storage: separate `file_contents` table, `text` column

**Decision**: Keep each file's content in `file_contents` (1:1 with a `file` item, cascade
delete), with `content text`, `size_bytes`, `revision`, `updated_at` and `updated_by_user_id`.

**Rationale**:
- Tree listing never reads content.
- Rename and move never touch the content row, so they don't change the revision (US3-AS5).
- PostgreSQL automatically compresses and stores large values out of line (TOAST).
- `text` can't hold NUL, which matches FR-016. The database's UTF-8 encoding enforces valid
  text.
- A check constraint caps `size_bytes` and ties it to `octet_length(content)`.

**Alternatives considered**:
- *Content columns on `workspace_items`*: bloats the tree query. Rejected.
- *`bytea`*: would accept any bytes, so text validity would rest on application code alone, and
  the content would be unreadable in database tools. Rejected.
- *Large objects or files on disk*: violates the user requirement and Principle VII's single
  durable store. Rejected.

## R4. Optimistic concurrency: explicit integer revision, compare-and-swap

**Decision**: Each file has `revision` (starting at 1, +1 on every content save). A save is one
statement:

```text
UPDATE file_contents SET content, size_bytes, revision = revision + 1, updated_at, updated_by
WHERE item_id = @item AND revision = @expected
  AND item belongs to @workspace
```

If it updates 0 rows, a follow-up read tells *conflict* (the file exists → return its current
revision) from *deleted* (it's gone → 404).

**Rationale**:
- The check and the write are one atomic statement, so the race can't be lost (FR-019, SC-004).
- The revision is meaningful to people ("revision 7") and easy to test.
- The revision is exposed as the HTTP `ETag` (R5).

**Alternatives considered**:
- *PostgreSQL `xmin` as the EF concurrency token*: no extra column, but opaque. It can change
  for reasons unrelated to content, and it couples the API contract to a storage detail.
  Rejected.
- *EF `[ConcurrencyCheck]` on load-modify-save*: two round trips and loading the 1 MiB content
  just to overwrite it. Rejected.
- *`updated_at` timestamps*: clock resolution and ties make them unsafe. Rejected.
- *Last write wins*: violates the spec. Rejected.

## R5. Content API: raw text body with `If-Match` / `ETag`

**Decision**:
- `GET …/files/{itemId}/content` returns the content as `text/plain; charset=utf-8` with
  `ETag: "<revision>"` and `Cache-Control: no-store`.
- `PUT` takes the raw text body and `If-Match: "<revision>"`. It answers:
  - **412** `revision_conflict` (Problem Details with `currentRevision`) on a mismatch;
  - **428** `revision_required` if `If-Match` is missing;
  - **415** for any other content type;
  - **413** `content_too_large` above 1 MiB.

**Rationale**:
- **Exact round-trip**: the stored text is exactly the bytes sent (FR-017). There is no JSON
  escaping, so line endings and BOMs pass through unchanged.
- **Size check**: the byte limit is checked directly against the request body. JSON escaping
  could inflate 1 MiB to about 6 MiB and make the limit fuzzy.
- **Standard semantics**: `If-Match`/`ETag`/412/428 are standard HTTP for conditional writes.
  Making `If-Match` required means no client can save without stating its base revision.
- **`no-store`**: keeps source code out of the browser's disk cache.

**Alternatives considered**:
- *JSON `{ content, baseRevision }`*: workable, but loses byte-exactness guarantees and makes
  the body limit approximate. Rejected.
- *409 for conflicts*: 409 is reserved here for structure conflicts (`name_taken`), and a failed
  precondition is properly 412. Rejected.

## R6. Validation rules and where they run

**Decision**: All rules are enforced in the domain, so the server can never be bypassed (FR-028).
The web app repeats the cheap ones for instant feedback.

| Rule | Domain type | Error code |
|------|-------------|------------|
| Name: 1–255 chars; no `/ \ < > : " \| ? *`, control chars (U+0000–U+001F, U+007F); no leading/trailing space; no trailing dot; not `.`/`..` | `ItemName` | `invalid_name` |
| Name compared ignoring case (`ToUpperInvariant`, as Identity does for emails) | `ItemName.Normalized` | `name_taken` |
| File extension in the allow-list of 11, compared ignoring case; no extension is rejected | `SupportedLanguages` | `unsupported_extension` |
| Depth ≤ 20, path ≤ 1,024 chars, ≤ 5,000 items, no move into itself or a descendant | `ProjectTree` | `too_deep`, `path_too_long`, `too_many_items`, `invalid_move` |
| Content: strict UTF-8 (rejects invalid bytes), no U+0000, ≤ 1,048,576 bytes | `FileContentRules` | `invalid_content`, `content_too_large` |

Rejecting control characters and `:`, `*` and similar keeps names portable if export is ever
added. They are rejected now, not sanitized, so the user always sees exactly what was stored.

**Path traversal**: requests never carry paths. The API addresses items only as
`/api/workspaces/{workspaceId}/items/{itemId}`, and every query filters by **both** ids. An
item id from another workspace is "not found" (FR-027). Names can't contain separators, so `..`
is only ever a literal name, and it is rejected anyway.

## R7. Authorization: two new operations in the existing permission table

**Decision**:
- Add `WorkspaceOperation.EditFiles` (Owner, Editor) and `WorkspaceOperation.DeleteFiles`
  (Owner only) to `WorkspacePermissions`.
- Reading the tree and content uses the existing `Read` operation.
- Every new endpoint uses the S02 `RequireWorkspace(op)` filter, which already reads the role
  per request: non-member → 404, member without permission → 403.

**Rationale**: It reuses the single source of truth from S02 with no new mechanism. The spec's
FR-024 table maps one-to-one onto the two new operations. Endpoints use only the bearer token,
and no cookie is involved, so no CSRF defence is needed beyond S02's.

**Alternatives considered**: separate Create/Rename/Move operations. All three have identical
role sets, so the extra enum values would add nothing. Rejected.

## R8. Editor: Monaco, bundled locally, highlighting only

**Decision**:
- Packages: `monaco-editor` 0.57.0 and `@monaco-editor/react` 4.7.0 (peer range covers React
  19).
- Configure `loader.config({ monaco })` with a local import, so nothing loads from a CDN.
- Import only the editor core (`editor.api`) and the tokenizer contributions for C#, JavaScript,
  TypeScript, JSON, HTML, CSS, Python, Java and Markdown. Register only the base editor worker
  via Vite's `?worker` import.
- The editor chunk is lazy-loaded, so sign-in and list pages don't download it.

**Extension → language**: `.cs`→csharp, `.js`/`.jsx`→javascript, `.ts`/`.tsx`→typescript,
`.json`→json, `.html`→html, `.css`→css, `.py`→python, `.java`→java, `.md`→markdown.

**Rationale**:
- The default `@monaco-editor/react` loader fetches Monaco from jsDelivr. That breaks offline
  local development and sends traffic to a third party.
- The TypeScript, JSON, CSS and HTML language workers are effectively language services, which
  are out of scope. The TS worker would also flag every cross-file import as an error, because
  it can't see the other files. Tokenizers alone give exactly "syntax highlighting" (FR-011),
  and the bundle is far smaller.
- Built-in find, undo/redo and bracket matching remain (spec Assumptions).

**Alternatives considered**:
- *CodeMirror 6*: lighter, but the user specified Monaco. Rejected.
- *Full `monaco-editor` with all workers*: larger bundle, plus misleading diagnostics. Rejected.
- *Raw `monaco-editor` without the React wrapper*: more lifecycle code to maintain. The wrapper
  is a thin, maintained adapter. Kept the wrapper.

## R9. Frontend state boundaries

**Decision**:

| State | Lives in | Why |
|-------|----------|-----|
| Tree, file content as loaded from the server | RTK Query cache (`filesApi`) | Server state with tags, refetch and the reset on account change from S02 |
| Open tabs, active tab, expanded folders, per-file save status (`saved` / `dirty` / `saving` / `failed` + reason), base revision | Redux `editorSlice` (small, serializable) | UI needs to react to it; testable reducers |
| Editor buffer text, undo stack, cursor and scroll | Monaco models, one per open file, keyed by item id | Large and changes per keystroke. Putting it in Redux would dispatch on every key and copy up to 1 MiB per action |
| Autosave timers and in-flight save | `AutosaveController`, one per open file, outside React render | Timer logic is unit-testable with fake timers |
| Drafts, persisted open tabs | `localStorage`, keyed by user id | Must survive reload; see R11 |

**Authority rule** (user requirement): the server copy is the only authoritative content. The
Monaco buffer and the draft are explicitly *unsaved* working copies, always shown with a
non-"Saved" status, and never used to answer "what is saved".

## R10. Autosave algorithm

**Decision**: one controller per open file, holding `baseRevision`, `lastSavedText` (or its
hash), and an in-flight flag.
- On change: status becomes `dirty`, and the 1 s debounce timer restarts. If no save has
  happened for 10 s since the first unsaved change, a save starts anyway (max wait).
- **One in flight**: while a save is running, further changes only mark the file dirty. When the
  save finishes, the controller saves again if the text differs from what was sent.
- **No empty saves**: if the text equals the last saved text, nothing is sent and the status
  returns to `saved` (FR-020).
- **Flush** (save now) on: tab switch, tab close, leaving the workspace route, and
  `visibilitychange → hidden`.
- **Errors**:
  - Network failure, 5xx or 503: status `failed: offline`, then automatic retries at 2 s, 4 s and
    8 s (capped at 30 s). Any new edit also retries. A Retry button is always offered.
  - 412: status `failed: conflict`; autosave pauses (FR-023).
  - 404: `failed: deleted`; autosave stops and the buffer stays copyable.
  - 403: `failed: no permission`; the editor switches to read-only, and the existing
    `permissionDenied` action refetches the role.
  - 413 / 400: `failed: too large` or `invalid content`; no automatic retry.
- **Cancellation**: saves are never aborted once sent, because an aborted save may still have
  committed. The response always updates the revision. Reads are aborted on unmount (RTK Query
  default).

**Rationale**: meets FR-020, FR-021 and SC-003. At most 6 max-wait saves plus 1 trailing save
in 60 s of continuous typing gives at most 7.

## R11. Unload, drafts and the 64 KiB `keepalive` limit

**Decision**:
- **Writing drafts**: while a file is dirty, the draft is written to `localStorage` at most every
  500 ms, and again on `pagehide`. The key is `devsync:draft:<userId>:<itemId>`, and the value
  is `{ text, baseRevision, savedAt }`.
- **Save on leave**: on `pagehide`, an immediate save is also attempted with
  `fetch(…, { keepalive: true })`, but **only when the body is ≤ 64 KiB**, because browsers
  reject larger `keepalive` bodies. Bigger edits rely on the draft.
- **Clearing drafts**: a draft is removed when a save of that text succeeds or the person
  discards it. All of the user's drafts are removed on explicit sign-out (`signedOut`), but
  **not** on `sessionEnded`: an expired session is exactly when the draft is needed (spec Edge
  Cases). Drafts older than 7 days are purged at startup.
- **Restoring**: when a file opens and its draft differs from the server text, a prompt offers
  Restore or Discard. Restore loads the text with the draft's `baseRevision`, so if the file
  moved on, the next save is a 412 conflict (FR-036).
- **Quota or storage errors**: writes are wrapped in try/catch and skipped silently (spec Edge
  Cases).

**Rationale**:
- `localStorage` is synchronous, so it is reliable inside `pagehide`. IndexedDB writes are async
  and may not finish while the page unloads.
- Drafts are source code, not credentials, so this is consistent with S02's storage rule.
- Keying by user id means another account in the same browser never sees them (FR-037).

**Accepted risk**: drafts from an expired (not signed-out) session stay in that browser's
storage for up to 7 days. They are readable by someone with access to that browser profile.
This is documented in the README.

**Alternatives considered**:
- *IndexedDB*: larger quota, but async writes are unreliable at unload. Rejected.
- *`sendBeacon`*: same 64 KiB limit and can't set `Authorization`. Rejected.
- *`beforeunload` prompt*: the clarified choice was save plus draft, not a warning. Not added.

## R12. Seeing other people's changes without real-time

**Decision**:
- The tree query refetches on window focus and reconnect (RTK Query `refetchOnFocus` and
  `refetchOnReconnect`), after the person's own mutations, and whenever a file operation returns
  404.
- Open tabs re-read their names from the tree, so a rename by someone else shows up after the
  next refetch.
- Content isn't polled. A stale buffer is caught by the revision check on the next save (R4).

**Rationale**: correctness comes from the server checks, not from freshness, so periodic polling
would add load without adding safety. Real-time sync is a later feature (user instruction).

## R13. Request limits at each hop

**Decision**:
- **nginx**: `client_max_body_size 2m` for `/api/`. The default of 1 MiB would reject a
  file exactly at the 1 MiB limit with an HTML 413 before the API sees it.
- **API**: the content endpoint reads at most 1,048,577 bytes and answers 413
  `content_too_large` beyond 1 MiB. Other file endpoints keep the default limits; their JSON
  bodies are tiny.
- **Vite dev proxy**: no limit, so the API limit is the effective one.

**Rationale**: the spec limit must be the API's, with the same Problem Details in Compose and
native runs.

## R14. Logging

**Decision**: no change to the S01/S02 logging rules: request bodies are never logged, and EF
sensitive-data logging stays off. File endpoints log item ids, sizes and revisions only, never
names or content. An integration test writes a marker string into a file and asserts it never
appears in captured logs (NFR-001).

## R15. Migration

**Decision**: one EF migration, `ProjectFiles`, adds `workspace_items` and `file_contents`. It is
purely additive: existing workspaces get an empty tree, and no data is changed. Down-migration
drops both tables.
