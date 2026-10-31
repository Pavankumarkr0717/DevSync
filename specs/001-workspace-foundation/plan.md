# Implementation Plan: Workspace Foundation

**Branch**: `001-workspace-foundation` (no git repository yet) | **Date**: 2026-10-29 |
**Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-workspace-foundation/spec.md`

## Summary

Deliver the DevSync foundation: a locally runnable modular monolith where a development user can
create a workspace (becoming its owner), list their workspaces, and view a workspace's details,
all stored durably in PostgreSQL. The backend is an ASP.NET Core 10 Web API with explicit
Domain / Application / Infrastructure / API projects and EF Core migrations. The frontend is a
React + TypeScript + Redux Toolkit (RTK Query) SPA. Docker Compose runs database, API and web
together. Identity uses a development-only header scheme, with server-side membership checks on
every workspace operation. See [research.md](./research.md) for decisions and alternatives.

## Technical Context

**Language/Version**: C# 14 on .NET 10 LTS (SDK 10.0.402 installed; `global.json` pins
`10.0.400` + `latestFeature`); TypeScript on Node.js 24 LTS (24.21.0 installed)

**Primary Dependencies**: ASP.NET Core 10 (minimal APIs, ProblemDetails, built-in validation,
health checks), EF Core 10.0.x + Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3; React 19.3,
Redux Toolkit 2.13 (RTK Query), react-redux 9.3, React Router 8, Vite 8, TypeScript 6.0 (7.x not yet supported by typescript-eslint; research R10)

**Storage**: PostgreSQL 18 (Docker image `postgres:18-alpine`), EF Core code-first migrations

**Testing**: xUnit v3 4.0.2 (unit); WebApplicationFactory 10.0.14 + Testcontainers.PostgreSql
4.16.0 (API integration on real PostgreSQL); Vitest 5 + React Testing Library 16 + user-event
14 + jest-dom 7 + MSW 3 + jsdom (frontend components)

**Target Platform**: Local developer machine (Windows/macOS/Linux) with Docker; Linux containers.
No cloud target.

**Project Type**: Web application (separate `backend/` and `frontend/`)

**Performance Goals**: None claimed (Principle XII). Correctness first; no benchmark in scope.

**Constraints**: Local only; no SignalR, Redis, RabbitMQ, code execution, external auth, or cloud
deployment; development identity MUST NOT run outside `Development`.

**Scale/Scope**: 3 seeded dev users, 3 API endpoints + 1 dev endpoint + 2 health endpoints,
3 UI pages, 3 tables.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Principle | How this plan complies | Pre | Post |
|---|-----------|------------------------|-----|------|
| I | Incremental, spec-driven | Only create/list/details + scaffolding. `WorkspaceMembership`/`WorkspaceRole` exist because ownership and membership checks need them now, not as stubs for invites. No file, diagram, real-time or execution code. | ✅ | ✅ |
| II | Clean architecture | Domain / Application / Infrastructure / Api projects with inward-only references (R2). Frontend: `app/` (store, router), `features/` (UI + RTK Query endpoints), `shared/`. No microservices. | ✅ | ✅ |
| III | Correctness first | Spec acceptance scenarios map to tests; boundary + domain validation (R7); explicit Problem Details errors (R8); DB-down behavior tested. Concurrency/reconnect N/A (no real-time). | ✅ | ✅ |
| IV | Security by default | All workspace endpoints `RequireAuthorization()`; membership enforced in the query (non-members get 404). Dev identity guarded by environment check (R6). Secrets only in git-ignored `.env`. No code execution. | ✅ | ✅ |
| V | Testability | Unit (domain + application), integration (API + PostgreSQL), component (React) tests — see Testing Strategy. | ✅ | ✅ |
| VI | Real-time correctness | N/A — no real-time features. | — | — |
| VII | Persistence & recoverability | PostgreSQL is authoritative; versioned EF migrations; single-transaction create (data-model.md); restart-persistence integration test. Snapshots N/A. | ✅ | ✅ |
| VIII | Observability | JSON structured logs, correlation ID middleware, `/health/live` + `/health/ready`; no bodies or credentials logged (R9). | ✅ | ✅ |
| IX | Simplicity & cost | Modular monolith + PostgreSQL + Docker Compose only. | ✅ | ✅ |
| X | Developer experience | `.env.example`, README with setup/run/test, quickstart.md, actionable errors. | ✅ | ✅ |
| XI | Dependency discipline | Maintained, first-party-leaning libraries; choices and rejected alternatives documented in research.md; no hand-rolled crypto. | ✅ | ✅ |
| XII | Evidence-based performance | No performance figures claimed anywhere. | ✅ | ✅ |

**Gate result**: PASS. No violations; Complexity Tracking is empty.

## Architecture

```text
Browser ──/api, /health──► web (nginx, SPA) ──proxy──► api (ASP.NET Core) ──► db (PostgreSQL)
   (native dev: Vite dev server proxies the same paths to the API on :5080)

