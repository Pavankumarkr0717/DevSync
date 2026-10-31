# DevSync

DevSync is a browser-based collaborative engineering workspace for software developers,
interview-preparation groups, students and small teams. The long-term goal is shared code
editing, architecture diagrams and isolated code execution inside a workspace.

## What works today

| Milestone | Delivers |
|-----------|----------|
| [001 Workspace Foundation](specs/001-workspace-foundation/) | Create, list and view workspaces. PostgreSQL persistence. Structured logs, correlation ids, health checks. Docker Compose setup. |
| [002 Identity and Workspace Permissions](specs/002-identity-and-permissions/) | Accounts (register, sign in, sign out, sign out everywhere). Short-lived access tokens with rotating, revocable refresh sessions. Workspace roles: **Owner**, **Editor**, **Viewer**. Invitations by email. Owner-managed roles and removal. Editing a workspace's name and description. |
| [003 Project Files and Code Editor](specs/003-file-explorer-editor/) | A folder and file tree per workspace. Create, rename, move and delete. A Monaco code editor with syntax highlighting and up to 20 tabs. Debounced autosave with Saving / Saved / Save failed status. Conflicts instead of silent overwrites. Unsaved-edit drafts that survive a reload. |
| [004 System Design Canvas](specs/004-system-design-canvas/) | Architecture diagrams per workspace: 8 component types on a grid canvas, labelled directed connections, a properties panel with metadata, zoom, fit and a minimap. Autosave with the same statuses, conflicts and drafts as files. A dark-first, Google-style UI on Fluent 2 design tokens with Inter and JetBrains Mono, a light/dark toggle, a public landing page, role badges and file-type icons ([design system](docs/design/design-system.md)). |

**Roles**

| | Owner | Editor | Viewer |
|-|:-:|:-:|:-:|
| View the workspace and its members | ✓ | ✓ | ✓ |
| Edit name and description | ✓ | ✓ | — |
| See members' email addresses | ✓ | — | — |
| Invite, change roles, remove members | ✓ | — | — |
| Browse and read files | ✓ | ✓ | ✓ |
| Create, edit, rename and move files and folders | ✓ | ✓ | — |
| Delete files and folders | ✓ | — | — |
| Open and view diagrams | ✓ | ✓ | ✓ |
| Create, edit and rename diagrams | ✓ | ✓ | — |
| Delete diagrams | ✓ | — | — |

People who aren't members get "not found", exactly as if the workspace didn't exist. The creator
is the only Owner, for good: ownership transfer is out of scope for now.

