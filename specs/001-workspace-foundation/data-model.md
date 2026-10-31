# Data Model: Workspace Foundation

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md) | **Date**: 2026-10-29

## Domain model

### User (module: Identity)

| Field | Type | Rules |
|-------|------|-------|
| Id | UUID (v7) | Immutable |
| DisplayName | string | Required, 1–100 code points, trimmed |
| CreatedAt | UTC instant | Set on creation |

In this release users are only created by the development seed (R6). No user-facing creation.

### Workspace (module: Workspaces) — aggregate root

| Field | Type | Rules |
|-------|------|-------|
| Id | UUID (v7) | Immutable, generated on creation |
| Name | `WorkspaceName` | Trimmed; 1–100 code points; no control characters; duplicates allowed |
| Description | `WorkspaceDescription?` | Trimmed; empty → null; ≤ 500 code points; control chars except `\n` `\r` `\t` rejected |
| OwnerId | UUID → User | Immutable in this release |
| CreatedAt | UTC instant | Set on creation |
| UpdatedAt | UTC instant | Equals CreatedAt in this release (no edits yet) |
| Members | collection of WorkspaceMembership | Contains exactly one `owner` membership after creation |

**Factory**: `Workspace.Create(name, description, ownerId, timeProvider)` validates via the value
objects and returns either a workspace (with its owner membership added) or a set of field
errors (`name`, `description`). No partially built workspace can exist.

### WorkspaceMembership (module: Workspaces) — part of Workspace aggregate

| Field | Type | Rules |
|-------|------|-------|
| WorkspaceId | UUID → Workspace | Part of identity |
| UserId | UUID → User | Part of identity |
| Role | `WorkspaceRole` | Enum; only `Owner` exists in this release |
| JoinedAt | UTC instant | Equals workspace CreatedAt for the owner |

**State transitions**: none in this release. Membership is created with the workspace and never
changed. Invite/join/remove are future features that will add roles and transitions.

## Relational schema (PostgreSQL)

Created by the initial EF Core migration `InitialWorkspaceFoundation`.

### `users`

| Column | Type | Constraints |
|--------|------|-------------|
| id | uuid | PK |
| display_name | varchar(100) | NOT NULL |
| created_at | timestamptz | NOT NULL |

### `workspaces`

| Column | Type | Constraints |
|--------|------|-------------|
| id | uuid | PK |
| name | varchar(100) | NOT NULL, `CHECK (char_length(btrim(name)) > 0)` |
| description | varchar(500) | NULL |
| owner_id | uuid | NOT NULL, FK → `users(id)` ON DELETE RESTRICT |
| created_at | timestamptz | NOT NULL |
| updated_at | timestamptz | NOT NULL |

### `workspace_memberships`

| Column | Type | Constraints |
|--------|------|-------------|
| workspace_id | uuid | PK part 1, FK → `workspaces(id)` ON DELETE CASCADE |
| user_id | uuid | PK part 2, FK → `users(id)` ON DELETE RESTRICT |
| role | varchar(20) | NOT NULL, `CHECK (role IN ('owner'))` |
| joined_at | timestamptz | NOT NULL |

### Indexes

| Index | Columns | Serves |
|-------|---------|--------|
| `pk_workspace_memberships` | (workspace_id, user_id) | Member check on details (FR-006, FR-010) |
| `ix_workspace_memberships_user_id` | (user_id) | "My workspaces" list (FR-004) |
| `ix_workspaces_owner_id` | (owner_id) | FK lookups; future owner queries |

No other indexes are added. List ordering (`created_at DESC, id DESC`) is satisfied from the
membership-filtered set; any further index requires measured evidence (Principle XII).

The database CHECK constraints duplicate the domain rules on purpose. They are a last line of
defense, not the primary validation.

## Query rules

- **List**: memberships for the current user joined to workspaces, ordered
  `created_at DESC, id DESC` (stable paging), `OFFSET (page-1)*pageSize LIMIT pageSize`, plus a
  total count. Projection only, read with no change tracking.
- **Details**: one query filtered by `workspace_id = :id AND EXISTS membership(:id, :currentUser)`.
  No rows means 404 whether the workspace is missing or the user is not a member (FR-006).

## Transaction boundaries

- **Create workspace**: the workspace row and its owner membership are inserted in a single
  `SaveChanges` call, which EF Core wraps in one database transaction. Any failure rolls back
  both rows (spec edge case: no orphaned workspace).
- Reads use no explicit transaction.

## Seed data (development identity only)

Inserted at startup, only when `DevSync__Auth__Mode=DevHeader`, and only if missing (idempotent).
It is not part of the migration, so a non-development database never contains these users.

| id | display_name |
|----|--------------|
| `0192f5a0-0000-7000-8000-000000000001` | Dev User A |
| `0192f5a0-0000-7000-8000-000000000002` | Dev User B |
| `0192f5a0-0000-7000-8000-000000000003` | Dev User C |
