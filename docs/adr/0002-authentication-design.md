# ADR 0002: Authentication and authorization design

**Status**: Accepted | **Date**: 2026-10-30 | **Spec**: [002 Identity and Workspace Permissions](../../specs/002-identity-and-permissions/spec.md)

## Context

Feature 001 identified people with a development-only header. Feature 002 needs real accounts,
short-lived access with revocable sessions, and per-workspace roles (Owner, Editor, Viewer),
without external identity providers or new infrastructure (constitution Principle IX). The
detailed alternatives are in [research.md](../../specs/002-identity-and-permissions/research.md).

## Decision

1. **Accounts**: ASP.NET Core Identity *core* (`UserManager`, `PasswordHasher` v3 =
   PBKDF2-HMAC-SHA512, 100k iterations), mapped onto the existing `users` table. Only the user
   entity is mapped; no Identity UI, cookies, roles, claims, logins or tokens tables. The domain
   refers to users only by id.
2. **Access token**: a 15-minute HS256 JWT (`sub`, `sid`, `jti`). The SPA keeps it **in memory
   only** and sends it as a bearer header. It contains no roles.
3. **Refresh token**: 256 random bits, stored only as a SHA-256 hash. It is sent in the
   `devsync_rt` cookie (`HttpOnly`, `SameSite=Strict`, `Path=/api/auth`, `Secure` by default).
   Every refresh rotates it. A token presented again within 10 s gets `409 refresh_conflict`, a
   multi-tab race: the client retries with the newer cookie. Presented later, it **revokes the
   whole session** (theft).
4. **Sessions** live in PostgreSQL: 7-day idle and 30-day absolute limits. **Every bearer request
   checks its session** (one primary-key read), so sign-out and sign-out-everywhere take effect
   on the next request.
5. **CSRF**: only `/api/auth/*` uses the cookie. Those endpoints require
   `X-DevSync-Client: web` and a same-origin `Origin`, on top of `SameSite=Strict`. The API sets
   no CORS policy.
6. **Brute force and enumeration**: an in-process limiter allows 5 failed sign-ins per normalized
   email per 15 minutes, applied identically to unknown emails; plus 20 auth requests per minute
   per IP. Unknown emails still run a password-hash verification. Invitation creation responds
   identically whether or not an account exists. Registration does reveal a taken email, which
   is accepted (spec FR-006).
7. **Authorization**:
   - A fallback policy requires authentication everywhere except auth endpoints and health checks.
   - Workspace routes use a `RequireWorkspace(operation)` filter: malformed id or no membership
     → 404; member without the permission → 403, via `IAuthorizationService` and a handler
     delegating to the pure domain table `WorkspacePermissions`.
   - Roles are read from the database on every request.
8. **Signing key**: from `DevSync__Auth__SigningKey`. In Development a temporary key is generated
   if it is unset; elsewhere startup fails without a key of at least 256 bits.

## Consequences

- Revocation is immediate, at the cost of one indexed read per authenticated request. No
  performance figure is claimed (Principle XII).
- Limiter state is per process. A multi-instance deployment needs a shared store and its own
  spec.
- Safari and some plain-HTTP hosts reject `Secure` cookies on `http://`. Developers there set
  `DevSync__Auth__RefreshCookie__Secure=false`.
- Without a configured key in Development, access tokens are invalidated by an API restart.
  Sessions survive (the refresh cookie is a database record), so the SPA renews silently.
- Emails are not verified (no email delivery). Whoever registers an invited address can accept
  the invitation; the spec records this as an accepted risk.
- Ownership is permanent and single in this release. Transfer needs a later spec.