**Not included yet**, each needing its own approved spec: real-time collaborative editing and
live cursors, file history or trash, Git integration, code execution, terminals, language
servers (IntelliSense, diagnostics), real-time diagram collaboration, diagram auto layout,
export or code generation, AI features, workspace deletion, email delivery, social login, SSO,
MFA, and cloud deployment. Several of these are planned in the [roadmap](#roadmap).

## Roadmap

Each milestone gets its own approved spec (Spec Kit) before implementation. New infrastructure
(SignalR, RabbitMQ, Redis, an execution sandbox) is introduced only by the milestone that needs
it, as the constitution requires.

| Milestone | Scope | Learning focus | Done when |
|-----------|-------|----------------|-----------|
| **S05 Real-time workspace presence** | ASP.NET Core SignalR, workspace groups, join/leave events and online participant indicators. | WebSockets, connection lifecycles, group membership, authorization, disconnects. | Two authorized browser sessions see presence changes without polling. |
| **S06 Concurrent editing and conflict resolution** | Synchronize text edits, selections and cursors. Choose Yjs or another established collaboration approach, and define reconnect and recovery semantics. | Concurrency, operation ordering, CRDT/OT trade-offs, idempotency, stale updates. | Simultaneous edits converge correctly under the defined test scenarios. |
| **S07 Live collaborative diagrams** | Synchronize node and edge operations, detect stale revisions, and recover the diagram after reconnects. | Event modeling, optimistic updates, revision control, state reconciliation. | Concurrent edits from multiple users result in a valid shared diagram. |
| **S08 Snapshots and version history** | Save recoverable versions of files and diagrams, browse revisions, and restore a selected version. | Transactions, snapshot design, retention, optimistic concurrency, audit trails. | A previous revision can be restored without corrupting current state. |
| **S09 Isolated code execution** | A separate execution worker using a restricted container or a purpose-built sandbox, with resource limits, execution timeouts and output capture. | Process isolation, resource quotas, threat modeling, job lifecycle, security testing. | Timeout, resource-exhaustion and malicious-input tests show that execution cannot access the host or application secrets. |
| **S10 Background jobs with RabbitMQ** | Queue execution requests and snapshot-generation jobs, with bounded retries, idempotent consumers, failure states and dead-letter handling. | Producer/consumer design, acknowledgements, delivery guarantees, retry policies, eventual consistency. | Transient failures recover without duplicate business effects, and poison messages can be inspected. |
| **S11 Observability and resilience** | Structured logs, metrics, distributed tracing, connection statistics, execution queue depth and failure dashboards. | OpenTelemetry, diagnostics, latency percentiles, fault injection, bottleneck analysis. | A request can be traced from the API through the queue to the execution worker. |
| **S12 Scaling and deployment readiness** | Redis-backed distributed coordination if needed, load tests, reconnect and message-throughput measurements, and an evaluation of cloud hosting costs. | Distributed systems, horizontal scaling, backpressure, load testing, operational trade-offs. | A documented benchmark establishes measured capacity and identifies the next bottleneck. |

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

## Files and editing

Open a workspace and choose **Open files** (`/workspaces/{id}/files`).

- **Files are database records**, never files on the server's disk. They are addressed by id,
  never by path.
- **Supported files**: `.cs .js .ts .tsx .jsx .json .html .css .py .java .md`. Anything else
  is refused, including `.txt`, `.yml` and `Dockerfile`.
- **Limits**:

  | Limit | Value |
  |-------|-------|
  | File size | 1 MiB, UTF-8 text |
  | Files and folders per workspace | 5,000 |
  | Folder depth | 20 |
  | Path length | 1,024 characters |
  | Open tabs | 20 |

  Names are unique within a folder, ignoring letter case.
- **Autosave**: saves 1 second after you stop typing, and at least every 10 seconds while you
  keep typing. Switching or closing a tab saves immediately. The status shows *Unsaved changes*,
  *Saving…*, *Saved* or *Save failed* with the reason. When the server can't be reached, it
  retries automatically, and a Retry button is always there.
- **Conflicts**: every save states the revision it was based on. If someone saved first, your
  save is refused and nothing is overwritten. You choose **Reload latest**, **Overwrite with
  mine** or **Copy mine**. There is no live co-editing: other people's renames and deletes
  appear when you return to the window.
- **Drafts**: unsaved text is kept in this browser and offered back next time you open the file.
  Signing out removes your drafts and remembered tabs. Drafts from a session that simply expired
  stay up to 7 days, so on a shared computer, sign out. A last save while closing the page is
  only attempted for edits up to 64 KiB (a browser limit); larger edits rely on the draft.
- **Delete** is permanent and Owner-only. The confirmation says how many items go with a folder.

The design and its trade-offs are in
[docs/adr/0003-project-file-storage.md](docs/adr/0003-project-file-storage.md). No new
environment variables are needed.

## Diagrams

Open a workspace and choose **Open diagrams** or the **Diagrams** tab (`/workspaces/{id}/diagrams`).

- **Components**: Client, API Gateway, Microservice, Database, Cache, Message Queue, Load
  Balancer and External Service. Drag one from the palette onto the canvas, or click it to place
  it in the middle of the view. Each has a title, a description and up to 20 key/value metadata
  entries. Select a component and use **Connect to…**, or drag from one handle to another, to
  add a directed connection with an optional label.
- **Connection rules** are structural only: any type may connect to any other, but not to
  itself, and only once in each direction. Deleting a component deletes its connections.
- **Navigation**: zoom from 10% to 400%, **Fit**, a minimap, and **Snap to grid** (20 px,
  remembered per person in this browser). The view is saved with the diagram on the next change.
- **Limits**:

  | Limit | Value |
  |-------|-------|
  | Diagrams per workspace | 50 |
  | Components / connections per diagram | 500 / 1,000 |
  | Name / title / label | 100 / 100 / 200 characters |
  | Description | 2,000 characters |
  | Diagram size | 1 MiB |

  Diagram names are unique within a workspace, ignoring letter case.
- **Saving** works like files: autosave after a pause at each finished change (a drag, a resize,
  a connection, an edit), with the same statuses, retries, conflict choices and drafts. Panning
  and zooming alone never save.
- **The server checks every save.** Unknown component types, connections to missing components,
  duplicates, oversized text and unknown fields are refused with the list of problems, and
  nothing is stored.
- **Newer formats**: a diagram saved by a newer DevSync version is shown with an explanation
  instead of being opened. Owners and Editors can **Start over**, which replaces it with an
  empty diagram after a confirmation.
- **Delete** is permanent and Owner-only. The confirmation names the diagram and its component
  count.

The design and its trade-offs are in
[docs/adr/0004-diagram-document-storage.md](docs/adr/0004-diagram-document-storage.md). The
visual language (colours, cards, the sidebar and dark mode) is described in
[docs/design/design-system.md](docs/design/design-system.md). No new environment variables are
needed.

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
- scans showing that passwords, tokens, file names, file content and diagram text never reach the logs,
- a test for every diagram document rule,
- concurrency races: ten simultaneous saves of one file or diagram, crossed folder moves, a save racing a delete.

`npm run check:bundle --prefix src/frontend` (after a build) checks that the code editor and
the diagram canvas are lazy-loaded and nothing is loaded from a CDN.

CI ([.github/workflows/ci.yml](.github/workflows/ci.yml)) runs all of the above and builds the
container images on every push and pull request.

## API at a glance

The full contract is in
[specs/002-identity-and-permissions/contracts/openapi.yaml](specs/002-identity-and-permissions/contracts/openapi.yaml)
for files,
[specs/003-file-explorer-editor/contracts/openapi.yaml](specs/003-file-explorer-editor/contracts/openapi.yaml),
and for diagrams,
[specs/004-system-design-canvas/contracts/openapi.yaml](specs/004-system-design-canvas/contracts/openapi.yaml).
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
| GET | `/api/workspaces/{id}/tree` · `/api/workspaces/{id}/files/{itemId}/content` | Members |
| POST | `/api/workspaces/{id}/items` · `…/items/{itemId}/rename` · `…/items/{itemId}/move` | Owner, Editor |
| PUT | `/api/workspaces/{id}/files/{itemId}/content` (raw text, `If-Match: "<revision>"`) | Owner, Editor |
| DELETE | `/api/workspaces/{id}/items/{itemId}` | Owner |
| GET | `/api/workspaces/{id}/diagrams` · `…/diagrams/{diagramId}` | Members |
| POST | `/api/workspaces/{id}/diagrams` · `…/diagrams/{diagramId}/rename` | Owner, Editor |
| PUT | `/api/workspaces/{id}/diagrams/{diagramId}/document` (JSON, `If-Match: "<revision>"`) | Owner, Editor |
| DELETE | `/api/workspaces/{id}/diagrams/{diagramId}` | Owner |

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
| "Someone else saved a newer version" | Another person (or another tab) saved first. Choose Reload latest, Overwrite with mine, or Copy mine |
| "Save failed: Couldn't reach the server — retrying" | The API or database is down; your text stays in the editor and as a draft, and saving resumes automatically |
| Start over with an empty database | `docker compose down -v` |

## Repository layout

```text
.github/workflows/ci.yml     CI pipeline
docs/                        architecture overview, ADRs, design system, benchmark policy
specs/                       feature specifications, plans, tasks, verification (Spec Kit)
src/backend/                 ASP.NET Core 10 modular monolith (DevSync.sln)
  DevSync.Domain             entities and rules: workspaces, roles, permissions, invitations, sessions, project files, diagrams
  DevSync.Application        use cases and ports
  DevSync.Infrastructure     EF Core, PostgreSQL, migrations, ASP.NET Core Identity
  DevSync.Api                HTTP endpoints, JWT auth, authorization, errors, logging, health
src/frontend/                React + TypeScript + Redux Toolkit (RTK Query), Monaco Editor, React Flow, Vite
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
