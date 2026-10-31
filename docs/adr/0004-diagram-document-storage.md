# ADR 0004: Diagram document storage and the canvas

**Status**: Accepted | **Date**: 2026-10-31 | **Spec**: [004 System Design Canvas](../../specs/004-system-design-canvas/spec.md)

## Context

Feature 004 lets workspace members draw system architecture diagrams: typed components
(client, API gateway, microservice, database, cache, message queue, load balancer, external
service) joined by labelled, directed connections, with autosave. As with files in 003, several
people may have the same diagram open, there is no real-time co-editing, and the risk to manage
is a stale save overwriting newer work. A canvas is chatty: one drag produces dozens of position
updates. No new infrastructure may be added (constitution Principle IX), and diagram data must
never become executable. The detailed alternatives are in
[research.md](../../specs/004-system-design-canvas/research.md) (R1–R14).

## Decision

1. **One JSONB document per diagram.**
   - The `diagrams` table holds the metadata (id, workspace, name, revision, counts, timestamps)
     and the whole content in a `document jsonb` column.
   - A save replaces the document as a unit. Node and edge counts are mirrored into plain
     columns so the list never reads documents.
   - Nodes and edges are not normalized into tables. Nothing queries inside diagrams, and a
     multi-row diff per save would need multi-statement consistency for no reader's benefit.
2. **The server validates everything and stores a canonical form.**
   - The body is read with the 003 bounded reader (1 MiB, 413 above).
   - Parsing is strict System.Text.Json: duplicate properties, unknown properties, missing
     required properties, comments and non-finite numbers are all rejected.
   - A pure `DiagramValidator` checks the structural rules: known component types, edges that
     reference existing nodes, no self-connections, no duplicate (source, target) pairs, unique
     ids, text limits, control characters, metadata limits and coordinate ranges. It collects up
     to 50 problems, each with a code, a JSON path and a message. Any problem → 400
     `invalid_diagram`, and nothing is stored.
   - The typed document is re-serialized canonically before it is stored, so unknown or extra
     data can never reach the database. Metadata values are inert, length-limited strings that
     are never evaluated and are rendered as text.
   - The missing database foreign keys (edge → node) are compensated by this validator, which
     runs on every write and has a test per rule.
3. **Versioned documents, upgraded forward on read.**
   - Every document carries `schemaVersion` (currently 1), mirrored in a column.
   - Reads run a chain of pure `v(n) → v(n+1)` steps and never write back; the next save stores
     the current version. Clients may only write the current version.
   - A stored version newer than the running release is never overwritten silently. GET returns
     `document: null` with `unsupportedSchemaVersion`, and PUT is refused with 409 unless the
     caller explicitly asks to replace it (`?replaceUnsupported=true`, "Start over" in the UI).
4. **Saves use the 003 revision compare-and-swap.**
   - `PUT …/diagrams/{id}/document` with `If-Match: "<revision>"`; a single conditional
     `UPDATE` bumps the revision. Missing header → 428, stale revision → 412 with
     `currentRevision`.
   - Names are unique per workspace ignoring case (a unique index on the normalized name), and a
     workspace holds at most 50 diagrams.
5. **Permissions** add two operations to the S02 table: `EditDiagrams` (Owner, Editor: create,
   rename, save) and `DeleteDiagrams` (Owner). Non-members get 404, as for every workspace
   resource.
6. **Canvas**:
   - React Flow (`@xyflow/react` 12.12.0), bundled locally and lazy-loaded with the canvas page
     only. `check-bundle.mjs` asserts it stays out of the entry chunk.
   - In-flight interaction (drag positions, selection, the live viewport) stays in React Flow's
     local state. Redux holds only the persisted document, normalized with entity adapters, and
     changes only at commit points: drag stop, resize end, connect, delete and panel edits.
     So a drag is one change and one save, and panning or zooming alone never saves.
   - Each commit serializes the canonical document (with the current viewport) into the shared
     003 autosave controller and draft store, so "unchanged" is a string comparison.

## Consequences

- A stale save can never silently overwrite a newer diagram. The person chooses **Reload
  latest**, **Overwrite with mine** or **Copy mine**, as for files.
- Every save rewrites the whole document (typically a few KB, at most 1 MiB). That's fine at
  human pace and keeps conflicts per diagram simple.
- Cross-diagram queries ("every database in the workspace") would need JSON operators or a
  later read model. None is needed today.
- New document versions need an upgrade step and fixtures, but no database migration unless a
  mirrored column changes. The upgrade machinery is exercised by a test-only v0 → v1 step.
- The viewport is saved only with the next content change, so panning alone doesn't move other
  people's saved view.
- The canvas chunk is about 64 KB gzipped and loads only on the canvas page. No performance
  figure is claimed (Principle XII).
- Real-time collaboration, auto layout, export and comments are out of scope and need their own
  specs.
