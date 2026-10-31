# Data Model: Project Files and Code Editor

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md) | **Date**: 2026-10-31

These are additions to the [002 data model](../002-identity-and-permissions/data-model.md). All
times are `timestamptz` UTC. Names are snake_case and configured explicitly, as in 001 and 002.
Nothing existing changes, apart from two new values in the `WorkspaceOperation` enum, which is
code only.

## Domain model (`DevSync.Domain/Files`)

### ProjectLimits (constants)

| Constant | Value | Spec |
|----------|-------|------|
| `MaxNameLength` | 255 | FR-005 |
| `MaxPathLength` | 1,024 | FR-006 |
| `MaxDepth` | 20 (top level = 0) | FR-007 |
| `MaxItemsPerWorkspace` | 5,000 | FR-008 |
| `MaxContentBytes` | 1,048,576 | FR-016 |

### ItemKind

`Folder | File`, stored as `'folder' | 'file'`.

### ItemName (value object)

`ItemName.Create(string?) → Result<ItemName>`. Input is **not** trimmed: leading or trailing
spaces are an error, not something silently fixed.
- **Valid when**:
  - length 1–255;
  - none of `/ \ < > : " | ? *`;
  - no control characters (U+0000–U+001F, U+007F);
  - doesn't start or end with a space;
  - doesn't end with `.`;
  - isn't `.` or `..`.
- **`Normalized`**: `Value.ToUpperInvariant()`, used for case-insensitive comparison and the
  unique index.
- **`Extension`**: the text after the last `.`, if any (`.eslintrc.json` → `json`).
- **Error**: `invalid_name`, with a message naming the broken rule.

### SupportedLanguages (pure)

`TryGetLanguage(ItemName) → string?` maps an extension (ignoring case) to a language id:
- `cs` → `csharp`
- `js`, `jsx` → `javascript`
- `ts`, `tsx` → `typescript`
- `json` → `json`
- `html` → `html`
- `css` → `css`
- `py` → `python`
- `java` → `java`
- `md` → `markdown`

Anything else, including no extension → `unsupported_extension` (FR-010). Applies to files only.

### WorkspaceItem (entity)

| Field | Rules |
|-------|-------|
| Id | UUIDv7, stable for life (FR-003) |
| WorkspaceId | required; never changes |
| ParentId | null = top level; otherwise a **folder** in the same workspace |
| Kind | `Folder` or `File`; never changes |
| Name / NormalizedName | `ItemName` |
| CreatedAt / CreatedByUserId | set once |
| UpdatedAt | set on create, rename and move |

### ProjectTree (aggregate over one workspace's items)

The tree is built from all of a workspace's `WorkspaceItem`s, loaded under the workspace lock
(research R2). Operations validate against the whole tree, then mutate the tracked entities:

| Operation | Checks, in order | Errors |
|-----------|------------------|--------|
| `Create(parentId, kind, name, by, time)` | parent exists, is a folder, same workspace → `ItemName` valid → extension supported (files) → no sibling with the same `Normalized` → depth(parent)+1 ≤ 20 → path length ≤ 1,024 → item count < 5,000 | `parent_not_found`, `invalid_name`, `unsupported_extension`, `name_taken`, `too_deep`, `path_too_long`, `too_many_items` |
| `Rename(itemId, name, time)` | item exists → name valid → extension supported (files) → no *other* sibling with the same `Normalized` (case-only renames of itself are allowed) → longest descendant path still ≤ 1,024 | `item_not_found`, `invalid_name`, `unsupported_extension`, `name_taken`, `path_too_long` |
| `Move(itemId, newParentId, time)` | item exists → target exists and is a folder (or null) → target is not the item or a descendant of it → no sibling clash in the target → depth(target)+1+height(item) ≤ 20 → longest descendant path ≤ 1,024. A move to the current parent is a successful no-op. | `item_not_found`, `parent_not_found`, `invalid_move`, `name_taken`, `too_deep`, `path_too_long` |
| `Delete(itemId)` | item exists. Returns the ids of the item and every descendant, for the response and logs. | `item_not_found` |
| `PathOf(itemId)` | joins names from the top level with `/` | — |

Invariants (each tested): no cycles; every parent is a folder in the same workspace; sibling
names are unique ignoring case; depth ≤ 20; every path ≤ 1,024; count ≤ 5,000.

### FileContent (entity, 1:1 with a File item)

| Field | Rules |
|-------|-------|
| ItemId | PK and FK to the file item |
| Content | text; validated by `FileContentRules` |
| SizeBytes | UTF-8 byte count of `Content` |
| Revision | starts at **1** on create (empty content), +1 per successful save |
| UpdatedAt / UpdatedByUserId | set on create and each save |

