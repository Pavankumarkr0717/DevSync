# Implementation Plan: Identity and Workspace Permissions

**Branch**: `002-identity-and-permissions` (no git repository yet) | **Date**: 2026-10-30 |
**Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/002-identity-and-permissions/spec.md`
(clarified 2026-10-30: invitations; one permanent Owner; delete 001 dev data)

## Summary

This feature replaces 001's development identity with real accounts.
- **Accounts and passwords**: ASP.NET Core Identity core, with `UserManager` and the PBKDF2
  `PasswordHasher`.
- **Credentials**: 15-minute JWT access tokens held in memory by the SPA, plus rotating, hashed,
  revocable refresh tokens in an `HttpOnly`, `SameSite=Strict` cookie scoped to `/api/auth`.
- **Revocation**: every bearer request checks that its database session is still active.
- **Roles**: workspace roles grow to Owner, Editor and Viewer, enforced by resource-based ASP.NET
  Core authorization backed by a pure domain permission table.
- **New features**: Owners invite by email (accept/decline inbox), change roles and remove
  members. Owners and Editors can edit the workspace name and description.
- **Migration**: one migration deletes 001's password-less data and reshapes the schema.
- **Frontend**: the React app gains login, registration, profile, invitations, protected routes
  and silent session renewal in Redux Toolkit.

All decisions are in [research.md](./research.md) (R1–R12).

## Technical Context

**Language/Version**: C# 14 / .NET 10 LTS (SDK 10.0.402); TypeScript 6.0 on Node 24. Unchanged
from 001.

**Primary Dependencies (new)**:
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.14 (Infrastructure)
- `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.14 (Api)
- built-in ASP.NET Core rate limiting and `IMemoryCache`
- tests: `Microsoft.Extensions.TimeProvider.Testing` 10.11.0
- frontend: no new packages

**Storage**: PostgreSQL 18. One new EF migration `IdentityAndPermissions` (data-model.md).

**Testing**: xUnit v3 unit tests; WebApplicationFactory + Testcontainers integration tests;
Vitest + RTL + MSW component tests (all from 001).

**Target Platform**: Local Docker Compose, plus native development. No cloud.

**Project Type**: Web application. Layout per
[ADR 0001](../../docs/adr/0001-repository-layout.md).

**Performance Goals**: None claimed (Principle XII). One extra primary-key read per
authenticated request (the session check) and one per workspace-scoped request (the role
check).

**Constraints**: No OAuth, Entra ID, SSO, MFA, Redis, distributed sessions or cloud services.
Credentials never in web storage. Single API instance (the rate-limiter state is in-process).

**Scale/Scope**:
- 10 new endpoints, 1 changed (`PUT` workspace), 2 removed (dev users, dev header).
- 3 new tables, 2 changed.
- 4 new pages, and the details page extended.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Principle | How this plan complies | Pre | Post |
|---|-----------|------------------------|-----|------|
| I | Incremental, spec-driven | Only spec 002 scope. Workspace name/description editing is in the spec (US5). No files, diagrams, real-time or ownership transfer. | ✅ | ✅ |
| II | Clean architecture | Domain stays framework-free (permissions, invitation and session rules). Identity, JWT and cookies live in Infrastructure/Api behind three small ports (R1, R2, R6). | ✅ | ✅ |
| III | Correctness first | Measurable acceptance criteria (spec SC-001–008). Concurrency is handled explicitly (atomic refresh rotation, multi-tab). Errors are explicit codes. | ✅ | ✅ |
| IV | Security by default | Server-side authorization on every route, with a fallback policy. Membership is checked per request. Hashed passwords and refresh tokens. No credentials in web storage. CSRF header + SameSite. Startup refuses without a signing key outside Development. | ✅ | ✅ |
| V | Testability | Unit (domain/application), integration (API + PostgreSQL, full role matrix, security), and component tests. See Test Strategy. | ✅ | ✅ |
| VI | Real-time correctness | N/A (no real-time). | — | — |
| VII | Persistence & recoverability | Versioned migration with an explicit, logged data reset (spec FR-030). Transaction boundaries are defined per operation (data-model.md). | ✅ | ✅ |
| VIII | Observability | Structured security events (NFR-002). Correlation ids kept. Secrets never logged (NFR-001); tested. | ✅ | ✅ |
| IX | Simplicity & cost | Sessions in PostgreSQL; in-process limiter; no Redis or IdP. | ✅ | ✅ |
| X | Developer experience | No secret needed locally (ephemeral dev signing key, R5). README and quickstart updated. Clear auth error codes. | ✅ | ✅ |
| XI | Dependency discipline | First-party Identity hasher and JwtBearer. Only standard primitives (CSPRNG, SHA-256) for refresh tokens. No custom crypto. Choices recorded in research.md. | ✅ | ✅ |
| XII | Evidence-based performance | No performance claims. | ✅ | ✅ |

