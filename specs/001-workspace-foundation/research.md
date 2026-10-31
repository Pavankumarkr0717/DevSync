# Research: Workspace Foundation

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-10-29

All Technical Context unknowns are resolved below. Each entry uses Decision / Rationale /
Alternatives considered.

## Environment inspected (2026-10-29)

| Tool | Installed | Notes |
|------|-----------|-------|
| .NET SDK | 10.0.101, 10.0.402 | .NET 10 is the current LTS; ASP.NET Core runtime 10.0.13 present |
| Node.js / npm | 24.21.0 / 10.9.0 | Node 24 is an active LTS line |
| Docker / Compose | 29.8.0 / v5.5.1 | Compose v2+ syntax (`docker compose`) available |
| git | 2.45.1 | Repository is not yet a git repo |
| Repository | `.specify/`, `.claude/`, `specs/` only | No existing application code or conventions to match |

Latest stable registry versions were queried the same day (npm registry, NuGet flat container).

## R1. Backend runtime and framework

- **Decision**: .NET 10 (LTS), ASP.NET Core minimal APIs grouped per module, C# 14. Pin the SDK
  with `global.json` (`10.0.400`, `rollForward: latestFeature`).
- **Rationale**: Installed, LTS, supported through Nov 2028. Minimal APIs with route groups keep
  endpoint code thin and colocated per module; .NET 10 adds built-in minimal-API validation and
  OpenAPI generation, removing two third-party dependencies.
- **Alternatives**: MVC controllers (more ceremony, no benefit at this size); .NET 8 LTS (older,
  shorter remaining support).

## R2. Backend project boundaries

- **Decision**: Four production projects — `DevSync.Domain`, `DevSync.Application`,
  `DevSync.Infrastructure`, `DevSync.Api` — with modules (`Workspaces`, `Identity`) as folders
  inside each. References point inward only: Api → Application/Infrastructure; Infrastructure →
  Application → Domain.
- **Rationale**: Constitution Principle II requires explicit layers. Project references make the
  dependency rule a compile error rather than a convention, at the cost of three extra `.csproj`
  files. Modules as folders (not projects) avoid premature module-per-assembly overhead.
- **Alternatives**: Single project with folders (boundaries unenforced; easy to leak EF types into
  domain); project-per-module-per-layer (8+ projects for one module — unjustified).

## R3. Persistence

