---

description: "Task list for feature 001-workspace-foundation"
---

# Tasks: Workspace Foundation

**Input**: Design documents from `specs/001-workspace-foundation/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/openapi.yaml](./contracts/openapi.yaml),
[contracts/ui-routes.md](./contracts/ui-routes.md), [quickstart.md](./quickstart.md)

**Tests**: Required. The spec (SC-005, SC-006) and constitution Principle V require unit,
integration and component tests. Within each story, write the test tasks first and confirm they
fail before implementing.

**Organization**: Phases follow the user stories. Inside each phase, tasks are grouped under
**Setup / Backend / Persistence / Frontend / Testing / Documentation** headings. Each task ends
with the requirement(s) it serves in parentheses.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel with the other [P] tasks in the same group. It touches different
  files and depends only on tasks listed earlier than that group.
- **[Story]**: US1 create, US2 list, US3 details, US4 run locally (spec.md)
- Paths are relative to the repository root.

## Path Conventions

- Backend: `backend/src/DevSync.{Domain,Application,Infrastructure,Api}/`, tests in
  `backend/tests/DevSync.{UnitTests,IntegrationTests}/`
- Frontend: `frontend/src/`, component tests colocated as `*.test.tsx`

## Scope guard

No task may add real-time messaging, SignalR, Redis, RabbitMQ, queues, code execution, file
editing, diagrams, invites/joins, workspace edit/delete, external auth, or cloud deployment.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Repository skeleton, toolchains, and pinned dependencies.

### Setup

- [X] T001 Create `.gitignore` at the repo root ignoring `.env`, `**/bin/`, `**/obj/`, `**/node_modules/`, `**/dist/`, `**/coverage/`, `**/TestResults/`, and add `.editorconfig` (UTF-8, LF, 4-space C#, 2-space TS/JSON/YAML) (Principle X)
- [X] T002 [P] Create `.env.example` with placeholder values only: `POSTGRES_DB=devsync`, `POSTGRES_USER=devsync`, `POSTGRES_PASSWORD=change-me-local-only`, `ASPNETCORE_ENVIRONMENT=Development`, `DevSync__Auth__Mode=DevHeader`, `DevSync__Database__ApplyMigrationsOnStartup=true`, `DB_PORT=5432`, `API_PORT=5080`, `WEB_PORT=8080`, each with a one-line comment (US4-AS4)
- [X] T003 [P] Create `compose.yaml` with only a `db` service: image `postgres:18-alpine`, env from `.env`, named volume `devsync-db-data`, port `127.0.0.1:${DB_PORT}:5432`, healthcheck `pg_isready -U $POSTGRES_USER -d $POSTGRES_DB` (US4-AS1, Principle IX)
- [X] T004 Create `backend/global.json` (sdk `10.0.400`, `rollForward: latestFeature`), `backend/Directory.Build.props` (`net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest`), and `backend/Directory.Packages.props` (central versions: Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3; Microsoft.EntityFrameworkCore.Design, Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore, Microsoft.AspNetCore.Mvc.Testing 10.0.14; xunit.v3 4.0.2; Testcontainers.PostgreSql 4.16.0; matching xunit runner and Microsoft.NET.Test.Sdk) (research R1, R3, R12)
- [X] T005 Create `backend/DevSync.slnx` with projects `src/DevSync.Domain` (classlib, no package refs), `src/DevSync.Application` (→ Domain), `src/DevSync.Infrastructure` (→ Application; Npgsql provider; EF Design with `PrivateAssets=all`), `src/DevSync.Api` (web; → Application, Infrastructure; HealthChecks.EntityFrameworkCore), `tests/DevSync.UnitTests` (xunit.v3; → Domain, Application), `tests/DevSync.IntegrationTests` (xunit.v3, Mvc.Testing, Testcontainers.PostgreSql; → Api, Infrastructure); verify `dotnet build backend/DevSync.slnx` succeeds with zero warnings (Principle II, research R2)
- [X] T006 [P] Create the local tool manifest `backend/.config/dotnet-tools.json` pinning `dotnet-ef` 10.0.14 (FR-013)
- [X] T007 [P] Scaffold `frontend/` as a Vite React-TS app with pinned exact versions in `frontend/package.json`: react/react-dom 19.3.1, @reduxjs/toolkit 2.13.0, react-redux 9.3.1, react-router 8.5.1; dev: typescript 7.0.2, vite 8.3.5, @vitejs/plugin-react 6.1.2, vitest 5.0.5, jsdom 30.1.3, @testing-library/react 16.3.3, @testing-library/user-event 14.6.8, @testing-library/jest-dom 7.0.1, msw 3.0.3, eslint 10.13.0 + typescript-eslint; scripts `dev`, `build`, `typecheck` (`tsc --noEmit`), `lint`, `test` (`vitest run`); `strict: true` in `frontend/tsconfig.json`; delete template demo assets (FR-016, research R10)
- [X] T008 Run the research R10 compatibility check in `frontend/`: `npm install` reports no peer conflicts and `typecheck`, `lint`, `test` (with one trivial passing test) and `build` all succeed. If `typescript-eslint` rejects TypeScript 7, pin the newest TypeScript it supports and record the outcome under R10 in `specs/001-workspace-foundation/research.md` (Principle XI)

**Checkpoint**: The backend solution builds; the frontend installs, lints, tests and builds;
`docker compose up -d db` reports `healthy`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The API host, error contract, observability, schema, development identity, and
frontend shell that every story needs.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Backend — host, errors, observability

- [X] T009 Create `Result<T>` (success value or a list of `FieldError(string Field, string Message)`) in `backend/src/DevSync.Domain/Common/Result.cs` and `FieldError.cs` (research R7)
- [X] T010 [P] Create `ICurrentUser` (`Guid UserId`) and `PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)` in `backend/src/DevSync.Application/Common/` (FR-004, FR-009)
- [X] T011 [P] Create `backend/src/DevSync.Api/Errors/ErrorCodes.cs` (`validation_failed`, `malformed_request`, `unauthenticated`, `not_found`, `service_unavailable`, `internal_error`), `ProblemDetailsSetup.cs` (configure `AddProblemDetails` to always add `code` and `correlationId` extensions) and `GlobalExceptionHandler.cs` (`IExceptionHandler`: `BadHttpRequestException` → 400 `malformed_request`; `NpgsqlException`, or `InvalidOperationException`/`TimeoutException` wrapping one → 503 `service_unavailable`; anything else → 500 `internal_error`; never include exception messages or stack traces) (FR-008, Principle IV)
- [X] T012 [P] Create `backend/src/DevSync.Api/Middleware/CorrelationIdMiddleware.cs`: accept inbound `X-Correlation-ID` only if it matches `^[A-Za-z0-9-]{1,64}$`, else generate one; store it under a public constant key in `HttpContext.Items`; set the response header; open a logging scope `{ correlationId }` (FR-008, FR-015)
- [X] T013 Create `backend/src/DevSync.Api/Middleware/RequestLoggingMiddleware.cs` logging one structured line per request: method, route template, status code, duration ms, correlationId (from T012's key), userId if authenticated; never bodies or header values (FR-015, Principle VIII)
- [X] T014 Compose `backend/src/DevSync.Api/Program.cs`: JSON console logging with scopes, middleware order (correlation → exception handler → request logging → auth), ProblemDetails, options binding for `DevSync:Auth:Mode` and `DevSync:Database:ApplyMigrationsOnStartup`, `/health/live` (no checks) and `/health/ready` (checks tagged `ready`); `public partial class Program` for tests; non-secret defaults in `appsettings.json`; `Properties/launchSettings.json` http profile on `http://localhost:5080`, environment `Development` (FR-014, FR-015)