**Gate result**: PASS.

## Contract changes (documented, per user instruction)

| Change | Kind | Why |
|--------|------|-----|
| `X-DevSync-User` header and `GET /api/dev/users` removed | Breaking, intended | Spec FR-029 |
| 401 codes `token_expired` and `session_ended` added alongside `unauthenticated` | Additive | Spec FR-028 |
| `role` enum adds `editor` and `viewer` | Additive | Spec FR-021 |
| `Member.email` (Owner only) and `WorkspaceDetails.myRole` | Additive | FR-027, FR-035 |
| `PUT /api/workspaces/{id}`, members, invitations, auth and `/api/me` endpoints | New | US1–US5 |
| 403 `forbidden` for members lacking the role | New | FR-027/028 |

The create, list and details request and response shapes from 001 are otherwise unchanged.
Non-members still get 404.

## Authentication flow

```text
Register / Login
  SPA ──POST /api/auth/{register|login} (X-DevSync-Client: web, JSON)──► API
      API: per-IP limiter → per-email failure limiter → UserManager (create | verify; dummy-hash on unknown email)
           → create Session(idle 7d, absolute 30d) + RefreshToken(hash only)
  SPA ◄── 200/201 { accessToken (15 min JWT: sub, sid), expiresAt, user }
          Set-Cookie: devsync_rt=<random 256-bit>; HttpOnly; SameSite=Strict; Path=/api/auth; Secure
  SPA stores accessToken in Redux memory only.

API call
  SPA ──Authorization: Bearer <access>──► API
      JwtBearer: signature, issuer, audience, exp (ClockSkew 0)
      OnTokenValidated: SELECT session by sid → revoked/expired? → 401 session_ended
      Fallback policy: authenticated → endpoint
```

## Refresh-token lifecycle

```text
                    ┌────────────── rotate (unused token) ──────────────┐
                    ▼                                                   │
 [issued] ──presented──► unused? ── yes ── atomic UPDATE used_at ───────┤ new token + new access token, slide idle expiry
                    │                       (0 rows → treat as used)    │
                    no (already used)                                   │
                    ├── used < 10 s ago ──► 409 refresh_conflict (client retries with the newer cookie)
                    └── used ≥ 10 s ago ──► REUSE: revoke session (refresh_token_reuse), security event, 401 session_ended

 Session ends when: logout (this session) | logout-all (all sessions) | reuse detected | idle 7 d | absolute 30 d
 After it ends: refresh → 401 session_ended (cookie cleared); any access token with that sid → 401 session_ended on the next request
```

## Authorization flow

