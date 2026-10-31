# Quickstart & Validation: Workspace Foundation

Runnable scenarios that prove feature 001 works end to end. Contracts:
[openapi.yaml](./contracts/openapi.yaml), [ui-routes.md](./contracts/ui-routes.md).
Data rules: [data-model.md](./data-model.md).

## Prerequisites

- Docker Desktop (or Docker Engine) with Compose v2+, running
- For native development and tests: .NET SDK 10.0.4xx, Node.js 24 LTS with npm

## 1. Setup

```bash
cp .env.example .env          # placeholder values work for local use; edit if ports clash
```

If a local PostgreSQL already uses port 5432, set `DB_PORT` (e.g. `15432`) in `.env`.

## 2. Run everything in Docker

```bash
docker compose up --build -d
docker compose ps             # db healthy; api and web running
```

| URL | Expected |
|-----|----------|
| http://localhost:8080 | DevSync UI loads and redirects to `/workspaces` |
| http://localhost:8080/health/ready | `Healthy` |
| http://localhost:5080/health/live | `Healthy` |

## 3. Run natively (optional, for development)

```bash
docker compose up -d db
dotnet run --project src/backend/DevSync.Api      # :5080, applies migrations, seeds dev users
npm install --prefix src/frontend && npm run dev --prefix src/frontend   # :5173, proxies /api
```

The API needs the env vars from `.env` (see the Configuration table in [plan.md](./plan.md)).
The README documents setting them in the shell or via `dotnet user-secrets`.

## 4. Validation scenarios

Use the UI at http://localhost:8080, or the HTTP calls below. `A` = `0192f5a0-0000-7000-8000-000000000001`,
`B` = `0192f5a0-0000-7000-8000-000000000002`.

| # | Story | Steps | Expected |
|---|-------|-------|----------|
| V1 | US4 | Fresh clone → steps 1–2 only | All three URLs respond as above; no source edits needed |
| V2 | US1 | As A, create `"  Interview Prep  "` with a description | 201, `Location` header, name stored as `Interview Prep`, owner = A, one `owner` member |
| V3 | US1 | As A, create with name `"   "`; then with a 101-character name | 400 `validation_failed`, `errors.name` present; list unchanged |
| V4 | US1 | POST without `X-DevSync-User`, then with an unknown UUID | 401 `unauthenticated` both times |
| V5 | US2 | As A create a second workspace; list as A | Both, newest first, `role: owner` |
| V6 | US2 | List as B | `items: []`; UI shows the empty state |
| V7 | US3 | Get A's workspace as A | 200 with details and members |
| V8 | US3 | Get A's workspace as B; get a random UUID as A; get `/api/workspaces/not-a-uuid` as A | 404 `not_found` for all three, identical bodies apart from `correlationId`/`instance` |
| V9 | US4 | `docker compose restart api`, then repeat V5 and V7 | Same data (SC-003) |
| V10 | Edge | `docker compose stop db`; list as A; GET `/health/ready` | 503 `service_unavailable` with `correlationId`; readiness `Unhealthy`. Then `docker compose start db` |
| V11 | Obs | `docker compose logs api` after V2–V8 | JSON lines with `correlationId`; no request bodies or `X-DevSync-User` header values |

Example calls:

```bash
curl -i -X POST http://localhost:8080/api/workspaces \
  -H "Content-Type: application/json" -H "X-DevSync-User: <A>" \
  -d '{"name":"  Interview Prep  ","description":"Mock interviews"}'

curl -s "http://localhost:8080/api/workspaces?page=1&pageSize=20" -H "X-DevSync-User: <A>"
curl -i http://localhost:8080/api/workspaces/<id> -H "X-DevSync-User: <B>"
```

## 5. Automated tests

```bash
dotnet test --solution src/backend/DevSync.sln   # unit + integration (Docker must be running)
npm test --prefix src/frontend                   # component tests (tests/frontend)
npm run lint --prefix src/frontend && npm run build --prefix src/frontend
```

Expected: all pass; integration tests start their own PostgreSQL container and do not touch the
Compose database.

## 6. Reset

```bash
docker compose down -v                # removes containers and the database volume
```
