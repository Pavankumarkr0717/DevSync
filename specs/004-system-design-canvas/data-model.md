# Data Model: System Design Canvas

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md) | **Date**: 2026-10-31

These are additions to the 001–003 data models. All times are `timestamptz` UTC. Names are
snake_case and configured explicitly. The only code change to existing types is two new
`WorkspaceOperation` values.

## Domain model (`DevSync.Domain/Diagrams`)

### DiagramLimits (constants)

| Constant | Value | Spec |
|----------|-------|------|
| `MaxDiagramsPerWorkspace` | 50 | FR-007 |
| `MaxNameLength` | 100 | FR-004 |
| `MaxNodes` / `MaxEdges` | 500 / 1,000 | FR-014, FR-018 |
| `MaxTitle` / `MaxDescription` / `MaxLabel` | 100 / 2,000 / 200 | FR-012, FR-015 |
| `MaxMetadataEntries` / `MaxMetadataKey` / `MaxMetadataValue` | 20 / 50 / 500 | FR-012 |
| `MinSize` / `MaxSize` | 40 / 2,000 | FR-012 |
| `MaxCoordinate` | 100,000 (absolute) | FR-012 |
| `MinZoom` / `MaxZoom` | 0.1 / 4 | FR-019 |
| `MaxViewportOffset` | 10,000,000 (absolute) | R5 |
| `MaxDocumentBytes` | 1,048,576 | R12 |
| `MaxIdLength` | 64, pattern `[A-Za-z0-9_-]+` | R3 |

### ComponentType

`Client | ApiGateway | Microservice | Database | Cache | MessageQueue | LoadBalancer |
ExternalService`. Wire values: `client`, `api_gateway`, `microservice`, `database`, `cache`,
`message_queue`, `load_balancer`, `external_service`.

### DiagramName (value object)

Trimmed; 1–100 code points; no control characters. `Normalized` is `ToUpperInvariant()`.
Invalid → `validation_failed` on field `name`.

### Diagram document v1 (immutable records)

| Record | Fields |
|--------|--------|
| `DiagramDocument` | `SchemaVersion` (int), `Nodes` (list of `DiagramNode`), `Edges` (list of `DiagramEdge`), `Viewport` (`Viewport?`) |
| `DiagramNode` | `Id`, `Type` (`ComponentType`), `Title`, `Description` (may be empty), `Metadata` (list of `MetadataEntry`), `Position` (`X`, `Y` doubles), `Size` (`Width`, `Height` doubles) |
| `MetadataEntry` | `Key`, `Value` |
| `DiagramEdge` | `Id`, `Source` (node id), `Target` (node id), `Label` (may be empty) |
| `Viewport` | `X`, `Y`, `Zoom` (doubles) |

`DiagramDocument.Empty` is `{ schemaVersion: 1, nodes: [], edges: [], viewport: null }`.

### DiagramValidator (pure)

`Validate(DiagramDocument) → IReadOnlyList<DiagramProblem>`. Each `DiagramProblem` is
`(Code, Path, Message)`, for example `("unknown_node_reference", "edges[3].target", "Edge
\"e7\" refers to unknown node \"n12\".")`.
- It collects up to 50 problems.
- An empty result means the document is valid.
- The rules are the table in research R5.
- Paths use the client's array indexes, so the app can highlight the item.

### DiagramDocumentUpgrader (pure)

`Upgrade(int fromVersion, JsonNode document) → (DiagramDocument? Current, UpgradeFailure?)`.
- It applies registered steps `v → v+1` until it reaches `DiagramSchema.CurrentVersion`.
- The production step list is empty while the current version is 1.
- A version above current, or a missing step → `UpgradeFailure.UnsupportedVersion(n)`.
- An unparseable document → `UpgradeFailure.Unreadable`.

### Diagram (aggregate)