```text
Request to /api/workspaces/{workspaceId}/...           (route declares the operation: Read | EditDetails | ManageMembers)
  1. Authenticated? (fallback policy)                 no  → 401
  2. workspaceId is a UUID?                           no  → 404 not_found
  3. role = SELECT role FROM workspace_memberships WHERE (workspace_id, user_id)   (fresh every request)
                                                      none → 404 not_found  (indistinguishable from missing)
  4. IAuthorizationService.AuthorizeAsync(user, WorkspaceAccess(id, role), WorkspaceOperationRequirement(op))
       handler → Domain WorkspacePermissions.Allows(role, op)
                                                      false → 403 forbidden
  5. Handler runs; data shaping uses the role (member emails only if Allows(role, ViewMemberEmails))
  6. Domain invariants (owner immutable, already member, ...) → 400/409 with specific codes
```

## Threat considerations

| Threat | Mitigation | Verified by |
|--------|------------|-------------|
| Password database theft | PBKDF2-HMAC-SHA512 100k iterations (Identity v3), per-user salt | DB scan test (no plaintext); hash format test |
| Refresh-token database theft | Only SHA-256 hashes stored; 256-bit random tokens | DB scan test |
| XSS stealing credentials | Refresh token `HttpOnly`; access token memory-only and short-lived; React escapes output; no `dangerouslySetInnerHTML` | Cookie-attribute test; lint rule; storage test (FE) |
| Stolen refresh token replayed | Rotation + reuse detection revokes the whole session | Reuse integration test |
| Stolen access token after logout | Per-request session check | Revocation-immediacy test |
| CSRF on cookie endpoints / login CSRF | `SameSite=Strict`, `Path=/api/auth`, required `X-DevSync-Client` header, `Origin` check, no CORS | CSRF tests (missing header, foreign Origin) |
| Credential stuffing / brute force | Per-email failure limiter (identical for unknown emails) + per-IP rate limit | Limiter tests |
| User enumeration | Identical login failure + equal hashing work; invitation creation response identical; registration 409 accepted (FR-006) | Byte-identical response tests |
| Privilege escalation via stale role | Roles never in tokens; role read per request | Downgrade/removal next-request tests |
| IDOR on workspaces/members/invitations | Membership check before every operation; invitations matched to the caller's normalized email | Role matrix; cross-user invitation tests |
| Open redirect via `returnTo` | Only same-origin relative paths accepted | FE test |
| Token forgery | HMAC key ≥ 256 bits from config; refuses to start outside Development without it; issuer/audience validation | Startup test; tampered-token test |
| Secrets in logs/responses | No body/header logging (001); auth events log ids only; Problem Details never echo input | Log-scan test |
| Unverified email claims an invitation | Accepted risk (no email delivery); documented in spec Assumptions | — |

## Project Structure

### Documentation (this feature)

```text
specs/002-identity-and-permissions/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/openapi.yaml, contracts/ui-routes.md
├── checklists/requirements.md
└── tasks.md                     # /speckit-tasks
```

### Source Code (changes to the existing layout)

