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
`Identity`) are folders inside each layer.

## Frontend (`src/frontend`)

React + TypeScript + Redux Toolkit. Server state lives in RTK Query, and its cache is reset whenever
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
  [research](../../specs/002-identity-and-permissions/research.md)
- Rules: [.specify/memory/constitution.md](../../.specify/memory/constitution.md)
