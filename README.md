# DevSync

DevSync is a browser-based collaborative engineering workspace for software developers,
interview-preparation groups, students and small teams. The long-term goal is shared code
editing, architecture diagrams and isolated code execution inside a workspace.

## What works today

| Milestone | Delivers |
|-----------|----------|
| [001 Workspace Foundation](specs/001-workspace-foundation/) | Create, list and view workspaces. PostgreSQL persistence. Structured logs, correlation ids, health checks. Docker Compose setup. |
| [002 Identity and Workspace Permissions](specs/002-identity-and-permissions/) | Accounts (register, sign in, sign out, sign out everywhere). Short-lived access tokens with rotating, revocable refresh sessions. Workspace roles: **Owner**, **Editor**, **Viewer**. Invitations by email. Owner-managed roles and removal. Editing a workspace's name and description. |

**Roles**

| | Owner | Editor | Viewer |
|-|:-:|:-:|:-:|
| View the workspace and its members | ✓ | ✓ | ✓ |
| Edit name and description | ✓ | ✓ | — |
| See members' email addresses | ✓ | — | — |
| Invite, change roles, remove members | ✓ | — | — |

People who aren't members get "not found", exactly as if the workspace didn't exist. The creator
is the only Owner, for good: ownership transfer is out of scope for now.

**Not included yet**, each needing its own approved spec: real-time collaboration, file editing,
diagrams, code execution, workspace deletion, email delivery (verification, password reset,
invitation emails), social login, SSO, MFA, and cloud deployment.

## Prerequisites

| Tool | Version | Needed for |
|------|---------|------------|
| Docker Desktop / Engine with Compose v2+ | recent | Running everything; backend integration tests |
| .NET SDK | 10.0.4xx | Native backend development and tests |
| Node.js + npm | 24 LTS | Native frontend development and tests |

## Quick start (everything in Docker)

```bash
cp .env.example .env          # placeholder values are fine locally; change ports if they clash
docker compose up --build -d
docker compose ps             # db is "healthy"; api and web are "running"
```

| URL | What |
|-----|------|
| http://localhost:8080 | DevSync UI. **Create an account** at `/register`. |
| http://localhost:8080/health/ready | `Healthy` when the API and database are up |
| http://localhost:5080/health/live | API liveness (direct) |

To try roles, register a second account in a private window. Invite its email from your
workspace's **Invitations** panel, then accept from the second account's **Invitations** page.

Stop with `docker compose down`. Use `docker compose down -v` to also delete the database volume.

> **Upgrading from 001:** the first start after upgrading deletes the 001 development users and
> their workspaces. They had no passwords and can't sign in. The API log says how many were
> removed.

## Native development

Run only the database in Docker, and the API and frontend on your machine:

```bash
cp .env.example .env
docker compose up -d db

# API on http://localhost:5080 (the launch profile sets Development and auto-migrate).
# Use the password and DB_PORT from your .env:
dotnet user-secrets set "ConnectionStrings:DevSync" \
  "Host=localhost;Port=5432;Database=devsync;Username=devsync;Password=change-me-local-only" \
  --project src/backend/DevSync.Api
# Optional: a fixed signing key so sign-ins survive API restarts without a renewal
dotnet user-secrets set "DevSync:Auth:SigningKey" "$(openssl rand -base64 32)" --project src/backend/DevSync.Api
dotnet run --project src/backend/DevSync.Api

# Frontend on http://localhost:5173 (Vite forwards /api and /health to :5080)
npm install --prefix src/frontend
npm run dev --prefix src/frontend
```

Instead of user-secrets you can export `ConnectionStrings__DevSync` and `DevSync__Auth__SigningKey`
in your shell.

## How sign-in works

- **Access token**: lasts 15 minutes. The browser keeps it in memory only and sends it as
  `Authorization: Bearer`.
- **Refresh token**: kept in an `HttpOnly`, `SameSite=Strict` cookie that is sent only to
  `/api/auth`. It is rotated on every renewal and stored on the server only as a hash. Reusing
  an old one ends the whole session.
- **Session limits**: a session ends after 7 days without use, or 30 days after sign-in.
  Signing out (or out everywhere) takes effect on the very next request.
- **Nothing in browser storage**: no credential is ever written to localStorage, sessionStorage
  or IndexedDB.

The design and its trade-offs are in
[docs/adr/0002-authentication-design.md](docs/adr/0002-authentication-design.md).

## Configuration

All settings come from environment variables. `.env.example` lists them with placeholders, and
your real `.env` is git-ignored.

| Variable | Default (`.env.example`) | Purpose |
|----------|--------------------------|---------|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | `devsync` / `devsync` / `change-me-local-only` | Database name and credentials |
| `ConnectionStrings__DevSync` | built by `docker-compose.yml` | API database connection |
| `ASPNETCORE_ENVIRONMENT` | `Development` | Environment name |
| `DevSync__Auth__SigningKey` | *(empty)* | Access-token signing key: base64, at least 32 bytes. Empty in Development uses a temporary key; **required elsewhere** (the API refuses to start without it). |
| `DevSync__Auth__RefreshCookie__Secure` | `true` | Send the refresh cookie only over HTTPS. Chrome, Edge and Firefox accept it on `http://localhost`; set `false` only for Safari or plain-HTTP hosts other than localhost. |
| `DevSync__Auth__AccessTokenMinutes`, `SessionIdleDays`, `SessionAbsoluteDays` | `15`, `7`, `30` | Optional overrides |
| `DevSync__Database__ApplyMigrationsOnStartup` | `true` | Apply EF Core migrations at startup |
| `DB_PORT`, `API_PORT`, `WEB_PORT` | `5432`, `5080`, `8080` | Host ports, bound to `127.0.0.1` only |