### FileContentRules (pure)

`Validate(ReadOnlySpan<byte> body) → Result<string>`:
1. If the length is > 1,048,576 → `content_too_large`.
2. Decode with strict UTF-8 (throw on invalid bytes, including lone surrogates). Failure →
   `invalid_content`.
3. If the text contains U+0000 → `invalid_content`.
4. Return the decoded string unchanged. A leading BOM, `\r\n` and `\r` are all kept as sent
   (FR-017).

### WorkspacePermissions (extended)

| Operation | Owner | Editor | Viewer |
|-----------|:-----:|:------:|:------:|
| `Read` (existing; now also tree and content) | ✓ | ✓ | ✓ |
| `EditFiles` (new: create, save, rename, move) | ✓ | ✓ | ✗ |
| `DeleteFiles` (new) | ✓ | ✗ | ✗ |

## Relational schema

Migration **`ProjectFiles`**. It is additive only, and existing workspaces start with an empty
tree.

### `workspace_items`

| Column | Type | Constraints |
|--------|------|-------------|
| id | uuid | PK |
| workspace_id | uuid | NOT NULL, FK → workspaces ON DELETE CASCADE |
| parent_id | uuid | NULL, FK → workspace_items(id) ON DELETE CASCADE |
| kind | varchar(8) | NOT NULL, CHECK in (`folder`,`file`) |
| name | varchar(255) | NOT NULL, CHECK `char_length(name) BETWEEN 1 AND 255` |
| normalized_name | varchar(255) | NOT NULL |
| created_at, updated_at | timestamptz | NOT NULL |
| created_by_user_id | uuid | NULL, FK → users ON DELETE SET NULL |

Indexes:
- `ux_workspace_items_sibling_name`: UNIQUE (`workspace_id`, `parent_id`, `normalized_name`)
  **NULLS NOT DISTINCT**. This enforces FR-004, including at the top level.
- `ix_workspace_items_parent_id` (`parent_id`): cascade deletes and children lookups.
- `ix_workspace_items_created_by_user_id`: FK index.
- The unique index's leading `workspace_id` column also serves "load the whole tree of a
  workspace".

Parent-is-a-folder and same-workspace rules are enforced by `ProjectTree` under the workspace
lock, not by the database. A composite FK (`workspace_id`, `parent_id`) → (`workspace_id`,
`id`) would also be possible. It is left out to keep the schema simple, because the only writer
is the locked, domain-validated path. All writes are tested.

### `file_contents`

| Column | Type | Constraints |
|--------|------|-------------|
| item_id | uuid | PK, FK → workspace_items(id) ON DELETE CASCADE |
| content | text | NOT NULL (default `''`) |
| size_bytes | integer | NOT NULL, CHECK `size_bytes BETWEEN 0 AND 1048576` and `size_bytes = octet_length(content)` |
| revision | bigint | NOT NULL, CHECK `revision >= 1` |
| updated_at | timestamptz | NOT NULL |
| updated_by_user_id | uuid | NULL, FK → users ON DELETE SET NULL |

Index: `ix_file_contents_updated_by_user_id` (FK index). A content row exists exactly for
`kind = 'file'` items. It is created in the same transaction as the item.

## Transaction boundaries

| Operation | Transaction |
|-----------|-------------|
| Create folder | lock workspace → load tree → validate → insert item |
| Create file | lock workspace → load tree → validate → insert item + empty content (revision 1) |
| Rename / move | lock workspace → load tree → validate → update one item |
| Delete | lock workspace → load tree → validate → delete one item (DB cascades the subtree and its contents) |
| Save content | single conditional `UPDATE … WHERE revision = @expected` (no workspace lock). If 0 rows, a follow-up read tells conflict from deleted |
| Read tree / content | plain reads, no lock |

How races resolve:
- **Save vs delete**: if the delete commits first, the save's `UPDATE` matches 0 rows → 404.
  If the save commits first, the delete waits on the content row's lock, then removes it.
- **Two saves at one revision**: exactly one matches `revision = @expected`. The other gets 412.
- **Two structure changes**: serialized by the workspace row lock.

## Query rules

- **Tree**: `SELECT id, parent_id, kind, name, updated_at FROM workspace_items WHERE workspace_id
  = @w`. Sorting and nesting are done by the client. Content is never included.
- **Content**: join `file_contents` to `workspace_items` on `item_id`, filtered by
  `workspace_id = @w AND id = @item AND kind = 'file'`. A miss means 404.
- **Every query filters by the workspace id from the route** (already authorized by
  `RequireWorkspace`), so an item id from another workspace is never found (FR-027).
