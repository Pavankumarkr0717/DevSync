# Quickstart & Validation: Project Files and Code Editor

**Feature**: [spec.md](./spec.md) | **Contracts**: [openapi.yaml](./contracts/openapi.yaml),
[ui-routes.md](./contracts/ui-routes.md) | **Data model**: [data-model.md](./data-model.md)

## Prerequisites and setup

Same as the [002 quickstart](../002-identity-and-permissions/quickstart.md). No new environment
variables or services are needed.

```bash
cp .env.example .env            # set DB_PORT if 5432 is taken
docker compose up --build -d    # applies the ProjectFiles migration on start
docker compose logs api | grep "ProjectFiles"   # "Applying 1 migration(s): …_ProjectFiles"
```

Upgrading from 002 keeps every account, workspace, member and invitation. Each existing workspace
starts with an empty tree.

**Setup for the scenarios**: register Ana (Owner), Ben and Cy, as in 002 B2–B7. Ana creates
"Demo" and invites Ben as **Editor** and Cy as **Viewer**; both accept.

## Browser scenarios (http://localhost:8080)

| # | Steps | Expected |
|---|-------|----------|
| F1 | Ana opens Demo → **Open files** | `/workspaces/{id}/files` shows "No files yet", plus New file and New folder |
| F2 | Ana creates `src` → `src/api` → `src/api/server.ts` | The nested tree appears; `server.ts` opens in a tab with TypeScript highlighting |
| F3 | Create `notes.txt`, `Dockerfile`, `a/b`, and `SRC` at the top level | Rejected: unsupported extension (twice), invalid name, and "already exists" |
| F4 | Type in `server.ts` for ~15 s without stopping, then stop | The status shows "Unsaved changes", then "Saving…", then "Saved". In DevTools → Network: at most 2 `PUT …/content` requests during typing (10 s maximum wait), plus 1 after stopping, never one per key |
| F5 | Reload the browser | The same tabs reopen and `server.ts` shows the typed text |
| F6 | Open five files, edit two, and switch between them | Each tab keeps its text, cursor and scroll position; `●` marks unsaved tabs; switching saves the pending one |
| F7 | Ben (other browser) and Ana both open `server.ts`. Ben edits and gets "Saved"; then Ana edits | Ana sees "Someone else saved a newer version", with Reload latest / Overwrite with mine / Copy mine. Ben's text is still on the server until Ana picks Overwrite |
| F8 | Ben renames `server.ts` → `app.ts` and moves `src/api` to the top level | Allowed. Ana's tab shows the new name after she focuses her window, and her next save succeeds |
| F9 | Ben looks for Delete | Not offered |
| F10 | Ana deletes `api` | The confirmation states the item count; the folder and its tabs disappear. Ben's open `app.ts` shows "This file was deleted" on his next save, and his text stays copyable |
| F11 | Cy opens Demo files | Can browse and read. The editor is read-only; no create, rename, move or delete actions |
| F12 | Ana downgrades Ben to Viewer while Ben has unsaved edits | Ben's next save shows "You no longer have permission to edit"; the editor becomes read-only and his text stays copyable |
| F13 | Ana types, then within 1 s presses F5 | After the reload, either the change is saved, or a "You have unsaved changes from …" bar offers Restore / Discard |
| F14 | DevTools → Network → Offline; type; go back online | "Couldn't reach the server — retrying", then "Saved" without retyping |
| F15 | Paste ~1.1 MB of text into a file | "File is larger than 1 MiB"; the text stays in the editor |
| F16 | Ana signs out, then inspects DevTools → Application → Local Storage | No `devsync:draft:*` or `devsync:tabs:*` keys for Ana remain |

## API scenarios (curl)

Get an access token as in 002 (`POST /api/auth/login` with `X-DevSync-Client: web`) and export
`T=<accessToken>`, `W=<workspaceId>`. All calls use `-H "Authorization: Bearer $T"`. Base URL:
`http://localhost:8080`.

| # | Call | Expected |
|---|------|----------|
| P1 | `POST /api/workspaces/$W/items` `{"parentId":null,"kind":"folder","name":"src"}` | 201 `Item` |
| P2 | `POST …/items` `{"parentId":"<src>","kind":"file","name":"main.py"}` | 201 |
| P3 | `GET /api/workspaces/$W/tree` | 200 with both items and `limits` |
| P4 | `GET …/files/<main>/content` | 200, empty body, `ETag: "1"`, `Cache-Control: no-store` |
| P5 | `PUT …/files/<main>/content` with `If-Match: "1"`, `Content-Type: text/plain; charset=utf-8`, body `print("hi")\r\n` | 200 `{"revision":2,…}`; `GET` then returns the exact bytes, `\r\n` included |
| P6 | Repeat P5, still with `If-Match: "1"` | 412 `revision_conflict`, `currentRevision: 2`; content unchanged |
| P7 | P5 without `If-Match` / with `Content-Type: application/json` | 428 `revision_required` / 415 `unsupported_media_type` |
| P8 | P5 with a 1,048,577-byte body; then exactly 1,048,576 bytes | 413 `content_too_large` (Problem Details from the API, not an nginx page); then 200 |
| P9 | P5 with the body containing a NUL byte, or invalid UTF-8 (`printf '\xff'`) | 400 `invalid_content` |
| P10 | `POST …/items` named `../etc`, `a/b`, `con:`, ` x.ts`, `x.ts.`, `x.txt` | 400 `invalid_name` (×5), 400 `unsupported_extension` |
| P11 | `POST …/items/<src>/move` `{"parentId":"<src>"}`; a move into its own descendant | 400 `invalid_move` |
| P12 | `GET …/files/<itemIdFromAnotherWorkspace>/content` (as a member of `$W`) | 404 `item_not_found` |
| P13 | As Cy (Viewer): `POST …/items`, `PUT …/content`, `…/rename`, `…/move`, `DELETE …/items/<id>` | 403 `forbidden` each; tree unchanged |
| P14 | As Ben (Editor): `DELETE …/items/<id>` | 403 `forbidden` |
| P15 | As a non-member: `GET /api/workspaces/$W/tree` | 404 `not_found` |
| P16 | Send the same `PUT` (`If-Match: "N"`) 10 times in parallel (`xargs -P10`) | Exactly one 200; nine 412 |
| P17 | `docker compose logs api` after P5 | No file content (`print("hi")`) or file names in the logs |

## Automated tests

```bash
dotnet test --solution src/backend/DevSync.sln     # unit + integration (Docker running)
npm install --prefix src/frontend
npm run typecheck --prefix src/frontend && npm run lint --prefix src/frontend
npm test --prefix src/frontend
npm run build --prefix src/frontend                # the editor is a separate lazy chunk
```

Expected: all pass. That includes the extended role matrix, the concurrency tests, the
log-scan test, and every 001/002 test (SC-007).

## Evidence

Record results (date, versions, pass/fail per scenario, deviations) in
`specs/003-file-explorer-editor/verification.md`. Record anything not verified (for example,
Safari or Firefox `keepalive` behaviour) explicitly.