### Persistence — domain model, schema, migrations

- [X] T015 [P] Create `User` in `backend/src/DevSync.Domain/Identity/User.cs`: `Id` (Guid), `DisplayName` ("Required, 1–100 code points, trimmed"), `CreatedAt` (UTC) (data-model User)
- [X] T016 [P] Create `WorkspaceRole` (enum, only `Owner`) and value objects `WorkspaceName` ("Trimmed; 1–100 code points; no control characters; duplicates allowed") and `WorkspaceDescription` ("Trimmed; empty → null; ≤ 500 code points; control chars except `\n` `\r` `\t` rejected"), each with a `Create(string?)` factory returning `Result` with field `name`/`description`, in `backend/src/DevSync.Domain/Workspaces/` (FR-001, FR-007, research R7)
- [X] T017 Create the `Workspace` aggregate and `WorkspaceMembership` in `backend/src/DevSync.Domain/Workspaces/`: `Workspace.Create(string? name, string? description, Guid ownerId, TimeProvider time)` returns `Result<Workspace>`, assigns `Guid.CreateVersion7()`, sets `CreatedAt = UpdatedAt`, and adds exactly one `Owner` membership with `JoinedAt = CreatedAt`; no public setters (FR-001–FR-003, data-model)
- [X] T018 Create `DevSyncDbContext` and configurations in `backend/src/DevSync.Infrastructure/Persistence/` (`UserConfiguration.cs`, `WorkspaceConfiguration.cs`, `WorkspaceMembershipConfiguration.cs`) with explicit snake_case names: `users(id uuid PK, display_name varchar(100) NOT NULL, created_at timestamptz NOT NULL)`; `workspaces(id uuid PK, name varchar(100) NOT NULL CHECK (char_length(btrim(name)) > 0), description varchar(500) NULL, owner_id uuid NOT NULL FK → users(id) ON DELETE RESTRICT, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL)`; `workspace_memberships(workspace_id uuid FK → workspaces(id) ON DELETE CASCADE, user_id uuid FK → users(id) ON DELETE RESTRICT, role varchar(20) NOT NULL CHECK (role IN ('owner')), joined_at timestamptz NOT NULL, PK (workspace_id, user_id))`; indexes `ix_workspace_memberships_user_id`, `ix_workspaces_owner_id`; role stored as the lowercase string `owner` (FR-012, data-model)
- [X] T019 Create `backend/src/DevSync.Infrastructure/DependencyInjection.cs` `AddInfrastructure(IConfiguration)`: register `DevSyncDbContext` with Npgsql from `ConnectionStrings:DevSync` (fail at startup with a clear message naming the variable if it is missing); sensitive-data logging and detailed errors off; register `TimeProvider.System`. Wire it into `Program.cs` and add `AddDbContextCheck<DevSyncDbContext>` tagged `ready` (FR-012, FR-014)
- [X] T020 Generate the migration `InitialWorkspaceFoundation` with `dotnet ef migrations add` into `backend/src/DevSync.Infrastructure/Persistence/Migrations/`, plus the idempotent script `backend/src/DevSync.Infrastructure/Persistence/Migrations/InitialWorkspaceFoundation.sql`; review the SQL against every column, constraint and index in data-model.md (FR-013)
- [X] T021 Create `backend/src/DevSync.Infrastructure/Persistence/MigrationRunner.cs`, called from `Program.cs` before the app starts listening, that applies pending migrations only when `DevSync:Database:ApplyMigrationsOnStartup` is `true` (US4-AS3, research R4)