```text
src/backend/
├── DevSync.Domain/
│   ├── Identity/                 Session.cs, RefreshToken.cs (User.cs removed)
│   └── Workspaces/               WorkspaceRole (+Editor, Viewer), WorkspaceOperation, WorkspacePermissions,
│                                 Invitation, InvitationStatus; Workspace gains UpdateDetails/ChangeMemberRole/RemoveMember/AddMember
├── DevSync.Application/
│   ├── Auth/                     IUserAccounts, ISessionRepository, IAccessTokenIssuer, ILoginAttemptLimiter,
│   │                             RefreshTokens (generate/hash), Register/Login/Refresh/Logout/LogoutAll/GetProfile handlers
│   └── Workspaces/               UpdateWorkspace, ChangeMemberRole, RemoveMember, Invite/Revoke/ListInvitations,
│                                 MyInvitations/Accept/Decline handlers; IWorkspaceAccess (role lookup); DTOs extended
├── DevSync.Infrastructure/
│   ├── Identity/                 AppUser : IdentityUser<Guid>, UserAccounts (UserManager), PasswordPolicyValidator,
│   │                             SessionRepository, LoginAttemptLimiter (IMemoryCache)  — DevUserSeeder, UserDirectory updated/removed
│   ├── Persistence/              configurations for users (Identity), sessions, refresh_tokens, workspace_invitations;
│   │                             migration IdentityAndPermissions
│   └── Workspaces/               WorkspaceRepository extended, InvitationRepository
└── DevSync.Api/
    ├── Auth/                     JwtSetup (options, OnTokenValidated session check, OnChallenge codes),
    │                             AccessTokenIssuer, RefreshCookie, ClientHeaderFilter (CSRF), AuthEndpoints,
    │                             WorkspaceAuthorization (requirement, handler, RequireWorkspace filter)
    │                             — DevHeaderAuthenticationHandler, AuthOptions(Mode), DevUserEndpoints removed
    ├── Endpoints/                WorkspaceEndpoints (+PUT, members), InvitationEndpoints, MeEndpoints
    └── Errors/                   ErrorCodes extended

tests/backend/
├── DevSync.UnitTests/            Domain: permissions matrix, invitation transitions, session expiry/rotation,
│                                 workspace member invariants; Application: auth + workspace handlers with fakes
└── DevSync.IntegrationTests/     Infrastructure: AuthClient helpers (register/sign-in, cookie container)
                                  Auth/, Sessions/, Authorization/RoleMatrixTests, Members/, Invitations/,
                                  Security/ (enumeration, CSRF, cookies, secrets scan, tokens), Migration/; 001 tests ported

src/frontend/src/
├── features/auth/                authSlice, authApi, LoginPage, RegisterPage, ProfilePage, RequireAuth, sessionEvents
├── features/invitations/         invitationsApi, InvitationsPage
├── features/workspaces/          WorkspaceDetailsPage (+EditDetails, MembersPanel, InvitationsPanel), workspacesApi extended
├── shared/api/                   baseQueryWithReauth.ts (single-flight refresh), baseApi.ts uses it
└── app/                          router (public/protected), AppShell (user menu)  — features/devIdentity removed
tests/frontend/                   auth/, invitations/, workspaces/ (role-aware controls), shared/api/baseQueryWithReauth.test.ts
```

**Structure Decision**: Keep the four backend projects and the module folders from 001. The new
`Auth` module adds four ports. Each isolates an external dependency (Identity, JWT, the
database, the cache) or holds testable policy, per Principle II.

## Configuration

| Variable | Default | Purpose |
|----------|---------|---------|
| `DevSync__Auth__SigningKey` | *(unset)* | Base64, ≥ 32 bytes. Unset in Development → ephemeral key + warning. Unset elsewhere → startup fails. |
| `DevSync__Auth__AccessTokenMinutes` | `15` | FR-009 |
| `DevSync__Auth__SessionIdleDays` / `SessionAbsoluteDays` | `7` / `30` | FR-011 |
| `DevSync__Auth__RefreshCookie__Secure` | `true` | Set `false` only for plain-HTTP non-localhost or Safari development |
| `DevSync__Auth__AllowedOrigins` | *(request origin)* | `Origin` check for `/api/auth/*` |
| `DevSync__Auth__Mode` | **removed** | Dev header identity no longer exists |

## Test Strategy

