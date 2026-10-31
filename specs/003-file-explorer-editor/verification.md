# Verification Evidence: Project Files and Code Editor

**Date**: 2026-10-31 | **Feature**: [spec.md](./spec.md) | **Tasks**: T075, T076, T080

## Environment

| Tool | Version |
|------|---------|
| OS | Windows 11 (10.0.26300) |
| .NET SDK | 10.0.402 (Microsoft.Testing.Platform) |
| Node.js / npm | 24.21.0 / 10.9.0 |
| Docker / Compose | 29.8.0 / v5.5.1 |
| PostgreSQL | `postgres:18-alpine` |
| New packages | `monaco-editor` 0.57.0, `@monaco-editor/react` 4.7.0; npm override `dompurify` 3.4.17 |

## Automated suites (T075, T076)

| Command | Result |
|---------|--------|
| `dotnet build src/backend/DevSync.sln --no-incremental` | Succeeded, **0 warnings** |
| `dotnet test --solution src/backend/DevSync.sln` | **559 passed**, 0 failed (256 unit, 303 integration; up from 301 after 002) |
| `npm run typecheck` (app + tests) | Passed |
| `npm run lint` (app + tests) | Passed, no findings |
| `npm test` | **179 passed**, 0 failed (27 files; up from 75 after 002) |
| `npm run build` | Succeeded. The editor is a separate lazy chunk; each language tokenizer is its own small chunk |
| `npm run check:bundle` | Monaco only in the lazy `CodeEditor` chunk; no CDN references (the one allowed literal is `@monaco-editor/loader`'s unused default path, which is bypassed by `loader.config({ monaco })`) |

What the new backend tests cover (traceability: [checklists/traceability.md](./checklists/traceability.md)):

- **Domain rules**: names (including traversal strings), extensions, the 1 MiB/UTF-8/NUL content
  rules, and every tree operation and limit, with invariants checked after each.
- **API**: create, rename, move, delete and content endpoints, every error code and status,
  byte-exact round-trips, and the 413 boundary with and without `Content-Length`.
- **Path security**: cross-workspace item ids, encoded ids, and no route taking a path.
- **Concurrency**:
  - 10 parallel saves from one revision give exactly one 200;
  - crossed moves (10 rounds) never create a cycle;
  - a rename racing a delete, and a save racing a delete (20 rounds, run 6 times): no 500s;
  - parallel creates can't exceed 5,000 items.
- **Authorization**: the role matrix grew to 14 operations × 5 callers (70 cases), plus
  privilege-escalation cases for files.
- **Migration**: the `ProjectFiles` migration over a 002 database keeps all rows, with
  constraints and cascades checked.
- **Logs**: the log scan finds no file names or content.

## End-to-end against Docker Compose (T080)

**Upgrade path**: `docker compose up --build -d` over the existing 002 database volume. The API
logged `Applying 1 migration(s): 20261031052604_ProjectFiles`. The existing 6 accounts and 2
workspaces were kept, and `/health/ready` answered `Healthy`.

**API scenarios**: quickstart P1–P17, plus API equivalents of F8–F10, scripted with real HTTP
requests through nginx (`http://localhost:8080`). **Result: 22/22 checks passed.**

| Check | Result |
|-------|--------|
| P1–P3 create a folder and a file (Editor), the tree with limits | Pass |
| P4 new file: empty, `ETag "1"`, `Cache-Control: no-store` | Pass |
| P5 save → revision 2, bytes round-trip exactly including `\r\n` | Pass |
| P6 stale `If-Match` → 412 `revision_conflict`, `currentRevision: 2`, content unchanged | Pass |
| P7 no `If-Match` → 428; JSON body → 415 | Pass |
| P8 **through nginx**: 1,048,577 bytes → 413 Problem Details *from the API*; exactly 1 MiB → 200 | Pass |
| P9 NUL or invalid UTF-8 → 400 `invalid_content` | Pass |
| P10 `../etc`, `a/b`, `con:`, ` x.ts`, `x.ts.` → `invalid_name`; `x.txt` → `unsupported_extension` | Pass |
| P11 move into itself or a descendant → 400 `invalid_move` | Pass |
| P12 another workspace's item id → 404 `item_not_found` | Pass |
| P13 Viewer create, save, rename, move, delete → 403 each; tree unchanged | Pass |
| P14 Editor delete → 403 | Pass |
| P15 non-member tree → 404 `not_found` | Pass |
| P16 10 parallel saves from revision 1 → one 200, nine 412; the winner's text stored at revision 2 | Pass |
| F8–F10 Editor renames and moves; Owner deletes a folder subtree; deleted file → 404 | Pass |
| P17 no file content or names in `docker compose logs api` | Pass |
| SPA route `/workspaces/x/files` served; editor, worker and tokenizer chunks served from the same origin | Pass |

### Defects found and fixed during implementation

| Defect | Found by | Fix |
|--------|----------|-----|
| `monaco-editor` 0.57.0 depends on `dompurify` 3.4.15, which has 5 published advisories (DOM XSS) | `npm audit` after install | npm `overrides` → `dompurify` 3.4.17 (patch release); recorded in ADR 0003 |
| Monaco 0.57 moved its modules (`basic-languages/*` no longer exists; tokenizers are `languages/definitions/*/register`) and has no tokenizer-only JSON | Inspecting the package | Imports updated; a small local Monarch JSON tokenizer registered |
| `visibilitychange → hidden` fires on a plain tab switch; a `keepalive` save there would bump the server revision behind the controller's back and make its next save conflict with itself | Design review while writing `fileSession.ts` | Hidden → normal flush + draft; only `pagehide` sends the `keepalive` save |
| RTK Query `upsertQueryData` is asynchronous, so the cache could still show the old text right after a save resolved | `filesApi.test.ts` | `updateQueryData` (synchronous patch of the open file's entry) |
| The existing schema test listed every table and failed once two new tables existed | Checkpoint T027 | Test extended with the new tables, checks, FKs and indexes |

Two test-only problems were also fixed:
- The mock server reused item ids across tests, so a save still in flight from one test could
  overwrite the next test's file. Mock ids are now unique for the whole run.
- jsdom textareas normalize `\r\n` to `\n`. Exact line endings are verified by the backend
  round-trip tests and end-to-end P5 instead.

### Not verified manually

- **The browser scenarios F1–F16 were not clicked through in a real browser**; no browser was
  available in this session. They are covered by 104 component tests (explorer, tabs, autosave,
  conflicts, drafts, roles) running against a textarea stand-in for Monaco, plus the API checks
  above. In particular, **Monaco itself was not seen rendering** with syntax highlighting:
  per-file models, undo history, and cursor and scroll restore on tab switch are untested in a
  browser. The build produces the expected chunks, and they are served correctly.
- The `keepalive` save on page unload and the `pagehide` draft were tested in jsdom only; no
  real browser close or reload was performed (Chrome, Firefox and Safari behaviour unverified).
- Typing performance in a file at the 1 MiB limit (SC-008) was not checked.
- SC-001 (create three nested levels and start typing in under a minute) was not timed.