DevSync.Api ─────────► DevSync.Application ─────► DevSync.Domain
     │                        ▲
     └──► DevSync.Infrastructure (EF Core, Npgsql, migrations, dev seed)
```

- **Domain**: `Workspace` aggregate, `WorkspaceMembership`, `WorkspaceRole`, `User`, value
  objects `WorkspaceName` / `WorkspaceDescription`, `Result`/field-error types. No framework
  references.
- **Application**: use cases `CreateWorkspace`, `ListMyWorkspaces`, `GetWorkspaceDetails`;
  `ICurrentUser`; ports `IWorkspaceRepository` (add, list-for-member, get-for-member, save) and
  `IUserDirectory` (exists, list-dev-users); response DTOs. Uses `TimeProvider`.
- **Infrastructure**: `DevSyncDbContext`, entity configurations, migrations, repository
  implementations, dev-user seeder, migration runner, `AddInfrastructure()` registration.
- **Api**: composition root, endpoint groups per module, `DevHeader` authentication handler,
  correlation-ID and request-logging middleware, exception → Problem Details mapping, health
  checks, startup guards.

**Key decisions and trade-offs** (details in research.md):

1. *Four backend projects, not one* (R2): compile-time-enforced boundaries for three extra
   `.csproj` files. Modules stay folders until a second module has real weight.
2. *Repository port in Application* rather than exposing `DbContext`: keeps EF out of use cases
   and makes them unit-testable with fakes. The cost is a small interface; queries remain
   EF projections inside Infrastructure.
3. *404 for non-members* (FR-006): hides workspace existence, at the cost of a less specific
   message for a legitimately confused user.
4. *Dev header identity* (R6): unblocks authorization testing now. The risk (spoofable header) is
   contained by the Development-only startup guard and by the fact that nothing is deployed.
5. *Migrations on startup behind a flag* (R4): zero manual steps locally; switchable later.
6. *Same-origin proxying* (R11): no CORS surface at all.
7. *RTK Query for server state*: no duplicated workspace data in slices; cache reset on user switch.

## Project Structure

### Documentation (this feature)

```text
specs/001-workspace-foundation/
├── spec.md
├── plan.md              # this file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── openapi.yaml     # HTTP contract
│   └── ui-routes.md     # UI routes and states
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks (not created here)
```

### Source Code (repository root)

> **Updated 2026-10-30**: the repository was reorganized into `src/backend`, `src/frontend`,
> `tests/backend`, `tests/frontend`, `docs/` and `.github/`, with `DevSync.sln` and
> `docker-compose.yml`. See [ADR 0001](../../docs/adr/0001-repository-layout.md). The tree below
> is the original plan; layers and modules are unchanged.

```text
global.json                     # repo root: SDK pin + Microsoft.Testing.Platform runner
backend/
├── DevSync.slnx
├── Directory.Build.props        # nullable, warnings-as-errors, analyzers, LangVersion
├── Directory.Packages.props     # central package versions
├── Dockerfile
├── src/
│   ├── DevSync.Domain/
│   │   ├── Common/              # Result, FieldError
│   │   ├── Identity/            # User
│   │   └── Workspaces/          # Workspace, WorkspaceMembership, WorkspaceRole, value objects
│   ├── DevSync.Application/
│   │   ├── Common/              # ICurrentUser, paging types
│   │   ├── Identity/            # IUserDirectory, DTOs
│   │   └── Workspaces/          # use cases, IWorkspaceRepository, DTOs
│   ├── DevSync.Infrastructure/
│   │   ├── Persistence/         # DevSyncDbContext, configurations, Migrations/
│   │   ├── Identity/            # UserDirectory, DevUserSeeder
│   │   └── Workspaces/          # WorkspaceRepository
│   └── DevSync.Api/
│       ├── Program.cs
│       ├── Auth/                # DevHeaderAuthenticationHandler, startup guard
│       ├── Middleware/          # correlation ID, request logging
│       ├── Errors/              # exception handler, Problem Details codes
│       ├── Endpoints/           # WorkspaceEndpoints, DevUserEndpoints
│       └── appsettings*.json    # non-secret defaults only
└── tests/
    ├── DevSync.UnitTests/       # Domain/ and Application/ folders
    └── DevSync.IntegrationTests/# fixtures (Testcontainers), Workspaces/, Health/, Auth/, Errors/

