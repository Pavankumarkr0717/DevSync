# Architecture Overview

DevSync is a **modular monolith**: one ASP.NET Core API, one PostgreSQL database, and one
React single-page app, run together locally with Docker Compose. More infrastructure (caches,
brokers, cloud hosting) is added only through an approved specification (constitution
Principle IX).

```text
Browser ──/api, /health──► web (nginx + SPA) ──proxy──► api (ASP.NET Core 10) ──► db (PostgreSQL 18)
          (dev: the Vite dev server proxies the same paths to the API on :5080)
```

## Backend layers (`src/backend`)

```text
DevSync.Api ───────► DevSync.Application ─────► DevSync.Domain
     │                        ▲
     └──► DevSync.Infrastructure (EF Core, Npgsql, migrations, ASP.NET Core Identity stores)
```

| Project | Responsibility | May reference |
|---------|----------------|---------------|
| `DevSync.Domain` | Entities, value objects and invariants. No frameworks. | — |
| `DevSync.Application` | Use cases, ports (`IWorkspaceRepository`, `IUserDirectory`), DTOs | Domain |
| `DevSync.Infrastructure` | EF Core `DbContext`, migrations, repository implementations | Application |
| `DevSync.Api` | HTTP endpoints, authentication, Problem Details, logging, health checks, composition root | Application, Infrastructure |

Project references enforce the dependency direction at compile time. Modules (`Workspaces`,
`Identity`, `Auth`, `Files`) are folders inside each layer.

## Frontend (`src/frontend`)

React + TypeScript + Redux Toolkit, styled with the tokens and components in
[docs/design/design-system.md](../design/design-system.md). Server state lives in RTK Query, and its cache is reset whenever
the signed-in account changes. `app/` holds the store, router and shell, `features/` the UI and
endpoints per feature, and `shared/` the API client, error parsing and UI primitives.

## Identity and authorization (feature 002)

```text
Sign-in:  POST /api/auth/login ─► Identity UserManager (PBKDF2) ─► session row + hashed refresh token
          ◄─ { 15-min JWT access token (sub, sid) }  +  Set-Cookie devsync_rt (HttpOnly, SameSite=Strict, Path=/api/auth)

Request:  Bearer JWT ─► JwtBearer (signature, issuer, audience, lifetime)
                     ─► session still active? (one PK read; immediate revocation)
                     ─► fallback policy: authenticated
                     ─► RequireWorkspace(op): role from DB ─► none: 404 · not allowed: 403 (IAuthorizationService → WorkspacePermissions)

Renewal:  POST /api/auth/refresh (cookie) ─► rotate; reuse within 10 s = 409 retry; later = revoke session
```

- The permission table (`WorkspacePermissions`) is pure domain code, the only place roles are
  interpreted.
- The SPA keeps the access token in Redux memory. A single-flight, cross-tab renewal runs on
  `token_expired`.
- Details: [ADR 0002](../adr/0002-authentication-design.md).

## Project files and editing (feature 003)

```text
Tree change:  POST/DELETE …/items… ─► RequireWorkspace(EditFiles | DeleteFiles)
              ─► BEGIN; SELECT … FROM workspaces WHERE id = @w FOR UPDATE      (one structure change at a time per workspace)
              ─► load all workspace_items ─► ProjectTree validates (names, extension, duplicates, depth, path, count, cycles)
              ─► INSERT / UPDATE / DELETE (folder subtree via ON DELETE CASCADE) ─► COMMIT

Save:         PUT …/files/{itemId}/content   body = raw text, If-Match: "7"
              ─► RequireWorkspace(EditFiles) ─► strict UTF-8, no NUL, ≤ 1 MiB
              ─► UPDATE file_contents SET …, revision = revision + 1 WHERE item_id = @i AND revision = 7   (compare-and-swap)
              ─► 1 row: 200 + ETag "8"   ·   0 rows: 412 revision_conflict (currentRevision) or 404 if deleted

Browser:      Monaco model per file (text, undo, cursor) ─► AutosaveController (1 s pause / 10 s max, one in flight)
              ─► RTK Query mutation ─► status in Redux (saved / dirty / saving / failed + reason)
              unsaved text also kept as a per-user draft in localStorage (offered back; never authoritative)
```

- Files are rows, not host paths. Items are addressed by `(workspaceId, itemId)` only.
- Monaco is bundled locally, lazy-loaded, with syntax tokenizers only (no language services).
- Details: [ADR 0003](../adr/0003-project-file-storage.md).

## System design canvas (feature 004)

```text
Save:     PUT …/diagrams/{diagramId}/document   body = JSON document, If-Match: "7"
          ─► RequireWorkspace(EditDiagrams) ─► bounded read (≤ 1 MiB) ─► strict JSON parse (no unknown or duplicate properties)
          ─► schemaVersion == 1 ─► DiagramValidator (types, edge references, self/duplicate edges, ids, text, ranges; ≤ 50 problems)
          ─► canonical re-serialization ─► UPDATE diagrams SET document = @doc::jsonb, counts, revision = revision + 1
                                            WHERE id = @d AND workspace_id = @w AND revision = 7          (compare-and-swap)
          ─► 200 + ETag "8"   ·   400 invalid_diagram (problems)   ·   412 revision_conflict   ·   404 if deleted

Read:     GET …/diagrams/{diagramId} ─► RequireWorkspace(Read) ─► DiagramDocumentUpgrader v(n) → v(current), never written back
          ─► a newer stored version: document = null + unsupportedSchemaVersion (PUT needs ?replaceUnsupported=true)

Browser:  React Flow (lazy chunk) owns drag positions, selection and the live viewport
          ─► commit points (drag stop, resize end, connect, delete, panel edit) ─► diagramEditor slice (entity adapters)
          ─► canonical JSON ─► the 003 AutosaveController and drafts ─► RTK Query mutation
```

- One `jsonb` document per diagram, with node and edge counts mirrored for the list. Nothing is
  normalized into node or edge tables.
- The server validator is authoritative; the client's connection rules and field limits are for
  instant feedback only.
- Details: [ADR 0004](../adr/0004-diagram-document-storage.md). The visual design is in
  [docs/design/design-system.md](../design/design-system.md).

## Cross-cutting rules

- **Authorization** is checked on the server for every workspace operation, using the caller's
  current role read from the database. A non-member gets the same 404 as for a workspace that
  doesn't exist; a member without the permission gets 403.
- **Errors** use RFC 9457 Problem Details with a stable `code` and a `correlationId`.
- **Observability**: JSON logs, an `X-Correlation-ID` header on every response, and
  `/health/live` + `/health/ready`.
- **Persistence**: PostgreSQL is authoritative; schema changes go through EF Core migrations.

## Where to read more

- Decisions: [docs/adr/](../adr/)
- Feature design: [001 plan](../../specs/001-workspace-foundation/plan.md) and
  [research](../../specs/001-workspace-foundation/research.md);
  [002 plan](../../specs/002-identity-and-permissions/plan.md) and
  [research](../../specs/002-identity-and-permissions/research.md);
  [003 plan](../../specs/003-file-explorer-editor/plan.md) and
  [research](../../specs/003-file-explorer-editor/research.md);
  [004 plan](../../specs/004-system-design-canvas/plan.md) and
  [research](../../specs/004-system-design-canvas/research.md)
- Design system: [docs/design/design-system.md](../design/design-system.md)
- Rules: [.specify/memory/constitution.md](../../.specify/memory/constitution.md)