| Field | Rules |
|-------|-------|
| Id | UUIDv7, stable (FR-003) |
| WorkspaceId | required; never changes |
| Name / NormalizedName | `DiagramName` |
| Revision | starts at **1** (the empty document); +1 on every successful content save; unchanged by rename |
| SchemaVersion | mirrors the stored document's version |
| NodeCount / EdgeCount | mirror the stored document (for the list view) |
| CreatedAt / CreatedByUserId | set once |
| UpdatedAt / UpdatedByUserId | set on create, rename and save |

Operations:
- `Diagram.Create(workspaceId, name, by, time)` → a diagram with the empty document.
- `Rename(name, time)`.

Content saves go through the repository's compare-and-swap (research R6), not by loading and
mutating the aggregate.

### WorkspacePermissions (extended)

| Operation | Owner | Editor | Viewer |
|-----------|:-----:|:------:|:------:|
| `Read` (list, open) | ✓ | ✓ | ✓ |
| `EditDiagrams` (new: create, rename, save content) | ✓ | ✓ | ✗ |
| `DeleteDiagrams` (new) | ✓ | ✗ | ✗ |

## Relational schema

Migration **`SystemDesignCanvas`**, additive.

### `diagrams`

| Column | Type | Constraints |
|--------|------|-------------|
| id | uuid | PK `pk_diagrams` |
| workspace_id | uuid | NOT NULL, FK → workspaces ON DELETE CASCADE |
| name | varchar(100) | NOT NULL, CHECK `char_length(btrim(name)) BETWEEN 1 AND 100` |
| normalized_name | varchar(100) | NOT NULL |
| revision | bigint | NOT NULL, CHECK `revision >= 1` |
| schema_version | integer | NOT NULL, CHECK `schema_version >= 1` |
| document | **jsonb** | NOT NULL, CHECK `jsonb_typeof(document) = 'object'` |
| node_count | integer | NOT NULL, CHECK `node_count BETWEEN 0 AND 500` |
| edge_count | integer | NOT NULL, CHECK `edge_count BETWEEN 0 AND 1000` |
| created_at, updated_at | timestamptz | NOT NULL |
| created_by_user_id, updated_by_user_id | uuid | NULL, FK → users ON DELETE SET NULL |

Indexes:
- `ux_diagrams_workspace_name`: UNIQUE (`workspace_id`, `normalized_name`). This enforces
  FR-004, and its leading column serves "list a workspace's diagrams".
- `ix_diagrams_created_by_user_id` and `ix_diagrams_updated_by_user_id`: FK indexes.

## Transaction boundaries

| Operation | Transaction |
|-----------|-------------|
| Create | lock the workspace row → count < 50 → insert (the unique index catches a name race → `diagram_name_taken`) |
| Rename | single `UPDATE` (the unique index → `diagram_name_taken`) |
| Save content | single conditional `UPDATE … WHERE revision = @expected` (no lock). On 0 rows, a follow-up read tells 412 from 404 |
| Delete | single `DELETE` |
| Read list / document | plain reads |

How races resolve:
- **Two saves at one revision**: exactly one wins.
- **Save vs delete**: the save gets 404 `diagram_not_found`.
- **Save vs rename**: both succeed, and the revision is unaffected by the rename.
- **Two creates at the 50th slot**: serialized by the workspace lock.

## Frontend state (`src/frontend/src/features/diagrams`)

```text
diagramEditorSlice (Redux, one open diagram)
  diagramId, workspaceId
  nodes:  EntityState<DiagramNode, string>   ← createEntityAdapter, sorted by id
  edges:  EntityState<DiagramEdge, string>
  viewport: Viewport | null                  ← as loaded; refreshed from React Flow at serialize time
  baseRevision, status, failure reason, currentRevision (on conflict)
  unsupportedSchemaVersion: number | null
  selection: NOT here (React Flow)            drag positions: NOT here (React Flow)
```

Selectors derive React Flow `nodes`/`edges` arrays (memoized) and the canonical serialized
document string used by autosave and drafts.
