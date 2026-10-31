# Research: Identity and Workspace Permissions

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-10-30

Decision / Rationale / Alternatives for every open technical question. Builds on
[001 research](../001-workspace-foundation/research.md) (stack, layering, error contract and
testing are unchanged unless stated here).

## Inputs inspected (2026-10-30)

- S01 code in `src/backend` and `src/frontend`: `DevHeaderAuthenticationHandler`,
  `HttpCurrentUser : ICurrentUser`, `IUserDirectory`, `DevUserSeeder`, `Domain.Identity.User`,
  `WorkspaceMembership` (role `owner` only, DB `CHECK (role IN ('owner'))`), membership-filtered
  queries in `WorkspaceRepository`, `Problems`/`ErrorCodes`, and the frontend `devIdentity`
  slice with user-keyed RTK Query cache.
- S01 migration `InitialWorkspaceFoundation` (tables `users`, `workspaces`,
  `workspace_memberships`).
- NuGet (same day): `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.14,
  `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.14,
  `Microsoft.Extensions.TimeProvider.Testing` 10.11.0.

## R1. User management and password hashing

- **Decision**: **ASP.NET Core Identity core**, without its UI or cookie sign-in:
  `AddIdentityCore<AppUser>()` + `AddEntityFrameworkStores<DevSyncDbContext>()`.
  - `AppUser : IdentityUser<Guid>` (plus `DisplayName` and `CreatedAt`) lives in
    **Infrastructure** and maps to the existing `users` table with snake_case columns. Domain
    keeps referring to users only by `Guid`, and `Domain.Identity.User` is deleted.
  - `UserManager<AppUser>` handles email normalization, uniqueness and password hashing. The
    hasher is `PasswordHasher` v3 (PBKDF2-HMAC-SHA512, 100,000 iterations, 128-bit salt), the
    framework default.
  - Password options: `RequiredLength = 12`, all character-class rules off,
    `RequiredUniqueChars = 1`. The 128 maximum and "not equal to email" (FR-003) are added as a
    small `IPasswordValidator<AppUser>`.
  - `User.RequireUniqueEmail = true`; `UserName` = email (Identity requires a user name).
  - Only the user entity is mapped. Roles, claims, logins and tokens tables are **not** created:
    `UserOnlyStore` touches them only through APIs DevSync never calls. If a later feature needs
    them, it adds a migration.
- **Rationale**: Requested by the user. A reviewed hasher with automatic rehash-on-upgrade,
  normalization and uniqueness handling, with no hand-written crypto (constitution XI).
- **Alternatives**: Using only `PasswordHasher<T>` with our own user store (fewer framework types,
  but re-implements normalization and validation); full Identity with `IdentityDbContext` (adds 6
  unused tables and global roles that conflict with per-workspace roles); Argon2 via a third-party
  package (not first-party; not needed for this threat model).

## R2. Credential design

- **Decision**: Split credentials.
  - **Access token**: a JWT signed with HMAC-SHA256, lifetime **15 min**, claims `sub` (user id),
    `sid` (session id), `jti`, `iat`, `exp`, issuer and audience `devsync`. It's returned in the
    JSON response body, and the SPA holds it **in memory only** (Redux state) and sends it as
    `Authorization: Bearer`. It contains no roles (FR-025).
  - **Refresh token**: 32 random bytes (`RandomNumberGenerator`), base64url. It's sent only as a
    cookie `devsync_rt`: `HttpOnly; SameSite=Strict; Path=/api/auth; Secure` (configurable, see
    R7), `Max-Age` = remaining absolute session lifetime. The server stores **SHA-256(token)**
    only. A fast hash is correct for 256-bit random secrets; slow hashing is for low-entropy
    passwords.
- **Rationale**: The bearer header is never sent automatically, so normal API calls need no CSRF
  defence. The long-lived credential is unreachable from scripts (FR-015) and travels only to
  the auth endpoints (`Path`). Stateless access tokens keep normal requests cheap. The session
  check in R4 gives immediate revocation.