### Backend — development identity

- [X] T022 Create `IUserDirectory` (`ExistsAsync(Guid)`, `GetAsync(Guid)` → `UserRefDto?`, `ListDevUsersAsync()`) and `UserRefDto(Guid Id, string DisplayName)` in `backend/src/DevSync.Application/Identity/` (FR-011)
- [X] T023 Implement `UserDirectory` and `DevUserSeeder` in `backend/src/DevSync.Infrastructure/Identity/`. The seeder idempotently inserts `0192f5a0-0000-7000-8000-000000000001` "Dev User A", `…0002` "Dev User B" and `…0003` "Dev User C", and runs after migrations only when Auth mode is `DevHeader` (FR-011, data-model seed)
- [X] T024 Create `backend/src/DevSync.Api/Auth/DevHeaderAuthenticationHandler.cs`: read `X-DevSync-User`; missing, non-UUID or unknown user (via `IUserDirectory.ExistsAsync`) → fail; success → principal with a `NameIdentifier` claim; override the challenge to return 401 Problem Details with code `unauthenticated`. Add `HttpCurrentUser : ICurrentUser` in `backend/src/DevSync.Api/Auth/HttpCurrentUser.cs` (FR-009, FR-011)
- [X] T025 Create `backend/src/DevSync.Api/Auth/AuthSetup.cs` and wire it into `Program.cs`: register the `DevHeader` scheme and authorization; **throw at startup** if `DevSync:Auth:Mode=DevHeader` and the environment is not `Development`; run `DevUserSeeder` after `MigrationRunner` (FR-011, Principle IV)
- [X] T026 Create `backend/src/DevSync.Api/Endpoints/DevUserEndpoints.cs` mapping `GET /api/dev/users` (anonymous; returns `UserRef[]`) only when Auth mode is `DevHeader`, so it is 404 otherwise (FR-011, contracts/openapi.yaml)

### Testing — backend foundation

- [X] T027 Create the integration-test harness in `backend/tests/DevSync.IntegrationTests/Infrastructure/`: `PostgresFixture.cs` (Testcontainers `postgres:18-alpine`, xUnit collection fixture); `DevSyncApiFactory.cs` (`WebApplicationFactory<Program>`, environment `Development`, connection string from the fixture, migrations on, `DevHeader` mode, optional overrides for `TimeProvider` and EF interceptors); `TestData.cs` (insert a unique user directly via `DevSyncDbContext`); `HttpClientExtensions.cs` (`AsUser(Guid)` sets `X-DevSync-User`) (Principle V)
- [X] T028 [P] Write `backend/tests/DevSync.IntegrationTests/Health/HealthTests.cs` (`/health/live` and `/health/ready` return 200 `Healthy`; with a factory whose connection string points at a closed port and migrations off, `/health/ready` returns 503 `Unhealthy` while `/health/live` stays 200) and `backend/tests/DevSync.IntegrationTests/Persistence/MigrationTests.cs` (on an empty database, the three tables, both CHECK constraints, both FKs with their delete rules, and both named indexes exist, queried from `information_schema`/`pg_indexes`) (FR-013, FR-014, US4-AS2, US4-AS3)
- [X] T029 [P] Write `backend/tests/DevSync.IntegrationTests/Auth/DevIdentityTests.cs`: `GET /api/dev/users` returns the three seeded users; a protected probe request with no header, a non-UUID header, or an unknown UUID returns 401 with `code=unauthenticated` and a `correlationId`; seeding twice creates no duplicates (FR-009, FR-011)
- [X] T030 [P] Write `backend/tests/DevSync.IntegrationTests/Auth/StartupGuardTests.cs`: environment `Production` with Auth mode `DevHeader` fails host startup with a message naming the setting; environment `Production` with no Auth mode makes `/api/dev/users` return 404 (FR-011, Principle IV)
- [X] T031 [P] Write `backend/tests/DevSync.IntegrationTests/Errors/CorrelationIdTests.cs`: a valid inbound `X-Correlation-ID` is echoed; invalid values (with spaces, 65 characters, containing `\r\n`) are replaced with a generated ID; every 4xx/5xx Problem Details body's `correlationId` equals the response header (FR-008, FR-015)

### Frontend — shell, state, test harness

