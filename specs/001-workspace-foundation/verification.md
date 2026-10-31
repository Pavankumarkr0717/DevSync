# Verification Evidence: Workspace Foundation

**Date**: 2026-10-29 | **Feature**: [spec.md](./spec.md) | **Tasks**: T071–T073

## Environment

| Tool | Version |
|------|---------|
| OS | Windows 11 (10.0.26300) |
| .NET SDK | 10.0.402 (tests via Microsoft.Testing.Platform) |
| Node.js / npm | 24.21.0 / 10.9.0 |
| Docker / Compose | 29.8.0 / v5.5.1 |
| PostgreSQL | `postgres:18-alpine` (Compose and Testcontainers) |

## Automated suites (T071)

| Command | Result |
|---------|--------|
| `dotnet build backend/DevSync.slnx --no-incremental` | Succeeded, **0 warnings** (warnings are errors) |
| `dotnet test --solution backend/DevSync.slnx` | **105 passed**, 0 failed (38 unit, 67 integration on real PostgreSQL 18) |
| `npm run typecheck --prefix frontend` | Passed |
| `npm run lint --prefix frontend` | Passed, no findings |
| `npm test --prefix frontend` | **30 passed**, 0 failed (7 files) |
| `npm run build --prefix frontend` | Succeeded |

## Security pass (T072)

| Check | Result |
|-------|--------|
| `.env` ignored by `.gitignore` | Yes (`.env`, `.env.*`, except `.env.example`) |
| Real secrets in source, config, Dockerfiles or Compose | None. Only the documented placeholder `change-me-local-only` (in `.env.example`, README, plan, tasks) and the test-only `Password=unused` for an unreachable host |
| `appsettings*.json` connection strings | None |
| Compose host ports | All bound to `127.0.0.1` |
| API container user | `uid=1654(app)`, non-root (`docker compose exec api id`) |
| Problem Details leak internals | No; V10 confirms no `Npgsql`, socket or exception text in the 503 body |
| Logs leak request content | No; V11 and `LoggingTests` |

## End-to-end quickstart (T073)

Procedure: `docker compose down -v`, delete `.env`, `cp .env.example .env`, then
`docker compose up --build -d`, then run every scenario in [quickstart.md](./quickstart.md) over
HTTP through the web container's proxy (`http://localhost:8080`). The checks were scripted with
curl.

**Deviation (environment, not product):** this machine runs a local PostgreSQL on port 5432, so
`DB_PORT=15432` was set in `.env`. This is the README's documented "port is already allocated"
fix. No source changes were needed.

| # | Scenario | Result |
|---|----------|--------|
| V1 | UI served, SPA fallback, `/health/ready` via web, `/health/live` direct | PASS |
| V2 | Create with spaces → 201, `Location`, trimmed name, owner A, one `owner` member | PASS |
| V3 | Blank and 101-char names → 400 `validation_failed` `errors.name`; nothing stored | PASS |
| V4 | No header / unknown user → 401 `unauthenticated` | PASS |
| V5 | List newest first, `role: owner` | PASS |
| V6 | User B sees an empty list | PASS |
| V7 | Owner sees full details | PASS |
| V8 | Non-member, missing and malformed id → identical 404 bodies (except `correlationId`) | PASS |
| V9 | `docker compose restart api` → identical list and details | PASS |
| V10 | `docker compose stop db` → 503 `service_unavailable`, header/body correlation ids match, readiness 503, recovery after `start db` | PASS (after fix below) |
| V11 | JSON logs with `CorrelationId`; no body text; no `X-DevSync-User` | PASS |

**Total: 31/31 checks passed** on the final clean run.

### Defect found and fixed during verification

- **Symptom**: V10 first returned **500 `internal_error`** instead of 503 when the database
  container was stopped.
- **Cause**: stopping PostgreSQL terminates pooled connections with `57P01 terminating connection
  due to administrator command`. `DatabaseErrors` treated every server-sent `PostgresException`
  as a non-outage. The integration test only covered a refused connection.
- **Fix**: `backend/src/DevSync.Infrastructure/Persistence/DatabaseErrors.cs` now classifies SQL
  states `08xxx`, `57P01`–`57P03` and `53xxx` as unavailable.
- **Regression test**: `backend/tests/DevSync.IntegrationTests/Errors/DatabaseOutageTests.cs`
  terminates the API's connections with `pg_terminate_backend`. It was confirmed to **fail
  without the fix** (1/1 failed) and pass with it.

### Not verified manually

- The UI was not clicked through in a real browser during this run. Browser behavior for
  V2/V5–V8 is covered by the component tests (create form, list, paging, details, not-found,
  503 and user switching), and the HTTP checks confirm the built SPA and proxy are served. A manual
  browser pass at http://localhost:8080 is still recommended.