- **Decision**: PostgreSQL 18 (`postgres:18-alpine` image), EF Core 10.0.x with
  `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3. Explicit `IEntityTypeConfiguration` classes set
  snake_case table/column names, lengths, and constraints. EF Core migrations, committed to source.
- **Rationale**: Mandated stack; Npgsql provider major tracks EF Core major. Explicit configuration
  avoids a naming-convention plugin dependency (Principle XI).
- **Alternatives**: Dapper + SQL migrations tool (more hand-written code for no gain at this
  size); `EFCore.NamingConventions` (extra dependency for a handful of names).

## R4. Applying migrations

- **Decision**: The API applies pending migrations at startup **only when**
  `DevSync__Database__ApplyMigrationsOnStartup=true` (set in Docker Compose and local dev). The
  initial migration is also checked in as an idempotent SQL script for review.
- **Rationale**: Satisfies "no manual steps" (spec US4 #3) for a single local instance. Gated by
  config so a future deployment can switch to migration bundles without code changes.
- **Alternatives**: Separate migrator container (extra service now); manual
  `dotnet ef database update` (violates SC-001).

## R5. Identifiers and time

- **Decision**: `Guid.CreateVersion7()` (UUIDv7) for workspace and user IDs; `TimeProvider`
  injected for timestamps; all times stored as `timestamptz` in UTC.
- **Rationale**: UUIDv7 has 74 random bits (non-guessable for practical purposes, FR-003) and is
  time-ordered, keeping B-tree inserts local. Authorization never relies on ID secrecy.
  `TimeProvider` is built in and makes time deterministic in tests.
- **Alternatives**: UUIDv4 (random index inserts); serial integers (enumerable, violates FR-003).

## R6. Development identity (spec FR-011)

- **Decision**: A custom ASP.NET Core authentication scheme `DevHeader`. The client sends
  `X-DevSync-User: <user-uuid>`; the handler authenticates only if that user exists in the `users`
  table. Three development users are seeded at startup when the scheme is enabled. The scheme is
  enabled by `DevSync__Auth__Mode=DevHeader`, and **startup fails** if that mode is set while
  `ASPNETCORE_ENVIRONMENT` is not `Development`. A dev-only endpoint lists the seeded users for the
  UI's user picker.
- **Rationale**: Uses the standard authentication pipeline, so endpoints use ordinary
  `RequireAuthorization()` and read the user ID from `ClaimsPrincipal`. Replacing it with real
  authentication later swaps the scheme, not the authorization rules. Lets tests prove non-member
  denial (SC-004).
- **Alternatives**: Local username/password accounts (larger scope; deferred); single implicit
  user (cannot test cross-user authorization).
- **Status**: Planning assumption — awaiting user confirmation (see spec FR-011).

## R7. Validation

- **Decision**: Two layers with one owner each:
  1. *Shape* (malformed JSON, wrong types, query params out of range) — framework binding and
     .NET 10 minimal-API validation → `400 malformed_request` / `validation_failed`.
  2. *Business rules* (name/description content and length) — domain value objects
     (`WorkspaceName`, `WorkspaceDescription`) returning a `Result` with field errors, mapped to
     `400 validation_failed`.
- **Rationale**: Business rules live in one place (domain) and are unit-testable without HTTP.
  No exceptions for expected validation failures.
- **Alternatives**: FluentValidation (extra dependency duplicating domain rules); DataAnnotations
  for business rules (cannot express trimming/code-point rules cleanly).
- **Length unit**: lengths are counted in Unicode code points after trimming, matching
  PostgreSQL `char_length`. Names reject all control characters; descriptions allow `\n`, `\r`,
  `\t` only.

## R8. Error contract

- **Decision**: RFC 9457 Problem Details (`application/problem+json`) via ASP.NET Core
  `AddProblemDetails`, extended with `code` (stable machine-readable string), `correlationId`, and
  for validation `errors: { field: [messages] }`. Codes: `validation_failed` (400),
  `malformed_request` (400), `unauthenticated` (401), `not_found` (404),
  `service_unavailable` (503), `internal_error` (500). A global exception handler maps database
  connectivity failures (`NpgsqlException`, transient) to 503 and everything else to 500 without
  leaking exception details.
- **Rationale**: Standard format with built-in framework support; satisfies FR-008.
- **Alternatives**: Custom error envelope (non-standard, more code).

## R9. Observability

- **Decision**: Built-in `Microsoft.Extensions.Logging` JSON console formatter (scopes enabled).
  Middleware reads `X-Correlation-ID` if it matches `^[A-Za-z0-9-]{1,64}$`, otherwise generates
  one; it is echoed in the response header, placed in a logging scope, and added to every Problem
  Details body. One request log line: method, route template, status, duration, correlation ID,
  user ID. Request/response bodies and headers are never logged; EF sensitive-data logging off.
  Health checks: `/health/live` (process up) and `/health/ready` (includes
  `AddDbContextCheck`, first-party package).
- **Rationale**: Meets Principle VIII without Serilog or a third-party health-check package.
- **Alternatives**: Serilog (richer sinks, unnecessary locally); OpenTelemetry (deferred until a
  collector exists — Principle IX).

## R10. Frontend stack

- **Decision**: Vite 8 + React 19.3 + TypeScript, Redux Toolkit 2.13 with **RTK Query** for server
  state, React Router 8 (declarative routes), plain CSS modules. Exact versions pinned via
  `package-lock.json`.
- **Rationale**: Mandated React/TS/RTK. RTK Query handles caching, loading and error state and
  header injection, so workspace data is not duplicated in hand-written slices. The only
  hand-written slice is `devIdentity` (selected user).
- **Alternatives**: Hand-written thunks (more code, duplicated cache logic); TanStack Query (would
  sideline the mandated Redux Toolkit); a UI component library (unneeded for three pages).
- **Compatibility check**: TypeScript 7.0 (native compiler), Vite 8, Vitest 5, React Router 8 and
  MSW 3 are recent majors. The first frontend task MUST confirm `npm install` reports no peer
  conflicts and `tsc --noEmit`, `eslint` and `vitest` all run; if `typescript-eslint` does not yet
  accept TS 7, pin the newest TypeScript it supports and record it here.
- **Outcome (2026-10-29, T008)**: `typescript-eslint` 8.72.1 declares `typescript: >=4.8.4 <6.1.0`, so
  TypeScript 7.0.2 is not supported. **TypeScript is pinned to 6.0.3.** All other pins installed
  without peer conflicts. MSW 3 renamed `onUnhandledRequest` to `onUnhandledFrame` (used in
  `frontend/src/test/setup.ts`).

## R11. Frontend ↔ API connectivity

- **Decision**: The browser always calls relative `/api/...`. In native dev, Vite's dev-server
  proxy forwards `/api` and `/health` to the API; in Docker Compose, nginx serves the built SPA
  and reverse-proxies the same paths.
- **Rationale**: Same origin in both modes → no CORS configuration and no frontend API-URL
  environment variable.
- **Alternatives**: CORS with configured origins (more config surface, more ways to misconfigure).

## R12. Testing

- **Decision**:
  - Backend unit: xUnit v3 (`xunit.v3` 4.0.2) with built-in `Assert`; hand-written fakes.
  - Backend integration: `Microsoft.AspNetCore.Mvc.Testing` 10.0.14 (`WebApplicationFactory`) +
    `Testcontainers.PostgreSql` 4.16.0 running real PostgreSQL 18 with real migrations.
  - Frontend component: Vitest 5 + jsdom + React Testing Library 16 + user-event 14 + jest-dom 7,
    with MSW 3 mocking the HTTP contract.
- **Rationale**: Real database in integration tests catches constraint, migration and
  transaction behavior an in-memory provider hides. Docker is already required.
- **Alternatives**: EF in-memory/SQLite (diverges from PostgreSQL semantics); FluentAssertions
  (commercial license since v8); Moq (unnecessary for a few fakes); Playwright E2E (deferred —
  quickstart covers manual end-to-end verification).

## R13. Local orchestration

- **Decision**: One `compose.yaml` with `db` (postgres:18-alpine, `pg_isready` healthcheck,
  named volume, port bound to `127.0.0.1`), `api` (multi-stage `sdk:10.0` → `aspnet:10.0`), and
  `web` (multi-stage `node:24-alpine` build → `nginx:alpine`). `.env.example` supplies
  placeholders; real `.env` is git-ignored. Developers can alternatively run only `db` in Compose
  and the API/frontend natively.
- **Rationale**: Principle IX baseline; one command to run everything (SC-001).
- **Alternatives**: .NET Aspire AppHost (extra orchestration concept; Compose is mandated).