- [X] T032 Create `frontend/src/shared/api/baseApi.ts` (RTK Query `createApi` with `fetchBaseQuery({ baseUrl: '/api' })`, `prepareHeaders` adding `X-DevSync-User` from the devIdentity state, tag type `Workspace`, no endpoints yet) and `frontend/src/shared/api/problem.ts` (parse an RTK Query error into `{ status, title, code, correlationId, fieldErrors }`, tolerating non-JSON bodies) and `frontend/src/shared/api/types.ts` (TypeScript types for every schema in `contracts/openapi.yaml`) (FR-008, FR-016)
- [X] T033 Create `frontend/src/features/devIdentity/devIdentitySlice.ts` (`selectedUserId: string | null`, initialized from `localStorage` key `devsync.devUserId`, with reads and writes in try/catch), `devUsersApi.ts` (`GET /dev/users` via `baseApi.injectEndpoints`), and `DevUserPicker.tsx` (select element; on change, dispatch the selection and `baseApi.util.resetApiState()`) (FR-011, ui-routes Global shell)
- [X] T034 Create `frontend/src/app/store.ts` (devIdentity reducer + `baseApi` reducer/middleware; export `RootState`, `AppDispatch`, and a `makeStore(preloadedState?)` for tests) and `frontend/src/app/hooks.ts` (typed `useAppDispatch`/`useAppSelector`)
- [X] T035 [P] Create `frontend/src/shared/ui/ErrorMessage.tsx` (401 → "Choose a development user to continue"; 503 → "DevSync can't reach its database right now. Try again shortly."; otherwise the Problem `title`; always shows the correlation ID when present; never renders raw bodies) and `frontend/src/shared/ui/Loading.tsx` (ui-routes Error display)
- [X] T036 [P] Configure testing: `frontend/vite.config.ts` (dev proxy `/api` and `/health` → `http://localhost:5080`; Vitest `environment: 'jsdom'`, `setupFiles: ['src/test/setup.ts']`), `frontend/src/test/setup.ts` (jest-dom; MSW server listen/reset/close with `onUnhandledRequest: 'error'`), `frontend/src/test/server.ts`, `frontend/src/test/handlers/devUsers.ts`, and `frontend/src/test/renderWithProviders.tsx` (fresh `makeStore` + `MemoryRouter` with an initial route) (research R11, R12)
- [X] T037 Create `frontend/src/app/AppShell.tsx` (header with app name and `DevUserPicker`; when no user is selected, render "Choose a development user to continue" in place of the outlet and make no workspace requests), `frontend/src/app/NotFoundPage.tsx` ("Page not found" + link to `/workspaces`), `frontend/src/app/router.tsx` (`/` → redirect `/workspaces`; `*` → NotFoundPage), and `frontend/src/main.tsx` (Provider + Router) (ui-routes)
- [X] T038 Write component tests `frontend/src/features/devIdentity/DevUserPicker.test.tsx` (lists users from MSW; selecting stores the ID in state and `localStorage`; the next request carries `X-DevSync-User`, asserted inside the MSW handler; switching users resets the API cache; a throwing `localStorage` does not crash), `frontend/src/app/AppShell.test.tsx` (no user → gate text, and no `/api/workspaces` request reaches MSW), `frontend/src/app/NotFoundPage.test.tsx`, and `frontend/src/shared/ui/ErrorMessage.test.tsx` (401, 503 text plus correlation ID, generic title) (FR-016, Principle V)

**Checkpoint**: The API starts against Compose's `db`, migrates, seeds dev users, and returns
healthy readiness; all Phase 2 integration and component tests pass.

---

## Phase 3: User Story 1 - Create a Workspace (Priority: P1) 🎯 MVP

**Goal**: An identified user creates a workspace and becomes its owner; the workspace is stored
durably.

**Independent Test**: As Dev User A, POST a valid workspace → 201; restart the API → the row
and owner membership still exist unchanged (checked in the DB in this phase; via the API once
US2/US3 exist).

### Testing (write first, confirm they fail)

- [X] T039 [P] [US1] Write domain unit tests in `backend/tests/DevSync.UnitTests/Domain/WorkspaceNameTests.cs`, `WorkspaceDescriptionTests.cs` and `WorkspaceTests.cs`: names are trimmed; length 0 → error, 1 and 100 → ok, 101 → error, counted in code points (100 emoji, each a surrogate pair, is valid; 101 is not); whitespace-only → error; `\u0000`, `\u001B`, `\n` in a name → error; description empty or whitespace → null, 500 → ok, 501 → error, `\n` `\r` `\t` allowed, `\u0007` → error; `Workspace.Create` with a hand-written fixed `TimeProvider` gives `CreatedAt == UpdatedAt == owner.JoinedAt`, exactly one `Owner` membership for `ownerId`, and a UUID version 7 `Id`; invalid name and description together return both field errors (FR-001–FR-003, FR-007)
- [X] T040 [P] [US1] Write `backend/tests/DevSync.UnitTests/Application/CreateWorkspaceHandlerTests.cs` with a hand-written fake `IWorkspaceRepository` and `IUserDirectory`: a valid command adds once and saves once, returns details with owner = `ICurrentUser.UserId` and its display name; an invalid command returns field errors and never calls add or save (FR-001, FR-002, FR-007)
- [X] T041 [P] [US1] Write `backend/tests/DevSync.IntegrationTests/Workspaces/CreateWorkspaceTests.cs`: AS1 → 201, `Location: /api/workspaces/{id}`, body matches `WorkspaceDetails` (all required properties, one `owner` member); AS2 → stored name trimmed; AS3 → `""` and `"   "` give 400 `validation_failed` with `errors.name` and 0 rows; AS4 → 101-char name and 501-char description give 400 with the matching field, 0 rows; missing `name`, `name: null` and `name: 123` → 400; malformed JSON and empty body → 400 `malformed_request`; AS5 → no header → 401, 0 rows; **duplicate names** by the same user → both 201 with distinct ids; **client-supplied identifiers ignored**: a body containing `"id"` and `"ownerId"` values → 201 with a server-generated id ≠ supplied and owner = caller; unknown properties ignored (FR-001–FR-003, FR-007–FR-009, US1-AS1–AS5)
- [X] T042 [P] [US1] Write `backend/tests/DevSync.IntegrationTests/Workspaces/CreateWorkspacePersistenceTests.cs`: **atomic create**: a test `DbCommandInterceptor` throws on the `INSERT INTO workspace_memberships` command → 500 `internal_error` with no exception text, and 0 rows in `workspaces`; **duplicate identifier**: inserting a second workspace with an existing `id` directly through `DevSyncDbContext` throws a unique-violation (`23505`) `DbUpdateException` and leaves the original row unchanged; **DB CHECK backstop**: raw SQL inserting `name = '   '` violates the check constraint; **restart persistence**: create via factory 1, dispose it, start factory 2 on the same database, and the workspace and owner membership rows are identical; **database failure**: with a factory pointed at a closed port, POST → 503 `service_unavailable` with `correlationId` and no exception details (FR-002, FR-003, FR-012, SC-003, spec edge cases)

