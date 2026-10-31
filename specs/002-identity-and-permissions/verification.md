# Verification Evidence: Identity and Workspace Permissions

**Date**: 2026-10-30 | **Feature**: [spec.md](./spec.md) | **Tasks**: T072–T076

## Environment

| Tool | Version |
|------|---------|
| OS | Windows 11 (10.0.26300) |
| .NET SDK | 10.0.402 (Microsoft.Testing.Platform) |
| Node.js / npm | 24.21.0 / 10.9.0 |
| Docker / Compose | 29.8.0 / v5.5.1 |
| PostgreSQL | `postgres:18-alpine` |
| Key packages | ASP.NET Core Identity EF 10.0.14, JwtBearer 10.0.14, TimeProvider.Testing 10.11.0 |

## Automated suites (T075)

| Command | Result |
|---------|--------|
| `dotnet build src/backend/DevSync.sln --no-incremental` | Succeeded, **0 warnings** |
| `dotnet test --solution src/backend/DevSync.sln` | **301 passed**, 0 failed |
| `npm run typecheck` (app + tests) | Passed |
| `npm run lint` (app + tests) | Passed, no findings |
| `npm test` | **75 passed**, 0 failed (15 files) |
| `npm run build` | Succeeded |

The 301 backend tests include:
- the role × endpoint matrix (35 cases);
- privilege-escalation attempts;
- refresh rotation, conflict, reuse and concurrency;
- idle and absolute expiry;
- immediate logout and logout-all;
- enumeration-identical login and invitation responses;
- per-email and per-IP throttling;
- CSRF header and `Origin` checks, and cookie attributes;
- signing-key startup guard; password hash format;
- secret scans of the database, logs and responses; security-event logging;
- the 001 → 002 migration;
- every ported 001 test (S01 regression, SC-008).

## End-to-end against Docker Compose (T076)

**Upgrade path:** `docker compose up --build -d` over the existing local 001 database volume. The
API logged, before applying the migration:

```text
Removing 3 development users and 2 workspaces from 001 (spec 002 FR-030)
Applying 1 migration(s): 20261030030028_IdentityAndPermissions
DevSync__Auth__SigningKey is not set; using a temporary signing key (Development only). ...
```

**Scenarios:** quickstart A1–A10, plus API equivalents of browser flows B1–B3, B5–B13, scripted
with curl through the web proxy (`http://localhost:8080`, the same origin and headers a browser
sends). **Result: 49/49 checks passed.**

| Area | Checks |
|------|--------|
| B1 | Protected API 401 without a session; SPA served |
| B2/B3, B13 | Register; cookie `HttpOnly`, `SameSite=Strict`, `Path=/api/auth`, `Secure`; case-variant duplicate → 409 `email_taken` |
| A1–A3 | Missing `X-DevSync-Client` → 403; wrong password vs unknown email identical; 5 failures then 429 |
| B5–B7, A9 | Create (myRole owner); invite registered and unregistered emails with identical response shape; invitees accept with their roles |
| B8/B9, A7/A8 | Editor renames but cannot invite; Viewer `PUT` → 403 `forbidden`; only the Owner sees emails; owner role immutable → 400 |
| B10/B11, A6 | Role change effective on the next request; removed member → 404 |
| A4 | Rotation; replay within 10 s → 409; replay after 11 s → 401 and the newest cookie also dead (session revoked) |
| A5, B12 | Logout → old access token 401 `session_ended`; logout-all ends the other session |
| A10, logs | Ready = Healthy; no password or refresh cookie value in API logs; reuse-detection security event present |

### Defects found and fixed during implementation

| Defect | Found by | Fix |
|--------|----------|-----|
| `GetMemberAsync` filtered on a projected DTO, which EF cannot translate (500 on role change) | Role-matrix and member tests | Filter before projecting (`WorkspaceRepository.MembersQuery`) |
| Frontend single-flight refresh promise was module-global, so one store's renewal could resolve into another | `SessionBootstrap` component test | Keyed per store `dispatch` (`shared/api/session.ts`) |
| nginx forwarded `Host` without the port, so the API's `Origin` check would refuse browser auth requests via `localhost:8080` | Design review before end-to-end run | `proxy_set_header Host $http_host` |
| Integration suite exhausted PostgreSQL connections as the parallel test hosts doubled (`53300`) | Full suite run | Test container `max_connections=500`; per-database `MaxPoolSize=10` (test harness only) |

### Not verified manually

- Browser flows were exercised through the HTTP API with browser-equivalent headers, plus 75
  component tests covering every screen and state in [ui-routes.md](./contracts/ui-routes.md).
  They were **not** clicked through in a real browser in this run.
- Cross-tab refresh coordination (Web Locks) cannot be exercised in jsdom. The server-side
  conflict path it relies on is covered by integration and end-to-end checks (409 → retry).
- Safari's handling of `Secure` cookies on `http://localhost` was not tested. The README documents
  the `RefreshCookie__Secure=false` workaround.