- **Alternatives**: A cookie-only session, with an Identity cookie for API calls (needs CSRF
  tokens on every mutating request and doesn't meet the "short-lived access token" requirement);
  a refresh token in localStorage (forbidden by spec); asymmetric signing keys (no third-party
  verifier exists; HMAC is simpler with a single service).

## R3. Session and refresh-token lifecycle

- **Decision**: Sessions and refresh tokens are database tables (data-model.md).
  1. **Sign-in / registration** → create a `session` (idle expiry = now + 7 days, absolute
     expiry = now + 30 days) and its first `refresh_token`; return the access token and set the
     cookie.
  2. **Refresh** (`POST /api/auth/refresh`) → look up the token by hash.
     - Unknown token → 401 `session_ended`.
     - Session revoked or expired → 401 `session_ended`; clear the cookie.
     - Token **unused** → atomically mark it used:
       `UPDATE refresh_tokens SET used_at = now, replaced_by = :new WHERE id = :id AND used_at IS NULL`.
       If exactly 1 row changed, insert a new token, slide idle expiry (never past absolute),
       and issue a new access token and cookie. If 0 rows changed, another request won; treat as
       already used.
     - Token **already used** less than **10 s** ago → 409 `refresh_conflict` and no revocation:
       a concurrent tab already rotated, and the browser's shared cookie jar now holds the new
       token, so the client retries once (FR-013).
     - Token already used 10 s or more ago → **reuse detected**: revoke the session (reason
       `refresh_token_reuse`), log a security event, return 401 `session_ended` (FR-012).
  3. **Sign-out** (`POST /api/auth/logout`) → revoke the session named by the cookie (or by the
     bearer `sid`); clear the cookie. Idempotent: always 204.
  4. **Sign-out everywhere** (`POST /api/auth/logout-all`, bearer required) → revoke every active
     session of the user; clear the cookie.
- **Rationale**: Implements FR-009–FR-018 with standard rotation plus reuse detection. The 10 s
  grace handles real multi-tab races without weakening detection beyond that window. The
  conditional `UPDATE` makes concurrency safe without locks.
- **Alternatives**: No grace period (legitimate multi-tab races would log people out); keeping
  only the current hash on the session row (can't tell reuse of an old token from garbage).

## R4. Access-token validation and immediate revocation

- **Decision**: `AddJwtBearer`: validate issuer, audience, signature and lifetime with
  `ClockSkew = 0`. In `OnTokenValidated`, look up `sessions` by `sid` (a primary-key read) and
  reject the token if the session is revoked or past its idle or absolute expiry. Map failures in
  `OnChallenge` to Problem Details:
  - expired token → 401 `token_expired`
  - revoked or ended session → 401 `session_ended`
  - missing or invalid token → 401 `unauthenticated`
- **Rationale**: FR-017 and FR-018 require revocation "on the next request". One indexed read
  per authenticated request is cheap. No performance claim is made (XII).
- **Alternatives**: A short access lifetime alone (up to 15 min of post-logout access violates
  FR-017); an in-memory revocation list (lost on restart).

## R5. Signing key and configuration

- **Decision**: `DevSync:Auth:SigningKey` (base64, at least 32 bytes). In **Development**, if it
  isn't set, generate a random key at startup and log a warning. Restarting then invalidates
  access tokens, but sessions survive because refresh tokens are opaque database records.
  **Outside Development the app refuses to start** without a valid key (NFR-004). Integration
  tests set a fixed test key.
- **Rationale**: Nothing secret has to go in `.env.example`, and production-like environments
  fail fast.
- **Alternatives**: A placeholder key in `.env.example` (a predictable secret that could leak into
  real use); ASP.NET Data Protection-backed keys (more moving parts for a single service).

## R6. Brute-force and enumeration controls

- **Decision**:
  - **Per-email failure limiter** (`ILoginAttemptLimiter`, backed by `IMemoryCache`): 5 failed
    sign-ins per normalized email per 15 min, then 429 `too_many_attempts` for 15 min. It
    applies identically to emails with and without accounts, so it doesn't reveal which exist.
    A successful sign-in clears the count. Identity's own lockout is **disabled**
    (`LockoutEnabled = false`) because it only applies to existing accounts and would leak
    their existence.
  - **Per-client limiter**: ASP.NET Core rate limiting, fixed window, 20 requests per minute per
    remote IP on `/api/auth/register` and `/api/auth/login`.
  - **Unknown email**: verify the supplied password against a fixed dummy hash, so the work done
    matches a real account (FR-005). The response is identical: 401 `invalid_credentials`.
  - **Registration**: an existing email returns 409 `email_taken`. This is accepted enumeration
    (spec FR-006), limited by the per-client limiter.
  - **Invitations**: creating an invitation returns 201 with the same body whether or not the
    email has an account (FR-020).
- **Rationale**: Meets FR-005, FR-006, FR-008 and FR-020 without new infrastructure.
- **Limitation (documented)**: limiter state is per process and in memory, which matches the
  single-instance modular monolith (IX). A multi-instance deployment would need a shared store,
  which needs its own spec.

## R7. Cookie security, CSRF and local HTTP

- **Decision**:
  - The cookie is `SameSite=Strict` and `Path=/api/auth`. The SPA and API share one origin
    (nginx in Compose, the Vite proxy in development), so no CORS is configured and cross-origin
    browser calls fail by default.
  - Every request to `/api/auth/*` must carry the header `X-DevSync-Client: web`; requests without
    it get 403 `csrf_check_failed`. Cross-site forms can't set custom headers, and cross-origin
    `fetch` with a custom header needs a CORS preflight that the API never approves. If an
    `Origin` header is present, it must match the configured `DevSync:Auth:AllowedOrigins`
    (default: the request's own scheme and host).
  - `Secure` defaults to **true** (`DevSync:Auth:RefreshCookie:Secure`). Chromium and Firefox
    accept `Secure` cookies on `http://localhost`. Developers using Safari or a non-localhost
    plain-HTTP host set it to `false`. The README documents this.
- **Rationale**: Defence in depth for the only cookie-authenticated endpoints. It also blocks
  login CSRF.
- **Alternatives**: Antiforgery tokens (needs an extra token round-trip on the SPA for a few
  endpoints, which the header check covers); `__Host-` cookie prefix (forces `Path=/`, so the
  cookie would go to every request).

## R8. Authorization model

- **Decision**: Resource-based authorization with the framework:
  - **Fallback policy**: authenticated user required. Every endpoint is protected unless it
    explicitly allows anonymous access (auth endpoints and health checks). This enforces FR-026
    by default.
  - **Domain**: `WorkspacePermissions.Allows(WorkspaceRole role, WorkspaceOperation op)` is a
    pure table matching spec FR-027 (operations: `Read`, `EditDetails`, `ViewMemberEmails`,
    `ManageMembers`).
  - **API**: an endpoint filter `RequireWorkspace(op)` on every `/api/workspaces/{workspaceId}…`
    route.
    1. Parse the id; malformed → 404.
    2. Load the caller's current role with one query; no membership → 404 `not_found`.
    3. Call `IAuthorizationService.AuthorizeAsync(user, new WorkspaceAccess(id, role), new WorkspaceOperationRequirement(op))`.
       Its handler delegates to `WorkspacePermissions`. Failure → 403 `forbidden`.
    4. Store `WorkspaceAccess` in `HttpContext.Items` for the handler.
  - Repository queries keep the membership filter from 001 as defence in depth.
- **Rationale**: Uses the ASP.NET Core authorization pipeline as requested. The permission matrix
  lives in one pure, unit-tested place, and roles are read fresh from the database on every
  request (FR-025).
- **Alternatives**: Role claims inside the JWT (stale until the token expires, violating
  FR-025); per-handler `if` checks (scattered and easy to forget); a policy per role (can't
  express "not a member → 404").

## R9. Invitations

- **Decision**: An `Invitation` aggregate in Domain with transitions `Pending → Accepted |
  Declined | Revoked | Expired`. Expiry is evaluated against `TimeProvider`, and the stored
  status becomes `expired` lazily when touched.
  - The invitee's identity is matched by their account's **normalized email** (Identity's
    `NormalizedEmail`).
  - Acceptance adds the membership and marks the invitation accepted in **one** `SaveChanges`.
  - A partial unique index allows one pending invitation per (workspace, normalized email).
- **Rationale**: Implements spec FR-020 with consent and no enumeration.
- **Alternatives**: Adding members directly by email (rejected by the user); invite links
  (rejected by the user).

## R10. S01 data migration

- **Decision**: The migration `IdentityAndPermissions` starts by deleting all existing
  `workspace_memberships`, `workspaces` and `users`. By construction, all S01 users are
  password-less development users. It uses `RAISE NOTICE` to print the counts, and the runner
  logs them. It then alters `users` into the Identity shape, widens the role CHECK, and creates
  the `sessions`, `refresh_tokens` and `workspace_invitations` tables (data-model.md).
  `DevUserSeeder`, `DevHeaderAuthenticationHandler`, `/api/dev/users` and the frontend
  `devIdentity` feature are removed.
- **Rationale**: Spec FR-029 and FR-030, decided by the user.
- **Alternatives**: Keeping the data with an operator claim command, or first-user claim (both
  rejected by the user).

## R11. Frontend session handling

- **Decision**:
  - `authSlice` (Redux Toolkit) holds `{ status: 'checking' | 'signedIn' | 'signedOut', user,
    accessToken, accessTokenExpiresAt, notice }` in memory only.
  - On load, call `/api/auth/refresh` once to restore the session from the cookie, and show a
    neutral loading screen while `checking`.
  - `baseQueryWithReauth` wraps `fetchBaseQuery`:
    - It adds `Authorization` and `X-DevSync-Client`.
    - On 401 `token_expired` it runs a **single-flight** refresh: one promise per tab, plus
      `navigator.locks.request('devsync-refresh')` across tabs when available. It then retries
      the original request once.
    - On 409 `refresh_conflict` it retries the refresh once.
    - On any other 401 it dispatches `sessionEnded`, which clears auth and the RTK Query cache
      and redirects to `/login` with the message "Your session has ended — please sign in
      again" and a `returnTo`.
  - It also refreshes proactively about 60 s before the access token expires, while the tab is
    visible.
  - `RequireAuth` route guard (UX only; the server is authoritative). `resetApiState()` runs on
    every sign-in and sign-out, so the user-keyed cache args from S01 are no longer needed.
- **Rationale**: Implements FR-032–FR-036. Credentials never touch persistent storage.
- **Alternatives**: Storing the access token in sessionStorage (forbidden by FR-015); refreshing
  in a service worker (more complex, no benefit here).

## R12. Testing

- **Decision**: Same tools as 001, plus `Microsoft.Extensions.TimeProvider.Testing`
  (`FakeTimeProvider`) for session expiry. Expired access tokens are produced by issuing them
  with a fake clock in the past. The integration harness gains `RegisterAsync` /
  `SignInAsync(email)` helpers that return an authenticated `HttpClient` plus a cookie container.
  S01 tests switch from the dev header to real accounts.
- **Security tests**:
  - the full role × operation matrix;
  - enumeration (identical bodies);
  - reuse detection and concurrent refresh;
  - revocation immediacy;
  - cookie attributes;
  - the CSRF header;
  - secrets absent from the database (no plaintext), from logs (a capturing logger scanned for
    passwords, tokens and cookie values) and from responses.
