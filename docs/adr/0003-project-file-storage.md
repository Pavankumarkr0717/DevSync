# ADR 0003: Project file storage and concurrency

**Status**: Accepted | **Date**: 2026-10-31 | **Spec**: [003 Project Files and Code Editor](../../specs/003-file-explorer-editor/spec.md)

## Context

Feature 003 gives each workspace a tree of folders and text source files that members browse
and edit in the browser, with autosave. Several people may have the same file open. There is no
real-time co-editing yet, so the risk to manage is losing someone's newer work to a stale save.
Files must be application-managed records, never host paths, and no new infrastructure may be
added (constitution Principle IX). The detailed alternatives are in
[research.md](../../specs/003-file-explorer-editor/research.md) (R1–R15).

## Decision

1. **Files are database records.**
   - `workspace_items` holds the tree as an adjacency list: an `id`, a nullable `parent_id`,
     a `kind` of folder or file, and the `name`.
   - `file_contents` holds each file's text with a `revision`, 1:1 with a file item.
   - Nothing is ever read from or written to the host filesystem.
   - Requests never carry paths: items are addressed by `(workspaceId, itemId)`, and every query
     filters by both.
   - Names cannot contain separators, control characters, `.`/`..` or Windows-reserved characters.
2. **Structure changes are serialized per workspace.**
   - Create, rename, move and delete lock the workspace row (`SELECT … FOR UPDATE`), load the
     whole tree (at most 5,000 small rows) and check every rule in a pure domain `ProjectTree`:
     unique names ignoring case, depth ≤ 20, paths ≤ 1,024 characters, no cycles, item limit.
   - A unique index (`NULLS NOT DISTINCT`) backs up the name rule.
   - Deleting a folder relies on `ON DELETE CASCADE`.
3. **Content saves use compare-and-swap on an explicit revision.**
   - The save is one statement: `UPDATE … SET revision = revision + 1 WHERE revision = @expected`.
   - Over HTTP, the body is the raw `text/plain; charset=utf-8` content and the base revision
     goes in `If-Match: "<n>"`. A missing header is answered with `428`, a stale revision with
     `412` plus `currentRevision`, and the response carries the new revision as an `ETag`.
   - Saves never take the workspace lock.
4. **Limits are enforced by the API.**
   - 1 MiB per file of strict UTF-8 without NUL. The body is read with a hard cap and answered
     with 413 above it.
   - nginx allows 2 MiB, so the API's own error is what clients see.
5. **Editor**:
   - Monaco 0.57, bundled locally and lazy-loaded, never from a CDN.
   - It loads the editor core and syntax tokenizers only. The TypeScript, JSON, CSS and HTML
     language services are left out (out of scope as language servers).
   - JSON highlighting uses a small local tokenizer.
6. **Client state**:
   - Editor text lives in Monaco's per-file models and in an autosave controller, never in
     Redux.
   - Autosave waits for a 1 s pause, saves at least every 10 s while typing continues, and never
     has two saves in flight.
   - Unsaved text is also kept as a per-user draft in `localStorage` and offered back on return.
     A restored draft keeps its base revision, so a stale draft conflicts instead of overwriting.

## Consequences

- A stale save can never silently overwrite newer content. The person sees the conflict and
  chooses: reload the latest version, overwrite deliberately, or copy their text.
- Structure changes in one workspace are serialized. That's fine for human-paced editing, and
  saves (the hot path) are not affected.
- Other people's renames and deletes show up when the window regains focus, after one's own
  changes, or on the next save. There is no polling and no push; real-time sync needs its own
  spec.
- Deletes are permanent; only the Owner can delete. There is no history or trash.
- Drafts from an expired (not signed-out) session stay in that browser for up to 7 days. The
  last-moment `keepalive` save is limited to 64 KiB by browsers, so larger unsaved edits rely
  on the draft.
- The editor chunk is large (about 1 MB gzipped), but only the files page loads it. No
  performance figure is claimed (Principle XII).
- `monaco-editor` 0.57.0 pins a DOMPurify version with known advisories. An npm `overrides`
  entry forces the patched 3.4.17 until Monaco updates.