### Backend

- [X] T043 [US1] Create in `backend/src/DevSync.Application/Workspaces/`: `IWorkspaceRepository` (`AddAsync(Workspace)`, `SaveChangesAsync()`), `WorkspaceDetailsDto(Id, Name, Description, Owner: UserRefDto, CreatedAt, UpdatedAt, Members: MemberDto[])`, `MemberDto(UserId, DisplayName, Role, JoinedAt)`, `CreateWorkspaceCommand(string? Name, string? Description)` and `CreateWorkspaceHandler` (uses `ICurrentUser`, `TimeProvider`, `IUserDirectory`; returns `Result<WorkspaceDetailsDto>`); register handlers with an `AddApplication()` extension in `backend/src/DevSync.Application/DependencyInjection.cs` (FR-001, FR-002)
- [X] T044 [US1] Implement `backend/src/DevSync.Infrastructure/Workspaces/WorkspaceRepository.cs` (`AddAsync`, `SaveChangesAsync` as a single `SaveChanges`, which inserts the workspace and membership in one transaction) and register it in `DependencyInjection.cs` (FR-002, data-model Transaction boundaries)
- [X] T045 [US1] Create `backend/src/DevSync.Api/Endpoints/WorkspaceEndpoints.cs` with a `/api/workspaces` route group that calls `RequireAuthorization()`, and map `POST /`: bind `CreateWorkspaceRequest(string? Name, string? Description)`, call the handler, map field errors to 400 `validation_failed` with camelCase `errors`, return 201 with `Location`; map the group in `Program.cs`; make T041 and T042 pass (FR-001, FR-007, FR-008, FR-009)

### Frontend

- [X] T046 [P] [US1] Write `frontend/src/features/workspaces/CreateWorkspacePage.test.tsx` with handlers in `frontend/src/test/handlers/workspaces.ts`: empty or whitespace-only name blocks submit and sends no request; more than 100 name / 500 description code points shows inline errors; server 400 `errors.name` appears beside the name field; the submit button is disabled while pending; 201 navigates to `/workspaces/{id}`; 503 shows the database message with the correlation ID (FR-016, ui-routes `/workspaces/new`)
- [X] T047 [US1] Add the `createWorkspace` mutation (POST `/workspaces`, invalidates `{ type: 'Workspace', id: 'LIST' }`) in `frontend/src/features/workspaces/workspacesApi.ts` using `baseApi.injectEndpoints` (FR-001)
- [X] T048 [US1] Create `frontend/src/features/workspaces/CreateWorkspacePage.tsx` (name and description fields with labels, client checks counting trimmed code points via `[...value.trim()].length`, server field errors, pending state, navigate on success) and register `/workspaces/new` in `frontend/src/app/router.tsx`; make T046 pass (FR-001, FR-016)

**Checkpoint**: US1 complete. Creation works through the API and UI, and invalid input, database
outages and restarts behave as specified.

---

## Phase 4: User Story 2 - List My Workspaces (Priority: P2)

**Goal**: A user sees only the workspaces they belong to, newest first, in pages.

**Independent Test**: User A creates two workspaces and user B creates one; A's list shows
exactly A's two, newest first, and B's shows only B's.

### Testing (write first, confirm they fail)

- [X] T049 [P] [US2] Write `backend/tests/DevSync.UnitTests/Application/ListMyWorkspacesHandlerTests.cs`: `PageRequest.Create(page, pageSize)` defaults to (1, 20); page < 1 → `errors.page`; pageSize < 1 or > 100 → `errors.pageSize`; the handler passes the current user ID and paging to the repository (FR-004)
- [X] T050 [P] [US2] Write `backend/tests/DevSync.IntegrationTests/Workspaces/ListWorkspacesTests.cs`: AS1 → items carry `id`, `name`, `role: owner`, `createdAt`, newest first; AS2 → a user with none gets `items: []`, `totalCount: 0`, 200; AS3 → other users' workspaces never appear; AS4 → no header → 401; paging: 25 workspaces → page 1 has 20, page 2 has 5, `totalCount: 25`; `pageSize=100` ok; `pageSize=101`, `pageSize=0`, `page=0` → 400 `validation_failed` naming the field; `page=abc` → 400 `malformed_request`; a page past the end → empty items with the correct `totalCount`; **equal `created_at`** (fixed `TimeProvider` override) gives a stable order by `id DESC` across pages; **restart persistence**: list, dispose the factory, new factory on the same DB, list again → identical JSON; **database failure**: closed-port factory → 503 `service_unavailable` with `correlationId` (FR-004, FR-009, FR-012, SC-003, US2-AS1–AS4)

### Backend