## Tests

```bash
dotnet test --solution src/backend/DevSync.sln   # unit + integration (Docker must be running)

npm install --prefix src/frontend                # also links tests/frontend/node_modules
npm test --prefix src/frontend                   # component tests in tests/frontend
npm run lint --prefix src/frontend               # app and tests
npm run typecheck --prefix src/frontend          # app and tests
npm run build --prefix src/frontend
```

Integration tests start their own PostgreSQL 18 container through Testcontainers. They never touch
the Compose database. They include:
- the full role-by-endpoint permission matrix,
- privilege-escalation attempts,
- refresh-token reuse detection,
- scans showing that passwords and tokens never reach the database, logs or responses.

CI ([.github/workflows/ci.yml](.github/workflows/ci.yml)) runs all of the above and builds the
container images on every push and pull request.

## API at a glance

The full contract is in
[specs/002-identity-and-permissions/contracts/openapi.yaml](specs/002-identity-and-permissions/contracts/openapi.yaml).
All `/api/auth/*` calls must send `X-DevSync-Client: web`.

| Method | Path | Who |
|--------|------|-----|
| GET | `/health/live`, `/health/ready` | Anyone |
| POST | `/api/auth/register`, `/api/auth/login` | Anyone (rate-limited) |
| POST | `/api/auth/refresh`, `/api/auth/logout` | Refresh cookie |
| POST | `/api/auth/logout-all` · GET `/api/me` | Signed in |
| POST / GET | `/api/workspaces` | Signed in (create; list your workspaces) |
| GET | `/api/workspaces/{id}` | Members |
| PUT | `/api/workspaces/{id}` | Owner, Editor |
| PATCH / DELETE | `/api/workspaces/{id}/members/{userId}` | Owner |
| GET / POST | `/api/workspaces/{id}/invitations` · DELETE `…/{invitationId}` | Owner |
| GET | `/api/invitations` · POST `/api/invitations/{id}/accept` · `…/decline` | The invited account |

Errors use RFC 9457 Problem Details with `code` and `correlationId`. 401 responses carry one of
three codes:
- `unauthenticated`: sign in.
- `token_expired`: renew and retry.
- `session_ended`: sign in again.

403 `forbidden` means you're a member without that permission. Every response carries an
`X-Correlation-ID` header. Quote that id when you report a problem.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `port is already allocated` | Change `DB_PORT`, `API_PORT` or `WEB_PORT` in `.env` |
| Integration tests fail to start a container | Start Docker Desktop |
| UI says "can't reach its database" | `docker compose ps`; restart with `docker compose up -d db` |
| Signed out after every page reload (Safari, or HTTP on a non-localhost host) | The browser rejects the `Secure` cookie: set `DevSync__Auth__RefreshCookie__Secure=false` for local use |
| "Too many attempts" when signing in | 5 failed sign-ins lock that email for 15 minutes; 20 auth requests per minute per client |
| API exits mentioning `DevSync__Auth__SigningKey` | Outside Development a key is required: `openssl rand -base64 32` |
| API exits saying the connection string is not configured | Set `ConnectionStrings__DevSync` or the user secret |
| Start over with an empty database | `docker compose down -v` |

## Repository layout

```text
.github/workflows/ci.yml     CI pipeline
docs/                        architecture overview, ADRs, benchmark policy
specs/                       feature specifications, plans, tasks, verification (Spec Kit)
src/backend/                 ASP.NET Core 10 modular monolith (DevSync.sln)
  DevSync.Domain             entities and rules: workspaces, roles, permissions, invitations, sessions
  DevSync.Application        use cases and ports
  DevSync.Infrastructure     EF Core, PostgreSQL, migrations, ASP.NET Core Identity
  DevSync.Api                HTTP endpoints, JWT auth, authorization, errors, logging, health
src/frontend/                React + TypeScript + Redux Toolkit (RTK Query), Vite
tests/backend/               DevSync.UnitTests, DevSync.IntegrationTests
tests/frontend/              component tests and test support
Directory.*.props, global.json   shared .NET build settings, package versions, SDK
docker-compose.yml, .env.example
.specify/                    project constitution and templates
```

Layout: [ADR 0001](docs/adr/0001-repository-layout.md). Architecture:
[docs/architecture/overview.md](docs/architecture/overview.md).

New migrations: `dotnet tool restore`, then
`dotnet ef migrations add <Name> --project src/backend/DevSync.Infrastructure --startup-project src/backend/DevSync.Infrastructure --output-dir Persistence/Migrations`.

Project rules are in [.specify/memory/constitution.md](.specify/memory/constitution.md).