frontend/
├── package.json / package-lock.json
├── vite.config.ts               # dev proxy for /api and /health; Vitest config
├── tsconfig*.json, eslint.config.js
├── Dockerfile, nginx.conf
└── src/
    ├── main.tsx
    ├── app/                     # store.ts, hooks.ts, router.tsx, AppShell.tsx
    ├── shared/
    │   ├── api/                 # baseApi.ts (RTK Query, headers), problem.ts
    │   └── ui/                  # ErrorMessage, Loading, Pager
    ├── features/
    │   ├── devIdentity/         # devIdentitySlice.ts, DevUserPicker.tsx, devUsersApi.ts
    │   └── workspaces/          # workspacesApi.ts, WorkspaceListPage, CreateWorkspacePage, WorkspaceDetailsPage
    └── test/                    # setup.ts, msw handlers, renderWithProviders
    # component tests colocated as *.test.tsx

compose.yaml
.env.example
.gitignore                      # includes .env, bin/, obj/, node_modules/, dist/
README.md
```

**Structure Decision**: A web application with separate `backend/` (.NET solution, four
production projects plus two test projects) and `frontend/` (Vite SPA). Compose and environment
files sit at the repository root.

## Configuration (environment variables)

| Variable | Used by | Example (`.env.example`) | Purpose |
|----------|---------|---------------------------|---------|
| `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` | db, api | `devsync` / `devsync` / `change-me-local-only` | Database credentials (placeholders) |
| `ConnectionStrings__DevSync` | api | composed in `compose.yaml` from the above | EF connection |
| `ASPNETCORE_ENVIRONMENT` | api | `Development` | Environment |
| `DevSync__Auth__Mode` | api | `DevHeader` | Enables dev identity (startup fails outside Development) |
| `DevSync__Database__ApplyMigrationsOnStartup` | api | `true` | Auto-migrate (R4) |
| `DB_PORT` / `API_PORT` / `WEB_PORT` | compose | `5432` / `5080` / `8080` | Host ports, bound to 127.0.0.1 |

For native runs, the API reads the same keys from user-secrets or the shell. `appsettings.json`
contains no connection string with a password.

## Security Considerations

- **Authentication**: `DevHeader` scheme only. A missing, malformed or unknown user ID gives 401.
  Startup throws if `Auth:Mode=DevHeader` and the environment is not `Development`. Dev users are
  seeded only in that mode. This is explicitly **not** production authentication.
- **Authorization**: every `/api/workspaces*` endpoint requires an authenticated user; list and
  details queries filter by membership in SQL, so a non-member never gets data, and a non-member
  and a missing workspace are indistinguishable (404). Ownership comes from the authenticated
  principal, never from the request body.
- **Input handling**: strict JSON binding (malformed → 400); unknown fields ignored, never bound
  to entities (request DTOs are separate from entities, so no over-posting); length and
  control-character rules (R7); default Kestrel body-size limits kept.
- **Data exposure**: Problem Details never include exception messages or stack traces;
  `EnableSensitiveDataLogging` and `EnableDetailedErrors` off by default; logs omit bodies and
  headers (including `X-DevSync-User`; only the resolved user ID is logged).
- **Secrets**: `.env` git-ignored; `.env.example` placeholders only; no secrets in
  `appsettings*.json` or Dockerfiles.
- **Network**: Compose publishes ports on `127.0.0.1` only; the database is not reachable from
  other machines.
- **Containers**: API runs as the image's non-root `app` user; nginx serves static files only.
- **Correlation ID**: client-supplied values are accepted only if they match a strict pattern,
  which prevents log injection.

## Testing Strategy

| Layer | Project/Tool | Covers |
|-------|--------------|--------|
| Unit — domain | `DevSync.UnitTests/Domain` (xUnit) | Name/description trimming, length limits at 0/1/100/101 and 500/501 code points (including multi-byte characters), control-char rejection, owner membership created with workspace, timestamps from `TimeProvider` |
| Unit — application | `DevSync.UnitTests/Application` (xUnit + fakes) | Create maps field errors and saves once; list passes paging; details returns not-found for missing and non-member |
| Integration — API + DB | `DevSync.IntegrationTests` (WebApplicationFactory + Testcontainers PostgreSQL 18) | Every acceptance scenario in US1–US3 over HTTP; 401 for missing/malformed/unknown user; non-member 404 identical to missing; malformed ID → 404; paging defaults, limits and stable order; response shapes match `openapi.yaml`; `Location` header; correlation ID echoed and present in Problem Details; migrations apply to an empty DB; **restart persistence** (new factory, same DB); **atomic create** (failure injected on membership insert leaves no workspace row); DB unreachable → 503 and `/health/ready` unhealthy; dev mode outside Development → startup failure |
| Component — frontend | Vitest + RTL + MSW | Dev-user picker sets header and clears cache; list loading/empty/list/paging/error; create form required/length checks, server field errors shown, pending state, navigation on success; details success / not found / 503 message with correlation ID; unknown route |
| Manual end-to-end | [quickstart.md](./quickstart.md) | Fresh-clone setup through all stories in the browser via Compose |

Test isolation: one PostgreSQL container per integration-test collection; each test creates its
own users and workspaces, so assertions never depend on other tests' data. Every bug fixed during
implementation gets a regression test (Principle V).

Commands: `dotnet test --solution src/backend/DevSync.sln` (Docker must be running) and
`npm test --prefix src/frontend`.

## Phased Implementation

Each phase ends with its tests passing. Phases map to `/speckit-tasks` groups.

1. **Scaffolding**: repo-root `.gitignore`, `.env.example`, `compose.yaml` (db only at first),
   backend solution with four projects + two test projects, central package management,
   `global.json`. API host with JSON logging, correlation-ID middleware, Problem Details +
   exception handler, `/health/live` and `/health/ready`. README skeleton.
   *Exit*: `docker compose up db` plus `dotnet run` gives a healthy `/health/ready`; the health
   integration test passes.
2. **Domain & persistence**: domain entities and value objects with unit tests; `DevSyncDbContext`,
   configurations, `InitialWorkspaceFoundation` migration (and reviewed SQL script); migration
   runner behind the flag. *Exit*: migration integration test on an empty DB.
3. **Development identity**: `DevHeader` handler, environment guard, dev-user seeder,
   `GET /api/dev/users`. *Exit*: auth integration tests (401 cases, guard).
4. **Workspace API (US1 → US2 → US3)**: use cases + repository + endpoints, one story at a time,
   each with its unit and integration tests. *Exit*: all US1–US3 integration scenarios green.
5. **Frontend**: Vite + React + TS + RTK scaffold (run the R10 compatibility check first), store,
   router, base API with headers, dev-user picker, then list, create and details pages per
   [ui-routes.md](./contracts/ui-routes.md) with component tests. *Exit*: `npm test`, `npm run
   lint`, `npm run build` green.
6. **Full-stack Compose & docs**: API and web Dockerfiles, nginx proxy, complete `compose.yaml`,
   README (prerequisites, setup, run, test, troubleshooting, env vars). *Exit*: every
   [quickstart.md](./quickstart.md) scenario passes from a fresh clone (SC-001).

## Complexity Tracking

No constitution violations to justify.