- [X] T051 [US2] Add `PageRequest` with `Create(int? page, int? pageSize)` → `Result<PageRequest>` in `backend/src/DevSync.Application/Common/PageRequest.cs`; `WorkspaceSummaryDto(Id, Name, Role, CreatedAt)`, `ListMyWorkspacesQuery` and `ListMyWorkspacesHandler` in `backend/src/DevSync.Application/Workspaces/`; add `ListForMemberAsync(Guid userId, PageRequest page)` → `PagedResult<WorkspaceSummaryDto>` to `IWorkspaceRepository` (FR-004)
- [X] T052 [US2] Implement `ListForMemberAsync` in `backend/src/DevSync.Infrastructure/Workspaces/WorkspaceRepository.cs`: memberships filtered by `user_id`, joined to workspaces, `AsNoTracking`, `ORDER BY created_at DESC, id DESC`, `Skip/Take`, plus a count (FR-004, data-model Query rules)
- [X] T053 [US2] Map `GET /api/workspaces` in `backend/src/DevSync.Api/Endpoints/WorkspaceEndpoints.cs` with `int? page, int? pageSize` query binding (non-integer → 400 `malformed_request` via T011), validation errors → 400 `validation_failed`, returning `WorkspacePage`; make T050 pass (FR-004, FR-007)

### Frontend

- [X] T054 [P] [US2] Write `frontend/src/features/workspaces/WorkspaceListPage.test.tsx` and extend `frontend/src/test/handlers/workspaces.ts`: loading state; empty state "You have no workspaces yet" with a link to `/workspaces/new`; renders name, role and created date in the order returned; Next/Previous appear only when `totalCount > pageSize`, and Next requests `page=2`; 503 shows the database message and correlation ID; switching dev user refetches and shows that user's list (FR-004, FR-016, ui-routes `/workspaces`)
- [X] T055 [US2] Add the `listWorkspaces({ page, pageSize })` query (provides `{ type: 'Workspace', id: 'LIST' }`) to `frontend/src/features/workspaces/workspacesApi.ts` (FR-004)
- [X] T056 [US2] Create `frontend/src/shared/ui/Pager.tsx` and `frontend/src/features/workspaces/WorkspaceListPage.tsx` (with a "New workspace" link and each item linking to `/workspaces/{id}`), and register `/workspaces` in `frontend/src/app/router.tsx`; make T054 pass (FR-004, FR-016)

**Checkpoint**: US1 and US2 work independently; workspaces created in US1 appear in the list
after a restart.

---

## Phase 5: User Story 3 - View Workspace Details (Priority: P3)

**Goal**: A member sees a workspace's details; non-members and invalid identifiers get an
indistinguishable "not found".

**Independent Test**: As the owner → 200 with details; as another user → 404; with a random or
malformed identifier → 404 with an identical body.

### Testing (write first, confirm they fail)

- [X] T057 [P] [US3] Write `backend/tests/DevSync.UnitTests/Application/GetWorkspaceDetailsHandlerTests.cs`: the repository returns details → success; the repository returns null → a not-found result (FR-005, FR-006)
- [X] T058 [P] [US3] Write `backend/tests/DevSync.IntegrationTests/Workspaces/WorkspaceDetailsTests.cs`: AS1 → the owner gets `name`, `description` (and `null` when none), `owner {id, displayName}`, `createdAt`, `updatedAt`, `members` (exactly the owner with role `owner`); AS2 → a non-member gets 404 `not_found`; AS3 → a random valid UUID gets 404; **invalid identifiers** `not-a-uuid`, `123`, a UUID with trailing characters, and `00000000-0000-0000-0000-000000000000` → 404; the non-member, missing and malformed responses are **byte-identical once `correlationId` and `instance` are removed**; no header → 401; **restart persistence**: create, dispose the factory, new factory on the same DB → identical details JSON; **database failure**: closed-port factory → 503 with `correlationId` (FR-005, FR-006, FR-010, FR-012, SC-003, SC-004, US3-AS1–AS3)

### Backend

- [X] T059 [US3] Add `GetWorkspaceDetailsQuery(Guid WorkspaceId)` and `GetWorkspaceDetailsHandler` (returns `WorkspaceDetailsDto` or not-found) in `backend/src/DevSync.Application/Workspaces/`, and add `GetDetailsForMemberAsync(Guid workspaceId, Guid userId)` → `WorkspaceDetailsDto?` to `IWorkspaceRepository` (FR-005, FR-006)
- [X] T060 [US3] Implement `GetDetailsForMemberAsync` in `backend/src/DevSync.Infrastructure/Workspaces/WorkspaceRepository.cs` as one no-tracking projection filtered by `id = :workspaceId AND EXISTS membership(:workspaceId, :userId)`, including the owner display name and members ordered by `joined_at` (FR-006, FR-010, data-model Query rules)
- [X] T061 [US3] Map `GET /api/workspaces/{workspaceId}` in `backend/src/DevSync.Api/Endpoints/WorkspaceEndpoints.cs` with a **string** route parameter: a failed `Guid.TryParse` returns the same 404 `not_found` Problem as a missing workspace; make T058 pass (FR-005, FR-006)

### Frontend

- [X] T062 [P] [US3] Write `frontend/src/features/workspaces/WorkspaceDetailsPage.test.tsx` and extend `frontend/src/test/handlers/workspaces.ts`: loading state; renders name, description or "No description", owner, created date, and members with roles; 404 → "Workspace not found"; 503 → the database message with the correlation ID; end-to-end within MSW: submitting the create form lands on the details page showing the new workspace (FR-005, FR-016, ui-routes `/workspaces/:workspaceId`)
- [X] T063 [US3] Add the `getWorkspace(id)` query (provides `{ type: 'Workspace', id }`) to `frontend/src/features/workspaces/workspacesApi.ts` (FR-005)
- [X] T064 [US3] Create `frontend/src/features/workspaces/WorkspaceDetailsPage.tsx` and register `/workspaces/:workspaceId` in `frontend/src/app/router.tsx`; make T062 pass (FR-005, FR-016)

