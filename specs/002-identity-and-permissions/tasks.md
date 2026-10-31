---

description: "Task list for feature 002-identity-and-permissions"
---

# Tasks: Identity and Workspace Permissions

**Input**: Design documents from `specs/002-identity-and-permissions/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/openapi.yaml](./contracts/openapi.yaml),
[contracts/ui-routes.md](./contracts/ui-routes.md), [quickstart.md](./quickstart.md)

**Tests**: Required (spec SC-002–SC-008, NFR-003, constitution V). In each story, write the
tests first and confirm they fail.

**Organization**: Phases follow the spec's user stories. Section headings carry the requested
**Group 1–9** labels:

| Group | Where |
|-------|-------|
| 1 Database & identity foundation | Phase 2 |
| 2 Registration & login APIs | Phase 3 (US1) |
| 3 Access & refresh token lifecycle | Phase 2 (validation core), Phase 4 (US2) |
| 4 Workspace ownership migration | Phase 2 (migration), Phase 5 (US3) |
| 5 Membership & RBAC | Phase 2 (domain), Phase 6 (US4) |
| 6 Backend authorization | Phase 2 (core), Phase 7 (US5) |
| 7 Frontend auth & protected routes | Phases 3–7 (per story) |
| 8 Security & regression testing | Per story + Phase 8 |
| 9 Documentation | Phase 8 |

Every task ends with **Done when:** (completion condition) and the requirement ids it serves.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel with the other [P] tasks in the same group: different files, and
  dependent only on earlier groups.
- **[Story]**: US1 register/sign in · US2 sessions · US3 own workspaces · US4 manage members ·
  US5 role-appropriate access.

## Path Conventions

Per [ADR 0001](../../docs/adr/0001-repository-layout.md): `src/backend/DevSync.*`,
`tests/backend/DevSync.*Tests`, `src/frontend/src`, `tests/frontend`.

## Scope guard

Do not add OAuth, social login, Entra ID, SSO, MFA, email delivery, password reset, profile
editing, account deletion, ownership transfer, workspace deletion, Redis or distributed state,
real-time notifications, or cloud deployment.

---

## Phase 1: Setup

- [X] T001 Add to `Directory.Packages.props`: `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.14, `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.14, `Microsoft.Extensions.TimeProvider.Testing` 10.11.0. Reference them from `src/backend/DevSync.Infrastructure/DevSync.Infrastructure.csproj` (Identity.EF), `src/backend/DevSync.Api/DevSync.Api.csproj` (JwtBearer), and both `tests/backend/*/*.csproj` (TimeProvider.Testing). **Done when:** `dotnet build src/backend/DevSync.sln` succeeds with 0 warnings. (research R1, R2, R12)
- [X] T002 [P] Update `.env.example` and `docker-compose.yml`: remove `DevSync__Auth__Mode`; add `DevSync__Auth__SigningKey=` (empty, commented "unset in Development = ephemeral key; required elsewhere") and `DevSync__Auth__RefreshCookie__Secure=true`; pass both to the `api` service. **Done when:** `docker compose config` succeeds and shows the new variables. (FR-029, FR-016, NFR-004)

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: Nothing in Phases 3+ starts until this phase is complete, including the ported
001 suite passing (T030).

### Group 1 / 5 — Domain model (tests first)

- [X] T003 [P] Write `tests/backend/DevSync.UnitTests/Domain/WorkspacePermissionsTests.cs`: all 12 combinations of {Owner, Editor, Viewer} × {`Read`, `EditDetails`, `ViewMemberEmails`, `ManageMembers`} match the data-model table (only Owner may `ViewMemberEmails`/`ManageMembers`; Viewer may only `Read`). **Done when:** it compiles and fails before T006. (FR-027)
- [X] T004 [P] Write `tests/backend/DevSync.UnitTests/Domain/SessionTests.cs` with `FakeTimeProvider`:
  - `IsActive` is false when revoked, at or after `IdleExpiresAt`, and at or after `AbsoluteExpiresAt`.
  - `Rotate` slides idle expiry to `min(now + 7d, AbsoluteExpiresAt)`.
  - `ClassifyPresentedToken`: unused → `Rotate`; used < 10 s ago → `Conflict`; used ≥ 10 s ago → `Reuse`.
  - `Revoke(reason)` accepts only `logout`, `logout_all` and `refresh_token_reuse`.

  **Done when:** it compiles and fails before T007. (FR-010–FR-013, FR-017)
- [X] T005 [P] Write `tests/backend/DevSync.UnitTests/Domain/InvitationTests.cs`:
  - `Create` normalizes the email (trimmed, upper-invariant), rejects emails longer than 254 characters or invalid, and allows role "Editor or Viewer only".
  - `ExpiresAt = CreatedAt + 7 days`.
  - Transitions: Pending → Accepted/Declined only by matching normalized email before `ExpiresAt`; Pending → Revoked; any touch at or after `ExpiresAt` → Expired; every other transition is rejected.

  **Done when:** it compiles and fails before T008. (FR-020)
- [X] T006 Extend `src/backend/DevSync.Domain/Workspaces/WorkspaceRole.cs` (`Owner, Editor, Viewer`) and add `WorkspaceOperation.cs` and `WorkspacePermissions.cs` (pure `Allows(role, op)`). **Done when:** T003 passes. (FR-021, FR-027)
- [X] T007 Create `src/backend/DevSync.Domain/Identity/Session.cs` and `RefreshToken.cs` per data-model (fields, `IsActive`, `Rotate`, `ClassifyPresentedToken` with a 10 s conflict window, `Revoke`), and delete `src/backend/DevSync.Domain/Identity/User.cs`. **Done when:** T004 passes and the solution builds. (FR-009–FR-013)
- [X] T008 Create `src/backend/DevSync.Domain/Workspaces/Invitation.cs` and `InvitationStatus.cs` per data-model (`Pending | Accepted | Declined | Revoked | Expired`). **Done when:** T005 passes. (FR-020)

### Group 1 — Identity persistence

- [X] T009 Create `src/backend/DevSync.Infrastructure/Identity/AppUser.cs`: `AppUser : IdentityUser<Guid>` with `DisplayName` ("trimmed, 1–100 code points") and `CreatedAt`. **Done when:** it compiles. (FR-001, research R1)
- [X] T010 Rewrite `src/backend/DevSync.Infrastructure/Persistence/Configurations/UserConfiguration.cs` for `AppUser` on table `users`, using the columns in data-model.md:
  - `user_name`, `normalized_user_name` (varchar 256, NOT NULL, unique `ux_users_normalized_user_name`);
  - `email`, `normalized_email` (varchar 254, NOT NULL, unique `ux_users_normalized_email`);
  - `email_confirmed`, `password_hash` (text NOT NULL), `security_stamp`, `concurrency_stamp`;
  - `lockout_enabled` default false, `lockout_end`, `access_failed_count` default 0;
  - **ignore** `PhoneNumber`, `PhoneNumberConfirmed`, `TwoFactorEnabled`.

  **Done when:** the model builds and no other Identity tables appear in the model. (FR-002, FR-004)
- [X] T011 [P] Create `src/backend/DevSync.Infrastructure/Persistence/Configurations/SessionConfiguration.cs` and `RefreshTokenConfiguration.cs`:
  - `sessions`: PK; FK `user_id` → users ON DELETE CASCADE; four NOT NULL timestamps; `revoked_reason` varchar(32) with CHECK in (`logout`,`logout_all`,`refresh_token_reuse`); partial index `ix_sessions_user_id_active` on (user_id) WHERE revoked_at IS NULL.
  - `refresh_tokens`: `token_hash` bytea NOT NULL with unique `ux_refresh_tokens_token_hash` and CHECK `octet_length(token_hash) = 32`; FK → sessions ON DELETE CASCADE; self-FK `replaced_by_id` ON DELETE SET NULL; `ix_refresh_tokens_session_id`.

  **Done when:** the model builds. (FR-014, data-model)
- [X] T012 [P] Create `src/backend/DevSync.Infrastructure/Persistence/Configurations/WorkspaceInvitationConfiguration.cs`:
  - Columns: `email`/`normalized_email` varchar(254) NOT NULL; `role` varchar(20) CHECK in (`editor`,`viewer`); `status` varchar(16) CHECK in (`pending`,`accepted`,`declined`,`revoked`,`expired`); CHECK `expires_at > created_at`.
  - FKs: workspace ON DELETE CASCADE; `invited_by_user_id` ON DELETE RESTRICT; `accepted_user_id` ON DELETE SET NULL.
  - Indexes: unique partial `ux_workspace_invitations_pending` (workspace_id, normalized_email) WHERE status = 'pending'; partial `ix_workspace_invitations_pending_email` (normalized_email) WHERE status = 'pending'.

  **Done when:** the model builds. (FR-020)
- [X] T013 [P] Update `WorkspaceMembershipConfiguration.cs` (CHECK `role IN ('owner','editor','viewer')`; add unique partial index `ux_workspace_memberships_one_owner` on (workspace_id) WHERE role = 'owner') and `RoleNames.cs` (map editor/viewer). **Done when:** the model builds. (FR-021, FR-023)
- [X] T014 Update `DevSyncDbContext.cs` (DbSets for `AppUser` as `Users`, `Sessions`, `RefreshTokens`, `WorkspaceInvitations`) and register Identity in `src/backend/DevSync.Infrastructure/DependencyInjection.cs`:
  - `AddIdentityCore<AppUser>`: `Password.RequiredLength = 12`, all character-class rules off, `RequiredUniqueChars = 1`, `User.RequireUniqueEmail = true`, `Lockout.AllowedForNewUsers = false`.
  - `.AddEntityFrameworkStores<DevSyncDbContext>()`.
  - Add `PasswordPolicyValidator.cs`: rejects passwords longer than 128 characters and a password equal to the email (case-insensitive).

  **Done when:** the solution builds. (FR-003, FR-004)

### Group 4 — S01 data migration

- [X] T015 Generate migration `IdentityAndPermissions` into `src/backend/DevSync.Infrastructure/Persistence/Migrations/`. Insert at the **top** of `Up()`: `DELETE FROM workspace_memberships; DELETE FROM workspaces; DELETE FROM users;`. Regenerate the idempotent script `IdentityAndPermissions.sql`. **Done when:** the SQL matches every column, constraint and index in data-model.md, and `dotnet ef migrations has-pending-model-changes` reports none. (FR-030, FR-021, data-model)
- [X] T016 Update `src/backend/DevSync.Infrastructure/Persistence/MigrationRunner.cs`: when `IdentityAndPermissions` is pending and the `users` table exists, count users and workspaces first and log `"Removing {Users} development users and {Workspaces} workspaces from 001 (spec 002 FR-030)"`. **Done when:** the migration test (T017) asserts the log line. (FR-030)
- [X] T017 [P] Write `tests/backend/DevSync.IntegrationTests/Persistence/IdentityMigrationTests.cs`:
  - Apply only `InitialWorkspaceFoundation` to a fresh database, then insert three 001 users, two workspaces and their owner memberships with raw SQL.
  - Start the app (which migrates). Assert 0 users/workspaces/memberships, the log line with counts, and that the new constraints and indexes exist (query `pg_constraint` and `pg_indexes`).
  - Inserting a second `owner` membership violates `ux_workspace_memberships_one_owner`.

  **Done when:** it passes. (FR-030, FR-021)

### Group 3 — Token validation core

- [X] T018 Create the auth ports in `src/backend/DevSync.Application/Auth/`:
  - `IUserAccounts` (`CreateAsync(displayName, email, password)` → `Result<UserProfileDto>` with errors `email_taken` or field errors; `VerifyAsync(email, password)` → `UserProfileDto?`; `GetAsync(id)`);
  - `ISessionRepository` (`AddAsync`, `FindByTokenHashAsync`, `TryMarkUsedAsync`, `IsActiveAsync(sessionId)`, `RevokeAsync`, `RevokeAllForUserAsync`, `SaveChangesAsync`);
  - `IAccessTokenIssuer` (`Issue(userId, sessionId)` → `(token, expiresAt)`);
  - `ILoginAttemptLimiter` (`IsBlocked`, `RecordFailure`, `Reset` keyed by normalized email);
  - `RefreshTokens.cs` (`Generate()` → 32 bytes CSPRNG as base64url; `Hash(token)` → SHA-256 bytes);
  - `UserProfileDto(Id, DisplayName, Email, CreatedAt)`.

  **Done when:** it compiles. (FR-009–FR-019, research R2/R6)
- [X] T019 Implement `src/backend/DevSync.Infrastructure/Identity/SessionRepository.cs`. `TryMarkUsedAsync` is a single conditional `ExecuteUpdateAsync` (`WHERE id = @id AND used_at IS NULL`) returning true only when 1 row changed. `RevokeAllForUserAsync` is a bulk update. **Done when:** used by T021 and covered by US2 tests. (FR-010, FR-013, FR-018)
- [X] T020 Rewrite `src/backend/DevSync.Api/Auth/AuthOptions.cs`: `SigningKey`, `AccessTokenMinutes = 15`, `SessionIdleDays = 7`, `SessionAbsoluteDays = 30`, `RefreshCookie.Secure = true`, `AllowedOrigins`. **Delete** `DevHeaderAuthenticationHandler.cs` and `Endpoints/DevUserEndpoints.cs`, the `Mode` and `SeedDevUsers` options and the dev startup guard, and delete `src/backend/DevSync.Infrastructure/Identity/DevUserSeeder.cs`. Rewrite `UserDirectory.cs` on `AppUser`, removing `ListDevUsersAsync`. **Done when:** the solution builds and no source references `X-DevSync-User`. (FR-029)
- [X] T021 Create `src/backend/DevSync.Api/Auth/SigningKeyProvider.cs`, `AccessTokenIssuer.cs` (`JsonWebTokenHandler`, HS256, issuer/audience `devsync`, claims `sub`, `sid`, `jti`, `iat`, `exp`, **no roles**) and `JwtSetup.cs`:
  - Signing key: from config (base64, ≥ 32 bytes). Development with no key → ephemeral random key + warning log. Any other environment with no or short key → throw at startup naming `DevSync__Auth__SigningKey`.
  - `AddJwtBearer`: validate signature, issuer, audience and lifetime; `ClockSkew = 0`; `MapInboundClaims = false`.
  - `OnTokenValidated`: `ISessionRepository.IsActiveAsync(sid)`, failing with `session_ended` if inactive.
  - `OnChallenge`: write Problem Details 401 with `token_expired` (expired lifetime), `session_ended`, or `unauthenticated` (otherwise), and never the failure reason text.

  `HttpCurrentUser` reads `sub`. **Done when:** the solution builds. (FR-009, FR-017, FR-028, NFR-004)
- [X] T022 In `src/backend/DevSync.Api/Program.cs`, register auth (`JwtSetup`) and an authorization **fallback policy** `RequireAuthenticatedUser()`. Health endpoints get `.AllowAnonymous()`. Extend `src/backend/DevSync.Api/Errors/ErrorCodes.cs` with every 002 code listed in `contracts/openapi.yaml` (`ForStatus(403)` → `forbidden`, `ForStatus(429)` → `too_many_attempts`). **Done when:** the solution builds and `/health/live` is reachable anonymously. (FR-026, FR-028)

### Group 6 — Authorization core

- [X] T023 Create `src/backend/DevSync.Application/Workspaces/IWorkspaceAccess.cs` (`GetRoleAsync(workspaceId, userId)` → `WorkspaceRole?`) and implement it in `src/backend/DevSync.Infrastructure/Workspaces/WorkspaceAccess.cs` as a single no-tracking PK lookup. **Done when:** it compiles. (FR-025)
- [X] T024 Create `src/backend/DevSync.Api/Auth/WorkspaceAuthorization.cs`:
  - `WorkspaceAccess(Guid WorkspaceId, WorkspaceRole Role)` resource and `WorkspaceOperationRequirement(op)`.
  - `WorkspaceOperationHandler : AuthorizationHandler<WorkspaceOperationRequirement, WorkspaceAccess>`, delegating to `WorkspacePermissions.Allows`.
  - Endpoint filter `RequireWorkspace(op)`:
    1. parse `{workspaceId}`; malformed → 404 `not_found`;
    2. look up the role; none → 404 `not_found`;
    3. `IAuthorizationService.AuthorizeAsync`; failure → 403 `forbidden`;
    4. store `WorkspaceAccess` in `HttpContext.Items`.

  Apply `RequireWorkspace(Read)` to the existing `GET /api/workspaces/{workspaceId}`. **Done when:** the solution builds. (FR-010/FR-026/FR-027, research R8)

### Group 8 — Test harness and S01 regression port

- [X] T025 Update `tests/backend/DevSync.IntegrationTests/Infrastructure/DevSyncApiFactory.cs`: remove DevHeader settings; set `DevSync:Auth:SigningKey` to a fixed 32-byte test key; add an optional `FakeTimeProvider` replacing `TimeProvider`; keep `CreateWithUnreachableDatabase` (with seeding settings removed). **Done when:** it compiles. (NFR-003)
- [X] T026 Rewrite `tests/backend/DevSync.IntegrationTests/Infrastructure/TestData.cs` and `HttpClientExtensions.cs`:
  - `CreateAccountAsync(email, password = "correct horse battery", displayName)` uses `UserManager<AppUser>`.
  - `ClientFor(userId)` creates a session via `ISessionRepository` and an access token via `IAccessTokenIssuer` from the factory services, then returns a client with `Authorization: Bearer …`.
  - Add `AuthClient` (cookie-aware `HttpClient` that always sends `X-DevSync-Client: web`) for HTTP auth flows.
  - Remove `DevUserA`/`DevUserB`.

  **Done when:** it compiles. (NFR-003)
- [X] T027 Port the 001 integration tests to accounts: `Workspaces/CreateWorkspaceTests.cs`, `CreateWorkspacePersistenceTests.cs`, `ListWorkspacesTests.cs`, `WorkspaceDetailsTests.cs`, `Errors/CorrelationIdTests.cs`, `Errors/DatabaseOutageTests.cs`, `Observability/LoggingTests.cs`, `Health/HealthTests.cs`, `Persistence/MigrationTests.cs`. Replace `ClientFor(DevUserX)` with `ClientFor(await CreateAccountAsync(...))` and the expected 401 code for missing tokens stays `unauthenticated`. Delete `Auth/DevIdentityTests.cs` and `Auth/StartupGuardTests.cs`. **Done when:** they compile. (SC-008)
- [X] T028 [P] Write `tests/backend/DevSync.IntegrationTests/Auth/SigningKeyStartupTests.cs`: `Production` with no key → host fails to start with a message naming `DevSync__Auth__SigningKey`; `Production` with a 16-byte key → fails; `Development` with no key → starts and logs a warning. **Done when:** it passes. (NFR-004)
- [X] T029 [P] Write `tests/backend/DevSync.IntegrationTests/Auth/TokenValidationTests.cs`:
  - missing bearer → 401 `unauthenticated`;
  - garbage token, a token signed with another key, and a token with the wrong issuer/audience → 401 `unauthenticated`, with no failure detail in the body;
  - a token issued with a `FakeTimeProvider` 16 minutes in the past → 401 `token_expired`;
  - a token whose session was revoked in the database → 401 `session_ended`;
  - `GET /api/dev/users` → 401 (fallback policy; the endpoint no longer exists);
  - an `X-DevSync-User` header alone → 401.

  **Done when:** it passes. (FR-026, FR-028, FR-029, SC-003)
- [X] T030 Run the whole backend suite. **Done when:** `dotnet test --solution src/backend/DevSync.sln` passes: unit, migration, token-validation and all ported 001 tests. (SC-008)

**Checkpoint**: the schema is migrated, tokens are validated with immediate revocation, the dev
identity is gone, and the 001 behaviour is green under accounts.

---

## Phase 3: User Story 1 — Register and Sign In (P1) 🎯 MVP · Groups 2, 7

**Goal**: People register and sign in. Signed-out visitors are redirected to sign in.
**Independent Test**: Register, sign out, sign in, reach `/workspaces`. A wrong password and an
unknown email produce identical failures.

### Tests (write first)

- [X] T031 [P] [US1] Write `tests/backend/DevSync.UnitTests/Application/RegisterHandlerTests.cs` and `LoginHandlerTests.cs` (with fakes):
  - display name trimmed 1–100; email trimmed, ≤ 254, valid; password 11 → error, 12 and 128 → ok, 129 → error; password = email → error; passwords never trimmed;
  - a successful register or login creates exactly one session and one refresh token (stored as a hash) and issues an access token;
  - login with an unknown email still calls `VerifyAsync`, which performs dummy-hash work;
  - a blocked limiter → `too_many_attempts` without verifying;
  - a failure records an attempt and a success resets it.

  **Done when:** it compiles and fails. (FR-001–FR-008)
- [X] T032 [P] [US1] Write `tests/backend/DevSync.IntegrationTests/Auth/RegisterTests.cs`:
  - 201 with an `AuthSession` body (no password or hash fields);
  - `Set-Cookie: devsync_rt` with `HttpOnly`, `SameSite=Strict`, `Path=/api/auth`, `Secure` and a positive `Max-Age`;
  - **duplicate registration** with the same email and with a case/whitespace variant (`" ANA@Example.com "`) → 409 `email_taken`, still one user row;
  - two parallel registrations of the same email → exactly one 201 and one 409;
  - field errors for name 0/101, email invalid/255 characters, password 11/129/equal to email → 400 `validation_failed` with the right `errors` keys, and no row.

  **Done when:** it compiles and fails. (FR-001–FR-003, FR-006, FR-007)
- [X] T033 [P] [US1] Write `tests/backend/DevSync.IntegrationTests/Auth/LoginTests.cs`:
  - success with an email in a different case → 200 + cookie;
  - **invalid credentials**: wrong password → 401 `invalid_credentials`; unknown email → a byte-identical body except `correlationId`;
  - an empty password → 400 `validation_failed`;
  - after 5 failures for an **existing** email the 6th → 429 `too_many_attempts`, and the same for a **non-existing** email;
  - a correct password while blocked → still 429;
  - after `FakeTimeProvider` advances 15 minutes → login works;
  - more than 20 requests per minute from one client → 429;
  - **CSRF**: no `X-DevSync-Client` → 403 `csrf_check_failed`; `Origin: https://evil.example` → 403.

  **Done when:** it compiles and fails. (FR-005, FR-008, FR-016, SC-004)
- [X] T034 [P] [US1] Write `tests/backend/DevSync.IntegrationTests/Auth/ProfileTests.cs`: `GET /api/me` → exactly `id`, `displayName`, `email`, `createdAt` (assert no other properties); without a token → 401. **Done when:** it compiles and fails. (FR-019)

### Backend — Group 2

- [X] T035 [US1] Implement `src/backend/DevSync.Infrastructure/Identity/UserAccounts.cs`:
  - `CreateAsync`: trim the name and email; `UserName = Email = trimmed email`; `UserManager.CreateAsync` maps `DuplicateEmail`/`DuplicateUserName` → `email_taken`, and password errors → field `password`. A unique-violation race (`23505`) → `email_taken`.
  - `VerifyAsync`: find by normalized email; if missing, run `PasswordHasher.VerifyHashedPassword` against a static dummy hash and return null; otherwise `CheckPasswordAsync`.

  **Done when:** T031 and T032 pass for the account parts. (FR-002–FR-006)
- [X] T036 [P] [US1] Implement `src/backend/DevSync.Infrastructure/Identity/LoginAttemptLimiter.cs` with `IMemoryCache`: at most 5 failures per normalized email in 15 minutes, then block for 15 minutes, using `TimeProvider`. **Done when:** the limiter cases in T033 pass. (FR-008)
- [X] T037 [US1] Create `src/backend/DevSync.Application/Auth/Register.cs`, `Login.cs` and `GetProfile.cs`. On success, create a `Session` and its first `RefreshToken` (hash only) in one save, issue an access token, and return `(AuthSessionDto, rawRefreshToken, absoluteExpiresAt)`. Log security events `auth.register`, `auth.login.succeeded`, `auth.login.failed` and `auth.login.blocked` with the user id when known, never the email or password. **Done when:** T031 passes. (FR-005, FR-007, FR-009, NFR-002)
- [X] T038 [US1] Create:
  - `src/backend/DevSync.Api/Auth/ClientHeaderFilter.cs`: requires `X-DevSync-Client: web`; if `Origin` is present it must be in `AllowedOrigins`, defaulting to the request's own origin; otherwise 403 `csrf_check_failed`.
  - `RefreshCookie.cs`: append and clear `devsync_rt` with `HttpOnly`, `SameSite=Strict`, `Path=/api/auth`, `Secure` (from options) and `Max-Age` = remaining absolute lifetime.
  - Rate limiter policy `auth-ip` (fixed window, 20 per minute per remote IP) in `Program.cs`.

  **Done when:** the CSRF and per-IP cases in T033 pass. (FR-008, FR-015, FR-016)
- [X] T039 [US1] Create `src/backend/DevSync.Api/Endpoints/AuthEndpoints.cs` and `MeEndpoints.cs`:
  - an `/api/auth` group (`AllowAnonymous`, `ClientHeaderFilter`, `auth-ip` on register and login);
  - `POST register` → 201; `POST login` → 200 (both set the cookie);
  - `GET /api/me`.

  **Done when:** T032, T033 and T034 pass. (FR-001–FR-008, FR-019)

### Frontend — Group 7 (tests first)

- [X] T040 [P] [US1] Write `tests/frontend/features/auth/LoginPage.test.tsx`, `RegisterPage.test.tsx` and `RequireAuth.test.tsx` (MSW):
  - login: the generic message for `invalid_credentials`, the 429 message, submit disabled while pending, success → `returnTo`;
  - register: client checks (name, email, password 12–128), server field errors, `email_taken` on the email field, success → `/workspaces`;
  - RequireAuth: signed out → `/login?returnTo=%2Fworkspaces%2Fx`; `returnTo=//evil.com` and `https://evil.com` fall back to `/workspaces`.

  **Done when:** they compile and fail. (FR-032, FR-033)
- [X] T041 [US1] Create:
  - `src/frontend/src/features/auth/authSlice.ts`: `status` (`checking | signedIn | signedOut`), `user`, `accessToken`, `accessTokenExpiresAt` and `notice`, in memory only; actions `signedIn`, `tokenRefreshed`, `sessionEnded(notice)`, `signedOut`.
  - `authApi.ts`: register, login and me endpoints.

  Update `src/frontend/src/shared/api/baseApi.ts` to send `Authorization: Bearer` from `authSlice` and `X-DevSync-Client: web` on every request. Remove the `devIdentity` reducer from `app/store.ts`. **Done when:** typecheck passes. (FR-015, FR-032)
- [X] T042 [US1] Create `src/frontend/src/features/auth/LoginPage.tsx`, `RegisterPage.tsx`, `RequireAuth.tsx` (with a `safeReturnTo()` helper), and update `src/frontend/src/app/router.tsx` (public `/login` and `/register`; everything else inside `RequireAuth`; signed-in users visiting `/login` → `/workspaces`) and `AppShell.tsx` (display name and Sign out instead of `DevUserPicker`). Delete `src/frontend/src/features/devIdentity/` and `tests/frontend/features/devIdentity/`, and update `tests/frontend/support/renderWithProviders.tsx` to preload a signed-in auth state. **Done when:** T040 passes and no source references `X-DevSync-User`. (FR-029, FR-032, FR-033)

**Checkpoint**: a person can register and sign in through the API and the browser.

---

## Phase 4: User Story 2 — Stay Signed In Safely and Sign Out (P1) · Groups 3, 7

**Goal**: silent renewal, rotation, reuse detection, sign-out and sign-out everywhere, with
immediate effect.
**Independent Test**: keep working past 15 minutes without re-entering credentials; after sign-out,
neither the old access token nor the old cookie works.

### Tests (write first)

- [X] T043 [P] [US2] Write `tests/backend/DevSync.UnitTests/Application/RefreshHandlerTests.cs`:
  - unknown hash → `session_ended`; inactive session → `session_ended`;
  - unused → rotate (old marked used, new issued, idle slid but capped at absolute);
  - `TryMarkUsed` returning false → treated as used;
  - used < 10 s → `refresh_conflict` with no revocation; used ≥ 10 s → session revoked (`refresh_token_reuse`) + security event.

  **Done when:** it compiles and fails. (FR-010–FR-013)
- [X] T044 [P] [US2] Write `tests/backend/DevSync.IntegrationTests/Sessions/RefreshTests.cs`:
  - refresh → 200 with a new access token and a **different** cookie value;
  - replaying the old cookie within 10 s → 409 `refresh_conflict` and the session still active;
  - replaying it after `FakeTimeProvider` +11 s → 401 `session_ended`, the cookie cleared, and **both** the newest cookie and the current access token now rejected (`session_ended`);
  - 5 parallel refreshes with the same cookie → exactly one 200, the rest 409, and the session still active;
  - no cookie → 401 `session_ended`; no `X-DevSync-Client` → 403.

  **Done when:** it compiles and fails. (FR-010, FR-012, FR-013, SC-003)
- [X] T045 [P] [US2] Write `tests/backend/DevSync.IntegrationTests/Sessions/SessionExpiryTests.cs` with `FakeTimeProvider`:
  - **expired session**: idle 7 days + 1 s without refresh → refresh 401 `session_ended`;
  - refreshing every 6 days keeps the session alive until day 30, and at day 30 + 1 s → 401 (absolute limit);
  - an access token after its session's absolute expiry → 401 `session_ended`.

  **Done when:** it compiles and fails. (FR-011, SC-006)
- [X] T046 [P] [US2] Write `tests/backend/DevSync.IntegrationTests/Sessions/LogoutTests.cs`:
  - **revoked session**: logout → 204 and the cookie cleared (`Max-Age=0`); the same access token on the next request → 401 `session_ended`; the old cookie → 401;
  - logout twice → 204 (idempotent); logout without a cookie or bearer → 204;
  - logout-all from session A ends sessions A and B (B's access token → 401 and B's cookie → 401);
  - logout-all without a bearer → 401;
  - a new login afterwards works.

  **Done when:** it compiles and fails. (FR-017, FR-018, SC-003)

### Backend — Group 3

- [X] T047 [US2] Create `src/backend/DevSync.Application/Auth/Refresh.cs`, `Logout.cs` and `LogoutAll.cs` using `Session.ClassifyPresentedToken` and `ISessionRepository.TryMarkUsedAsync`. A rotation inserts the new token, sets `replaced_by_id` and slides idle expiry in one transaction. Log the security events `auth.refresh.reuse_detected` (warning), `auth.logout` and `auth.logout_all`. **Done when:** T043 passes. (FR-010–FR-013, FR-017, FR-018, NFR-002)
- [X] T048 [US2] Add to `src/backend/DevSync.Api/Endpoints/AuthEndpoints.cs`:
  - `POST /api/auth/refresh` (cookie → 200 + rotated cookie, or 401/409; clears the cookie on 401);
  - `POST /api/auth/logout` (cookie or bearer `sid`; always 204; clears the cookie);
  - `POST /api/auth/logout-all` (bearer required; 204; clears the cookie).

  **Done when:** T044, T045 and T046 pass. (FR-010, FR-017, FR-018)

### Frontend — Group 7 (tests first)

- [X] T049 [P] [US2] Write `tests/frontend/shared/api/baseQueryWithReauth.test.ts`:
  - 3 parallel requests get 401 `token_expired` → exactly **one** `/api/auth/refresh` call, then all 3 are retried once with the new token;
  - refresh 409 → refresh retried once, then success;
  - refresh 401 → `sessionEnded` dispatched, the API cache reset, and navigation to `/login?returnTo=…` with "Your session has ended — please sign in again";
  - a non-`token_expired` 401 → `sessionEnded` directly, with no refresh.

  **Done when:** it compiles and fails. (FR-013, FR-034)
- [X] T050 [P] [US2] Write `tests/frontend/features/auth/SessionBootstrap.test.tsx` and `ProfilePage.test.tsx`:
  - status `checking` shows only "Loading…" and sends no protected request; refresh 200 → signed in; refresh 401 → signed out → `/login`;
  - the profile shows name, email and member since;
  - Sign out → `/login` and state cleared;
  - Sign out everywhere asks for confirmation and then calls `/logout-all`;
  - a spy on `Storage.prototype.setItem` and `indexedDB.open` records **0** auth-related writes during sign-in, refresh and sign-out.

  **Done when:** they compile and fail. (FR-015, FR-032, FR-034)
- [X] T051 [US2] Create `src/frontend/src/shared/api/baseQueryWithReauth.ts`: a single-flight refresh promise per tab, `navigator.locks.request('devsync-refresh', …)` when available, one retry, 409 handling, and `sessionEnded` + `baseApi.util.resetApiState()` on failure. Use it in `baseApi.ts`. Add a proactive refresh timer (about 60 s before `accessTokenExpiresAt`, only while `document.visibilityState === 'visible'`) in `src/frontend/src/features/auth/useSessionKeepAlive.ts`. **Done when:** T049 passes. (FR-013, FR-034, SC-006)
- [X] T052 [US2] Create `src/frontend/src/features/auth/SessionBootstrap.tsx` (calls refresh once at startup and renders "Loading…" while `checking`), `ProfilePage.tsx` (`/profile`, with sign out and sign out everywhere), and the session-ended banner on `LoginPage.tsx`, and wire them in `router.tsx`/`main.tsx`. **Done when:** T050 passes. (FR-017–FR-019, FR-032, FR-034)

**Checkpoint**: sessions renew silently and end immediately when revoked or expired.

---

## Phase 5: User Story 3 — Own My Workspaces Under My Account (P1) · Groups 4, 8

**Goal**: 001 workspace behaviour works for accounts; ownership is tied to the account.
**Independent Test**: two accounts each see only their own workspaces; the other's gives 404.

### Tests (write first)

- [X] T053 [P] [US3] Write `tests/backend/DevSync.IntegrationTests/Workspaces/OwnershipTests.cs`:
  - create → `owner.id` = the caller's account and `myRole` = `owner`;
  - the list includes workspaces where the caller is Editor or Viewer (memberships inserted directly) with the correct `role`;
  - **unauthorized workspace access**: account B → `GET`, `PUT`, members and invitations on A's workspace → 404 `not_found`, with a body identical to a random id;
  - details for the Owner include member `email`; for Editor/Viewer callers no `email` property is present.

  **Done when:** it compiles and fails. (FR-027, FR-031, US3-AS1–AS3)

### Backend

- [X] T054 [US3] Update `src/backend/DevSync.Application/Workspaces/WorkspaceDtos.cs`, `GetWorkspaceDetails.cs` and `src/backend/DevSync.Infrastructure/Workspaces/WorkspaceRepository.cs`:
  - `WorkspaceDetailsDto` gains `MyRole`; `MemberDto` gains an optional `Email`, populated only if `WorkspacePermissions.Allows(role, ViewMemberEmails)`.
  - The handler reads the role from the `WorkspaceAccess` set by `RequireWorkspace`.
  - Create still records the creator as Owner.
  - Contract shapes are otherwise unchanged.

  **Done when:** T053 passes. (FR-027, FR-031, contracts)

### Frontend

- [X] T055 [US3] Update `src/frontend/src/features/workspaces/workspacesApi.ts` and pages: remove the `userId` query args (the cache is reset on sign-in and sign-out instead); the list shows the role; types in `src/frontend/src/shared/api/types.ts` add `editor`, `viewer`, `myRole` and `Member.email?`. Port `tests/frontend/features/workspaces/*.test.tsx` to the signed-in harness. **Done when:** all frontend tests pass. (FR-031, SC-008)

**Checkpoint**: 001 behaviour runs under accounts (backend and frontend).

---

## Phase 6: User Story 4 — Owners Manage Members and Roles (P2) · Groups 5, 6, 7

**Goal**: invitations (invite, inbox, accept, decline, revoke), role changes and removal.
**Independent Test**: the Owner invites an Editor and a Viewer, who accept; the Owner changes a
role and removes a member; access changes on the next request. Non-owners are refused.

### Tests (write first)

- [X] T056 [P] [US4] Write `tests/backend/DevSync.UnitTests/Domain/WorkspaceMembershipRulesTests.cs`:
  - `ChangeMemberRole`: Editor↔Viewer ok; the owner target → `cannot_change_owner`; the role `Owner` → error; a missing member → `member_not_found`.
  - `RemoveMember`: the owner → `cannot_remove_owner`.
  - `AddMember`: an existing member → `already_member`; the role `Owner` → error.

  **Done when:** it compiles and fails. (FR-021–FR-024)
- [X] T057 [P] [US4] Write `tests/backend/DevSync.UnitTests/Application/InviteHandlerTests.cs`:
  - self email (any case) → `cannot_invite_self`;
  - a member's email → `already_member`;
  - an existing pending invitation → `invitation_exists`;
  - an expired pending invitation is marked `expired` and a new one is created;
  - the 51st pending → `too_many_invitations`;
  - the result is identical whether or not an account exists.

  **Done when:** it compiles and fails. (FR-020)
- [X] T058 [P] [US4] Write `tests/backend/DevSync.IntegrationTests/Members/MemberManagementTests.cs`:
  - the Owner changes an Editor to Viewer → 200, and that member's next `PUT` → 403;
  - the Owner removes a member → 204, and their next `GET` → 404 with the workspace gone from their list;
  - the Owner targets themselves (PATCH/DELETE) → 400 `cannot_change_owner`/`cannot_remove_owner`;
  - an unknown or non-member `userId` → 404 `member_not_found`;
  - `{"role":"owner"}` → 400 `validation_failed`.

  **Done when:** it compiles and fails. (FR-022–FR-025, SC-007)
- [X] T059 [P] [US4] Write `tests/backend/DevSync.IntegrationTests/Invitations/InvitationTests.cs`:
  - invite a registered vs an unregistered email → both 201, with bodies identical after removing `id`, `email` and timestamps;
  - the invitee's `GET /api/invitations` shows it (email matched case-insensitively);
  - accept → 200 + membership with the offered role, and the invitation is no longer listed;
  - decline → 204 and no membership;
  - revoke → 204 and a later accept → 404;
  - expired (`FakeTimeProvider` +7 d) → accept 404;
  - another account accepts someone else's invitation → 404;
  - accept twice → 404;
  - duplicate pending → 409 `invitation_exists`; member email → 409 `already_member`; self → 400; 51st → 409;
  - re-invite after expiry → 201.

  **Done when:** it compiles and fails. (FR-020, US4-AS1)

### Backend — Groups 5 and 6

- [X] T060 [US4] Add `ChangeMemberRole`, `RemoveMember` and `AddMember` to `src/backend/DevSync.Domain/Workspaces/Workspace.cs`, enforcing that the "owner is immutable" invariant always holds. **Done when:** T056 passes. (FR-021–FR-024)
- [X] T061 [US4] Create `src/backend/DevSync.Application/Workspaces/ChangeMemberRole.cs`, `RemoveMember.cs`, `InviteMember.cs`, `RevokeInvitation.cs`, `ListWorkspaceInvitations.cs`, `ListMyInvitations.cs`, `AcceptInvitation.cs` (membership + invitation status in one save) and `DeclineInvitation.cs`, plus `IInvitationRepository.cs`. Implement it in `src/backend/DevSync.Infrastructure/Workspaces/InvitationRepository.cs` (expire stale pending in the same transaction before insert). Log the events `membership.invited`, `membership.invitation_revoked`, `membership.joined`, `membership.declined`, `membership.role_changed` and `membership.removed` with the workspace id and actor/target user ids. **Done when:** T057 passes. (FR-020, FR-022, FR-025, NFR-002)
- [X] T062 [US4] Map the endpoints:
  - In `src/backend/DevSync.Api/Endpoints/WorkspaceEndpoints.cs`, under `RequireWorkspace(ManageMembers)`: `PATCH/DELETE /api/workspaces/{workspaceId}/members/{userId}`, and `GET/POST /api/workspaces/{workspaceId}/invitations` and `DELETE …/invitations/{invitationId}`.
  - In `src/backend/DevSync.Api/Endpoints/InvitationEndpoints.cs` (authenticated): `GET /api/invitations`, `POST /api/invitations/{id}/accept`, `POST …/decline`.

  Malformed ids → 404. **Done when:** T058 and T059 pass. (FR-020, FR-022, FR-027)

### Frontend — Group 7 (tests first)

- [X] T063 [P] [US4] Write `tests/frontend/features/workspaces/MembersPanel.test.tsx` and `InvitationsPanel.test.tsx`, and `tests/frontend/features/invitations/InvitationsPage.test.tsx`:
  - the Owner sees role selectors and Remove (with confirmation) for non-owners and none for themselves, and can invite (email + role), see pending invitations and revoke;
  - Editor/Viewer see members without emails and with no controls;
  - the inbox lists invitations; accept → navigates to the workspace; decline removes the row; empty state;
  - `forbidden` → "You don't have permission to do that" and a refetch.

  **Done when:** they compile and fail. (FR-035, FR-036)
- [X] T064 [US4] Create `src/frontend/src/features/workspaces/MembersPanel.tsx` and `InvitationsPanel.tsx` (rendered on `WorkspaceDetailsPage.tsx` according to `myRole`), and `src/frontend/src/features/invitations/invitationsApi.ts` and `InvitationsPage.tsx` (`/invitations`). Add an "Invitations (n)" header link in `AppShell.tsx` and the endpoints in `workspacesApi.ts`. **Done when:** T063 passes. (FR-020, FR-035)

**Checkpoint**: collaboration through invitations works end to end.

---

## Phase 7: User Story 5 — Role-Appropriate Access to Workspace Content (P2) · Groups 6, 7, 8

**Goal**: Editors and Owners edit the name and description; Viewers can't. The full role matrix
is proven.
**Independent Test**: Owner and Editor `PUT` succeed; Viewer `PUT` → 403 with nothing changed.

### Tests (write first)

- [X] T065 [P] [US5] Write `tests/backend/DevSync.UnitTests/Domain/WorkspaceUpdateTests.cs`: `UpdateDetails` reuses the 001 name/description rules (trimming, 1–100 / ≤ 500 code points, control characters) and sets `UpdatedAt` from `TimeProvider`; invalid input leaves the entity unchanged. **Done when:** it compiles and fails. (US5-AS1, AS3)
- [X] T066 [P] [US5] Write `tests/backend/DevSync.IntegrationTests/Authorization/RoleMatrixTests.cs`. A single table-driven `[Theory]` covers every protected workspace endpoint (GET details, PUT, PATCH member, DELETE member, GET/POST/DELETE invitations) × {Owner, Editor, Viewer, non-member, anonymous}. Expected outcomes:
  - Owner → success;
  - Editor → success for GET/PUT, 403 for the rest;
  - Viewer → success for GET, 403 for the rest;
  - non-member → 404;
  - anonymous → 401.

  It asserts that state is unchanged after every refused call. **Done when:** it compiles and fails. (FR-026, FR-027, NFR-003, SC-002)
- [X] T067 [P] [US5] Write `tests/backend/DevSync.IntegrationTests/Authorization/PrivilegeEscalationTests.cs`:
  - a Viewer `PUT`s → 403 and the name is unchanged;
  - a Viewer PATCHes their own role to editor → 403;
  - an Editor PATCHes their own role to owner → 403; an Editor invites someone → 403; an Editor removes the Owner → 403;
  - the Owner PATCHes someone to `owner` → 400;
  - a JWT signed with the test key carrying extra claims `role: owner` / `workspace_role` grants nothing;
  - a JWT whose `sub` is another user but with the caller's `sid` → 401 `session_ended` (the session belongs to a different user);
  - a member downgraded mid-session is refused on the very next request;
  - a removed member's cached UI action → 404.

  **Done when:** it compiles and fails. (FR-024, FR-025, FR-027, SC-007)
- [X] T068 [P] [US5] Write `tests/frontend/features/workspaces/EditWorkspace.test.tsx`: Owner/Editor see Edit; saving updates the heading; 400 field errors are shown; Viewer has no Edit button; a 403 from a stale role → permission toast + refetch. **Done when:** it compiles and fails. (FR-035, FR-036)

### Backend

- [X] T069 [US5] Add `UpdateDetails` to `src/backend/DevSync.Domain/Workspaces/Workspace.cs`, `src/backend/DevSync.Application/Workspaces/UpdateWorkspace.cs`, and `PUT /api/workspaces/{workspaceId}` under `RequireWorkspace(EditDetails)` in `WorkspaceEndpoints.cs` (same body as create; 200 `WorkspaceDetails`). **Done when:** T065 and T066 pass. (US5)
- [X] T070 [US5] Make the `OnTokenValidated` session check in `src/backend/DevSync.Api/Auth/JwtSetup.cs` also verify that `session.UserId == sub`. **Done when:** T067 passes. (FR-017, FR-025)

### Frontend

- [X] T071 [US5] Add inline edit to `src/frontend/src/features/workspaces/WorkspaceDetailsPage.tsx` (`EditWorkspaceForm.tsx`, reusing the create validation) shown when `myRole` is owner or editor, plus a global forbidden toast in `src/frontend/src/shared/ui/Toast.tsx`. **Done when:** T068 passes. (FR-035, FR-036)

**Checkpoint**: all stories are complete; the role matrix and escalation suites are green.

---

## Phase 8: Polish — Security, Regression and Documentation · Groups 8, 9

### Group 8 — Security and regression

- [X] T072 [P] Write `tests/backend/DevSync.IntegrationTests/Security/SecretExposureTests.cs` with a capturing logger. Run a script (register, login, refresh, invite, accept, logout) using a distinctive password and capture every raw refresh token and access token. Scan:
  - every `text`, `varchar` and `bytea` column of every table (password plaintext, raw refresh tokens, UTF-8 and base64url forms);
  - all log messages and exception text (password, refresh tokens, access tokens, `Cookie`/`Authorization` values);
  - all response bodies (password, hashes, refresh tokens).

  Expect 0 hits. **Done when:** it passes. (FR-004, FR-014, NFR-001, SC-005)
- [X] T073 [P] Write `tests/backend/DevSync.IntegrationTests/Security/SecurityEventTests.cs`: each event in NFR-002 is logged once with `correlationId` and the acting user id (and the workspace/target for membership events), and contains no email or password. **Done when:** it passes. (NFR-002)
- [X] T074 [P] Write `tests/backend/DevSync.IntegrationTests/Security/PasswordStorageTests.cs`: the stored `password_hash` is Identity v3 format (first byte `0x01`, PBKDF2-HMAC-SHA512, iterations ≥ 100000 decoded from the header) and differs between two accounts with the same password (salted). **Done when:** it passes. (FR-004)
- [X] T075 Full regression run. `dotnet test --solution src/backend/DevSync.sln`, `npm run typecheck`, `npm run lint`, `npm test` and `npm run build` (in `src/frontend`) all pass with 0 warnings. **Done when:** green, and the counts are recorded in `specs/002-identity-and-permissions/verification.md`. (SC-002, SC-008)
- [X] T076 End-to-end verification. Upgrade the existing local 001 database by running `docker compose up --build -d` against the current volume, and confirm the migration log line shows the removed counts. Then run quickstart.md B1–B13 in a browser and A1–A10 with curl. Record the date, versions, pass/fail per scenario, the migration log line and any deviations in `specs/002-identity-and-permissions/verification.md`. **Done when:** all scenarios pass or have documented, accepted deviations. (SC-001–SC-008, constitution governance)

### Group 9 — Documentation

- [X] T077 [P] Write `docs/adr/0002-authentication-design.md` (context; decision covering Identity core, JWT + rotating hashed refresh cookie, per-request session check, CSRF header, limiter; consequences including the in-process limiter, Safari `Secure` note and unverified-email invitations) and add it to `docs/adr/README.md`. **Done when:** the ADR exists and is linked. (constitution XI)
- [X] T078 [P] Update `README.md`: remove the development-users section and the dev-identity warning; add registering/signing in, roles and invitations, auth environment variables (`SigningKey` ephemeral in Development, `RefreshCookie__Secure`), the API table (auth, me, members, invitations), troubleshooting ("Signed out after API restart in dev without a signing key", Safari cookie, 429). Update `docs/architecture/overview.md` (auth and authorization flow summary). **Done when:** no doc mentions `X-DevSync-User` or dev users except as history. (constitution X)
- [X] T079 [P] Create `specs/002-identity-and-permissions/checklists/traceability.md`, mapping US1–US5 acceptance scenarios, FR-001–FR-036, NFR-001–NFR-005 and SC-001–SC-008 to tests. Add tests for any gap. **Done when:** every row has at least one test or verification reference. (SC-002, constitution V)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** → **Foundational (Phase 2)** → stories.
- Phase 2 internal order:
  - Domain tests (T003–T005) → domain (T006–T008).
  - Persistence: T009 → T010; T011, T012 and T013 [P] after T008/T009; then T014 → T015 → T016 → T017.
  - Auth core: T018 → T019, T020 → T021 → T022.
  - Authorization: T023 → T024.
  - Harness: T025 → T026 → T027, then T028/T029 [P], then T030.
- **US1 (Phase 3)** needs Phase 2.
- **US2 (Phase 4)** needs US1 (sessions are created by login and register).
- **US3 (Phase 5)** needs Phase 2. Its backend is independent of US1/US2, because tests use
  `ClientFor`. Its frontend needs US1's auth state (T041–T042).
- **US4 (Phase 6)** needs US3 (details DTO/`myRole`). Its tests create accounts via `TestData`.
- **US5 (Phase 7)** needs US4 (the role matrix covers the members and invitations endpoints).
  T065, T069 and T070 can start after Phase 2.
- **Polish (Phase 8)** needs all stories. T076 runs last.

### Shared files (sequential across tasks)

- `src/backend/DevSync.Api/Endpoints/WorkspaceEndpoints.cs`: T024 → T062 → T069
- `src/backend/DevSync.Api/Endpoints/AuthEndpoints.cs`: T039 → T048
- `src/backend/DevSync.Domain/Workspaces/Workspace.cs`: T060 → T069
- `src/backend/DevSync.Api/Auth/JwtSetup.cs`: T021 → T070
- `src/frontend/src/app/router.tsx`: T042 → T052 → T064
- `src/frontend/src/features/workspaces/WorkspaceDetailsPage.tsx`: T064 → T071
- `src/frontend/src/shared/api/baseApi.ts`: T041 → T051

## Parallel Examples

```text
Phase 2: T003 + T004 + T005 (separate unit test files); T011 + T012 + T013 (separate configurations); T028 + T029
US1:     T031 + T032 + T033 + T034 + T040; T036 alongside T035
US2:     T043 + T044 + T045 + T046 + T049 + T050
US4:     T056 + T057 + T058 + T059 + T063
US5:     T065 + T066 + T067 + T068
Polish:  T072 + T073 + T074; T077 + T078 + T079
```

## Implementation Strategy

1. **Foundation** (Phase 2): the schema migrates, tokens validate, the dev identity is removed,
   and 001 is green under accounts. Stop and validate with T030.
2. **MVP** = US1 + US2 + US3: real accounts, safe sessions, private workspaces. Validate with
   quickstart B1–B5, B12–B13 and A1–A5.
3. **Collaboration** = US4 + US5: invitations, roles, edit permissions, role matrix.
4. **Polish**: security scans, ADR, docs, end-to-end evidence.


## Implementation notes (2026-10-30)

All 79 tasks are complete. Evidence: [verification.md](./verification.md) (301 backend tests, 75
frontend tests, 49/49 end-to-end checks) and [checklists/traceability.md](./checklists/traceability.md).
Deviations from the task text:

- **T019/T021/T070**: `ISessionRepository.IsActiveAsync(sessionId, userId, now)` checks that the
  session belongs to `sub` from the start, so T070's extra check landed in T021.
- **T026**: `ClientFor` became `ClientForAsync` (no sync-over-async in tests).
- **T024**: the authorization resource type is `WorkspaceAccessContext` (`WorkspaceAccess` names
  the Infrastructure role lookup).
- **T041/T051**: the refresh single-flight is keyed per store `dispatch` (not module-global), a bug
  caught by a component test. `safeReturnTo` lives in `features/auth/returnTo.ts` (react-refresh lint).
- **T055**: user-keyed RTK Query args were removed; the cache resets on sign-in, sign-out and
  session end (listener middleware in `app/store.ts`).
- **T071**: a 403 dispatches `permissionDenied`, which shows the global toast and invalidates the
  Workspace and Invitation tags. Implemented in `baseQueryWithReauth` + `uiSlice`.
- **Extra**: explicitly named FK indexes (data-model.md note); nginx forwards `Host` with the port
  for the Origin check; integration test PostgreSQL `max_connections=500` and `MaxPoolSize=10`.
- **Not done manually**: a real-browser click-through and Safari (see verification.md).

## Notes

- Constraints from data-model.md are quoted in the tasks. Don't change them during
  implementation without updating the data model.
- Each requirement id cited maps to `spec.md`. A task without a requirement must not be added
  (scope guard).
- A test that fails for an unexpected reason is a bug to fix, and its test stays (Principle V).
