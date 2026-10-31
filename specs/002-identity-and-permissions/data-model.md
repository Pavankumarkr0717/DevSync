# Data Model: Identity and Workspace Permissions

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md) | **Date**: 2026-10-30

Changes relative to [001 data model](../001-workspace-foundation/data-model.md). All times are
`timestamptz` UTC. Names are snake_case and configured explicitly, as in 001.

## Domain model

### WorkspaceRole (extended)

`Owner | Editor | Viewer`, stored as `'owner' | 'editor' | 'viewer'`.

### WorkspacePermissions (new, pure)

| Operation | Owner | Editor | Viewer |
|-----------|:-----:|:------:|:------:|
| `Read` (details, members, own invitations view) | ✓ | ✓ | ✓ |
| `EditDetails` (name, description) | ✓ | ✓ | ✗ |
| `ViewMemberEmails` | ✓ | ✗ | ✗ |
| `ManageMembers` (invite, revoke invitation, change role, remove) | ✓ | ✗ | ✗ |

Non-members never reach this table; they get "not found" first.

### Workspace (extended aggregate)

New behaviour, with existing 001 rules unchanged:
- `UpdateDetails(name, description, time)`: same `WorkspaceName` / `WorkspaceDescription`
  validation as create. Sets `UpdatedAt`.
- `ChangeMemberRole(userId, role, time)`: `role ∈ {Editor, Viewer}`. Target must be a non-owner
  member. Errors: `cannot_change_owner`, `member_not_found`.
- `RemoveMember(userId)`: target must be a non-owner member. Errors: `cannot_remove_owner`,
  `member_not_found`.
- `AddMember(userId, role, time)`: used only by invitation acceptance; `role ∈ {Editor, Viewer}`.
  Error: `already_member`.
- **Invariant**: exactly one `Owner` membership, equal to `OwnerId`, and immutable (spec FR-023).

### Invitation (new aggregate)

| Field | Rules |
|-------|-------|
| Id | UUIDv7 |
| WorkspaceId | required |
| Email | Trimmed, ≤ 254 chars, syntactically valid. Stored as entered plus `NormalizedEmail` (upper-invariant, matching Identity normalization) |
| Role | `Editor` or `Viewer` only |
| InvitedBy | Owner's user id |
| Status | `Pending`, `Accepted`, `Declined`, `Revoked` or `Expired` |
| CreatedAt / ExpiresAt | `ExpiresAt = CreatedAt + 7 days` |
| RespondedAt | Set on accept, decline or revoke |
| AcceptedUserId | Set on accept |

State transitions (all others are rejected):

```text
Pending ──accept (by account with matching normalized email, before ExpiresAt)──► Accepted
Pending ──decline (same matching account)──────────────────────────────────────► Declined
Pending ──revoke (workspace Owner)──────────────────────────────────────────────► Revoked
Pending ──any touch at/after ExpiresAt────────────────────────────────────────► Expired
```

Creation rules (checked in the application layer with the repository):
- The email must not equal the Owner's own email → `cannot_invite_self`.
- The email must not belong to a current member → `already_member`. The Owner can see member
  emails, so this reveals nothing new.
- There must be no other pending, unexpired invitation for (workspace, normalized email) →
  `invitation_exists`.
- There can be at most 50 pending invitations per workspace → `too_many_invitations`.

### Session (new aggregate, in Domain/Identity)

| Field | Rules |
|-------|-------|
| Id | UUIDv7; the `sid` claim |
| UserId | required |
| CreatedAt | sign-in time |
| LastRefreshedAt | updated on every rotation |
| IdleExpiresAt | `min(now + 7d, AbsoluteExpiresAt)`, slid on rotation |
| AbsoluteExpiresAt | `CreatedAt + 30d`, never extended |
| RevokedAt / RevokedReason | `logout`, `logout_all` or `refresh_token_reuse` |

`IsActive(now) = RevokedAt is null && now < IdleExpiresAt && now < AbsoluteExpiresAt`.

### RefreshToken (child of Session)

| Field | Rules |
|-------|-------|
| Id | UUIDv7 |
| SessionId | required |
| TokenHash | SHA-256 of the token, 32 bytes, unique |
| IssuedAt | |
| UsedAt | null until rotated |
| ReplacedById | the token issued by that rotation |

Reuse classification at refresh time (research R3): unused → rotate; used less than 10 s ago →
conflict; used 10 s or more ago → revoke the session.

## Relational schema changes

Migration **`IdentityAndPermissions`** (single migration, in order):

1. **Data reset (spec FR-030)**: `DELETE FROM workspace_memberships; DELETE FROM workspaces;
   DELETE FROM users;` with row counts reported via `RAISE NOTICE`.
2. **`users`** becomes the Identity user table:

| Column | Type | Constraints |
|--------|------|-------------|
| id | uuid | PK (existing) |
| display_name | varchar(100) | NOT NULL (existing) |
| created_at | timestamptz | NOT NULL (existing) |
| user_name | varchar(256) | NOT NULL |
| normalized_user_name | varchar(256) | NOT NULL, **unique** `ux_users_normalized_user_name` |
| email | varchar(254) | NOT NULL |
| normalized_email | varchar(254) | NOT NULL, **unique** `ux_users_normalized_email` |
| email_confirmed | boolean | NOT NULL default false (unused; no email delivery) |
| password_hash | text | NOT NULL |
| security_stamp | text | NOT NULL |
| concurrency_stamp | text | NOT NULL |
| lockout_enabled | boolean | NOT NULL default false |
| lockout_end | timestamptz | NULL (unused, see R6) |
| access_failed_count | integer | NOT NULL default 0 |
| phone_number, phone_number_confirmed, two_factor_enabled | — | **ignored** in the EF model (no columns) |

3. **`workspace_memberships`**:
   - Replace `ck_workspace_memberships_role` with `CHECK (role IN ('owner','editor','viewer'))`.
   - Add `ux_workspace_memberships_one_owner` UNIQUE (workspace_id) `WHERE role = 'owner'`.
   - The existing PK (workspace_id, user_id), FKs and `ix_workspace_memberships_user_id` are
     unchanged.
4. **`sessions`** (new):

| Column | Type | Constraints |
|--------|------|-------------|
| id | uuid | PK |
| user_id | uuid | NOT NULL, FK → users ON DELETE CASCADE |
| created_at, last_refreshed_at, idle_expires_at, absolute_expires_at | timestamptz | NOT NULL |
| revoked_at | timestamptz | NULL |
| revoked_reason | varchar(32) | NULL, CHECK in (`logout`,`logout_all`,`refresh_token_reuse`) |

Index: `ix_sessions_user_id_active` on (user_id) `WHERE revoked_at IS NULL` (sign-out
everywhere).

5. **`refresh_tokens`** (new):

| Column | Type | Constraints |
|--------|------|-------------|
| id | uuid | PK |
| session_id | uuid | NOT NULL, FK → sessions ON DELETE CASCADE |
| token_hash | bytea | NOT NULL, **unique** `ux_refresh_tokens_token_hash`, CHECK length = 32 |
| issued_at | timestamptz | NOT NULL |
| used_at | timestamptz | NULL |
| replaced_by_id | uuid | NULL, FK → refresh_tokens ON DELETE SET NULL |

Index: `ix_refresh_tokens_session_id`.

6. **`workspace_invitations`** (new):

| Column | Type | Constraints |
|--------|------|-------------|
| id | uuid | PK |
| workspace_id | uuid | NOT NULL, FK → workspaces ON DELETE CASCADE |
| email | varchar(254) | NOT NULL |
| normalized_email | varchar(254) | NOT NULL |
| role | varchar(20) | NOT NULL, CHECK in (`editor`,`viewer`) |
| invited_by_user_id | uuid | NOT NULL, FK → users ON DELETE RESTRICT |
| status | varchar(16) | NOT NULL, CHECK in (`pending`,`accepted`,`declined`,`revoked`,`expired`) |
| created_at, expires_at | timestamptz | NOT NULL, CHECK expires_at > created_at |
| responded_at | timestamptz | NULL |
| accepted_user_id | uuid | NULL, FK → users ON DELETE SET NULL |

Indexes:
- `ux_workspace_invitations_pending` UNIQUE (workspace_id, normalized_email) `WHERE status = 'pending'`
- `ix_workspace_invitations_pending_email` (normalized_email) `WHERE status = 'pending'` (invitee
  inbox)

Before inserting, any pending invitation that has already expired is marked `expired` in the
same transaction, so the partial unique index never blocks a legitimate re-invite.

*Added during implementation (2026-10-30):* plain foreign-key indexes
`ix_refresh_tokens_replaced_by_id`, `ix_workspace_invitations_workspace_id`,
`ix_workspace_invitations_invited_by_user_id` and `ix_workspace_invitations_accepted_user_id`.
Without them, cascade and restrict checks scan the table. These are named explicitly instead of
EF Core's default PascalCase names.

## Transaction boundaries

| Operation | One `SaveChanges` / transaction contains |
|-----------|------------------------------------------|
| Register | user insert (Identity) → then session + first refresh token (second save; a failure leaves an account with no session, and the person can just sign in) |
| Sign-in | session + first refresh token |
| Refresh | conditional `UPDATE` marking old token used → insert new token + slide session (one transaction) |
| Reuse detected | session revoke |
| Logout-all | bulk `UPDATE sessions SET revoked_at` for the user |
| Create workspace | workspace + owner membership (unchanged from 001) |
| Accept invitation | membership insert + invitation status update |
| Change role / remove member | single membership update/delete |

## Query rules

- **Bearer validation**: `SELECT revoked_at, idle_expires_at, absolute_expires_at FROM sessions
  WHERE id = :sid` (PK).
- **Workspace access**: `SELECT role FROM workspace_memberships WHERE workspace_id = :w AND
  user_id = :u` (PK).
- **Invitee inbox**: pending, unexpired invitations by `normalized_email`, joined to workspace
  name and inviter display name.
- **Member emails**: included in details and member responses only when the caller's role allows
  `ViewMemberEmails`.