**Checkpoint**: All three product stories work in the browser and over the API.

---

## Phase 6: User Story 4 - Run DevSync Locally (Priority: P1, developer-facing)

**Goal**: A developer goes from fresh clone to the full running stack with documented commands.
Phases 1–2 already deliver the database, migrations and health checks; this phase adds the
full-stack containers and documentation.

**Independent Test**: On a clean checkout, following only the README, `docker compose up --build`
serves the UI at `http://localhost:8080` and `/health/ready` returns `Healthy`.

### Setup

- [X] T065 [P] [US4] Create `backend/Dockerfile` (multi-stage `mcr.microsoft.com/dotnet/sdk:10.0` publish → `mcr.microsoft.com/dotnet/aspnet:10.0`, runs as the non-root `app` user, `ASPNETCORE_HTTP_PORTS=8080`) and `backend/.dockerignore` (bin, obj, tests' TestResults) (US4-AS1, Principle IV)
- [X] T066 [P] [US4] Create `frontend/Dockerfile` (multi-stage `node:24-alpine` `npm ci && npm run build` → `nginx:alpine`), `frontend/nginx.conf` (SPA fallback `try_files $uri /index.html`; proxy `/api/` and `/health/` to `http://api:8080`; pass `X-DevSync-User` and `X-Correlation-ID` through) and `frontend/.dockerignore` (US4-AS1, research R11)
- [X] T067 [US4] Extend `compose.yaml` with `api` (build `./backend`; `ConnectionStrings__DevSync=Host=db;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}`; `ASPNETCORE_ENVIRONMENT`, `DevSync__Auth__Mode` and `DevSync__Database__ApplyMigrationsOnStartup` from `.env`; `depends_on: db: condition: service_healthy`; port `127.0.0.1:${API_PORT}:8080`) and `web` (build `./frontend`; port `127.0.0.1:${WEB_PORT}:80`; `depends_on: api`). Verify `docker compose up --build -d` gives `/health/ready` `Healthy` through `http://localhost:8080` (US4-AS1, US4-AS2, SC-001)

### Documentation

- [X] T068 [US4] Write `README.md` at the repo root: what DevSync is and what this milestone delivers (and explicitly does not); prerequisites (Docker with Compose v2+, .NET SDK 10.0.4xx, Node.js 24 LTS); setup (`cp .env.example .env`); run everything (`docker compose up --build -d`, URLs); native development (`docker compose up -d db`, `dotnet user-secrets set ConnectionStrings:DevSync ...` in `backend/src/DevSync.Api`, `dotnet run`, `npm run dev`); dev users table with IDs and the warning that dev identity is not authentication; environment variable table; test commands (Docker required for integration tests); health endpoints; troubleshooting (port in use, Docker not running, reset with `docker compose down -v`); repository layout; links to `specs/001-workspace-foundation/` and the constitution (US4-AS1, US4-AS5, Principle X)

**Checkpoint**: The README alone is sufficient to run and test DevSync locally.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Cross-story verification and evidence (constitution Governance).

### Testing

- [X] T069 [P] Write `backend/tests/DevSync.IntegrationTests/Observability/LoggingTests.cs` with a capturing `ILoggerProvider` registered in the factory: after a create request with a distinctive name and description, a list, and a 404, the log output contains a `correlationId` and `userId` for each request, and **never** contains the request body text or the raw `X-DevSync-User` header value (FR-015, Principle VIII)
- [X] T070 [P] Create `specs/001-workspace-foundation/checklists/traceability.md` mapping every acceptance scenario (US1-AS1 … US4-AS5), functional requirement FR-001–FR-016 and success criterion SC-003–SC-006 to the test(s) that verify it; add tests for any gap found (SC-006)
- [X] T071 Run the full automated suite and fix all failures and warnings: `dotnet build backend/DevSync.slnx` (zero warnings), `dotnet test backend/DevSync.slnx`, and in `frontend/` `npm run typecheck`, `npm run lint`, `npm test`, `npm run build` (SC-006, US4-AS5)
- [X] T072 Security pass: confirm `.env` is git-ignored and that no real password, token or connection string with a password appears in tracked files (`appsettings*.json`, Dockerfiles, `compose.yaml` literals, frontend source); confirm Compose ports bind to `127.0.0.1`; confirm the API container runs as non-root (`docker compose exec api id`); record the findings in `specs/001-workspace-foundation/verification.md` (Principle IV)

### Documentation & end-to-end verification

- [X] T073 Final end-to-end verification from a clean state: `docker compose down -v`, delete `.env`, then follow `README.md` and run every scenario V1–V11 in `specs/001-workspace-foundation/quickstart.md` against the Compose stack, in the browser for V2/V5–V8 and with curl for the rest. Record the date, tool versions, each scenario's pass/fail and any deviations in `specs/001-workspace-foundation/verification.md`; fix the README or code for any failure and re-run the affected scenarios (SC-001–SC-006, constitution Governance: verification evidence)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none. T005 needs T004; T008 needs T007.
- **Foundational (Phase 2)**: needs Setup. Blocks every story. Internal order: T009 → (T010, T011, T012) → T013 → T014; T015/T016 → T017 → T018 → T019 → T020 → T021; T022 → T023 → T024 → T025 → T026; T027 needs T021 and T026; T028–T031 need T027. Frontend: T032 → T033 → T034 → (T035, T036) → T037 → T038. The frontend track (T032–T038) needs only Setup and can run alongside the backend track.
- **US1 (Phase 3)**: needs Foundational.
- **US2 (Phase 4)**: needs Foundational. Its tests create data through `POST` (US1 backend T043–T045) or via `TestData` inserts; using `TestData` keeps US2 independent of US1.
- **US3 (Phase 5)**: needs Foundational; the same note as US2 applies. T062's create-to-details flow test needs T048.
- **US4 (Phase 6)**: T065 needs the backend to build; T066–T067 need the frontend pages (US1–US3) to be useful; T068 can be drafted any time and finalized last.
- **Polish (Phase 7)**: needs all stories. T073 is last.

### Shared files (forces sequential work across stories)

- `backend/src/DevSync.Api/Endpoints/WorkspaceEndpoints.cs`: T045 → T053 → T061
- `backend/src/DevSync.Infrastructure/Workspaces/WorkspaceRepository.cs`: T044 → T052 → T060
- `backend/src/DevSync.Application/Workspaces/IWorkspaceRepository.cs`: T043 → T051 → T059
- `frontend/src/features/workspaces/workspacesApi.ts`: T047 → T055 → T063
- `frontend/src/app/router.tsx`: T037 → T048 → T056 → T064
- `frontend/src/test/handlers/workspaces.ts`: T046 → T054 → T062

So stories can be developed in parallel only on separate branches, merging these files in order.
The default is sequential: US1 → US2 → US3.

### Within Each User Story

- Tests are written first and fail → application → infrastructure → endpoint → frontend API →
  page.
- Tests marked [P] within a story touch separate files and can be written together.

---

## Parallel Examples

### Phase 2

```text
After T009: T010, T011, T012 together (Application/Common, Api/Errors, Api/Middleware)
T015 and T016 together (Domain/Identity, Domain/Workspaces value objects)
After T027: T028, T029, T030, T031 together (separate test files)
Backend track (T009–T031) alongside the frontend track (T032–T038)
```

### User Story 1

```text
T039 Domain unit tests          (backend/tests/DevSync.UnitTests/Domain/)
T040 Application unit tests     (backend/tests/DevSync.UnitTests/Application/)
T041 API integration tests      (backend/tests/DevSync.IntegrationTests/Workspaces/CreateWorkspaceTests.cs)
T042 Persistence tests          (backend/tests/DevSync.IntegrationTests/Workspaces/CreateWorkspacePersistenceTests.cs)
T046 Create page component test (frontend/src/features/workspaces/CreateWorkspacePage.test.tsx)
```

### User Story 2 / 3

```text
US2: T049 + T050 + T054 together
US3: T057 + T058 + T062 together
```

---

## Implementation Strategy

### MVP First (US1 + the US4 basics)

1. Phase 1 Setup → Phase 2 Foundational (the API runs, migrates, seeds, and reports healthy)
2. Phase 3 US1 → **stop and validate**: create via API and UI; restart persists
3. Demo with `docker compose up -d db` + native API + Vite

### Incremental Delivery

1. + US2 (list) → validate independently
2. + US3 (details) → validate independently, including non-member 404
3. + US4 (full Compose, README) → a fresh clone runs with one command
4. Polish → traceability, full suite, security pass, final end-to-end verification evidence

---

## Repository layout change (2026-10-30)

File paths in the tasks above are the original layout. The repository was later reorganized (no
behavior change): `backend/src/X` → `src/backend/X`, `backend/tests/X` → `tests/backend/X`,
`frontend/` → `src/frontend/`, frontend `*.test.tsx` and `src/test/*` → `tests/frontend/`
(helpers in `tests/frontend/support/`), `DevSync.slnx` → `src/backend/DevSync.sln`,
`compose.yaml` → `docker-compose.yml`, `Directory.*.props` and `.config/` → repo root.
See [ADR 0001](../../docs/adr/0001-repository-layout.md).

## Implementation notes (2026-10-29)

All tasks are complete; evidence is in [verification.md](./verification.md) and
[checklists/traceability.md](./checklists/traceability.md). Deviations from the task text:

- **T004**: `global.json` lives at the repo root (not `backend/`), so `dotnet test` from the root
  picks up `"test": { "runner": "Microsoft.Testing.Platform" }`, which the .NET 10 SDK requires
  for xUnit v3. Test command: `dotnet test --solution backend/DevSync.slnx`.
- **T007/T008**: TypeScript is pinned to 6.0.3 (`typescript-eslint` does not accept 7.x yet), as
  research R10 anticipated.
- **T020**: migrations are created from `DevSync.Infrastructure` using a design-time factory, so
  the API project does not need `Microsoft.EntityFrameworkCore.Design`.
- **T033**: instead of `resetApiState()` on user change, workspace queries include the user id in
  their cache key, so one user's cached data can never be shown to another.
- **T036**: MSW 3 uses `onUnhandledFrame` (not `onUnhandledRequest`).
- **T069**: the user id is logged on purpose (it equals the header value), so the test asserts that
  the header *name* and the request content never appear in logs.
- **T073**: found and fixed a defect (database shutdown returned 500 instead of 503), with a
  regression test `Errors/DatabaseOutageTests.cs`. See verification.md.

## Notes

- Every task names its files and the requirement it serves; constraints from data-model.md are
  quoted so they are not decided at implementation time.
- A test that fails for an unexpected reason is a bug: fix it and keep the test (Principle V).
- Stop at any checkpoint to validate the story on its own.
- Out of scope (see spec): real-time features, file editing, diagrams, code execution, invites,
  workspace edit/delete, external auth, queues/caches, and cloud deployment.