| Layer | Covers |
|-------|--------|
| **Unit — Domain** | `WorkspacePermissions` for every role × operation; `Invitation` transitions, including expiry at the exact boundary; `Session.IsActive` and idle sliding capped at absolute; workspace member invariants (owner immutable, already member, editor/viewer only) |
| **Unit — Application** | Register (password rules incl. 11/12/128/129 chars and password = email; email normalization); Login (unknown email still calls verify; limiter consulted; success resets it); Refresh classification (unused, conflict window, reuse); invitation creation rules (self, member, duplicate, 50 cap); accept/decline matching by normalized email |
| **Integration — Auth** | Register → 201 + cookie attributes; duplicate (case-variant) → 409; login success; unknown email vs wrong password **byte-identical**; 6th failure → 429 for existing *and* non-existing emails; refresh rotates (old cookie then fails); concurrent refresh → exactly one 200 (+409); reuse after grace → session revoked and its access token rejected; logout and logout-all immediacy; expired access → `token_expired`; tampered/foreign-key token → `unauthenticated`; idle/absolute expiry with `FakeTimeProvider`; missing `X-DevSync-Client` → 403; foreign `Origin` → 403; `/api/me` shape |
| **Integration — Authorization** | **Role matrix**: every protected endpoint × {Owner, Editor, Viewer, non-member, anonymous} → expected status, generated as `[Theory]` data from one table mirroring spec FR-027; role change/removal effective on the next request |
| **Integration — Members & invitations** | Invite existing vs non-existing email → identical 201 body (minus ids/timestamps); invitee inbox; accept → member with role; decline; revoke; expired; wrong account → 404; duplicate pending → 409; already member → 409; self → 400; 51st → 409; owner cannot be removed/changed |
| **Integration — Migration** | Database with 001 data (dev users + workspaces) → after migration: all removed, counts logged, new constraints/indexes present; schema matches data-model.md |
| **Integration — Security scans** | After a scripted run (register, login, refresh, invite, logout), scan every text/bytea column, all captured logs and all response bodies for the plaintext passwords and raw refresh tokens used → 0 hits; access tokens never logged |
| **Integration — 001 regression** | All 001 integration tests ported to real accounts and still passing (spec SC-008) |
| **Component (FE)** | Login/register validation and error mapping; `RequireAuth` redirect with `returnTo` and open-redirect rejection; bootstrap `checking` state; `baseQueryWithReauth`: token_expired → one refresh for N parallel requests → retries; refresh 409 → retry; refresh 401 → session-ended banner; nothing written to web storage (spy); role-based controls (Viewer has no Edit, Editor has no member controls, Owner has all); invitations accept/decline; profile sign-out/sign-out-everywhere |
| **End-to-end** | quickstart.md scenarios against Docker Compose |

## Local development

- **Compose**: `cp .env.example .env && docker compose up --build -d`. No auth secret is needed
  (ephemeral dev key). Register at http://localhost:8080/register.
- **Native**: as in 001, using `dotnet user-secrets` for the connection string. Optionally
  `dotnet user-secrets set "DevSync:Auth:SigningKey" "<base64 32 bytes>"` so access tokens survive
  API restarts.
- **Upgrading an existing 001 database**: the migration deletes all 001 users and workspaces
  automatically (spec FR-030). The API log shows the counts.
- **Safari / non-localhost HTTP**: set `DevSync__Auth__RefreshCookie__Secure=false`.

## Phased Implementation

1. **Domain**: roles, permissions, invitation and session rules, workspace member operations,
   with unit tests.
2. **Persistence**: `AppUser` mapping, new configurations, the `IdentityAndPermissions`
   migration (with data reset), and a migration test.
3. **Auth backend**: Identity setup, password policy, limiter, sessions, JWT issue/validate with
   session check, refresh cookie, CSRF filter, auth and `/api/me` endpoints, error codes.
   Remove the dev identity.
4. **Authorization backend**: fallback policy, `RequireWorkspace` filter + requirement and
   handler, workspace `PUT`, member endpoints, and the role matrix tests. Port the 001 tests.
5. **Invitations backend**: endpoints and tests.
6. **Frontend auth**: `authSlice`, `baseQueryWithReauth`, bootstrap, login/register/profile,
   `RequireAuth`. Remove `devIdentity`.
7. **Frontend permissions**: details page edit/members/invitations panels, invitations inbox.
8. **Security scans, docs** (README, .env.example, ADR 0002 "Authentication design", quickstart
   run), and verification evidence.

## Complexity Tracking

No constitution violations. The four Auth ports are justified in the Structure Decision.
