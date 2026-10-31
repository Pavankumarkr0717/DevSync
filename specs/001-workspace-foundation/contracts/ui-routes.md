# UI Contract: Workspace Foundation

Browser routes and the observable behavior component tests assert. API shapes are in
[openapi.yaml](./openapi.yaml).

## Global shell

- Header shows a **Dev user** picker populated from `GET /api/dev/users`.
- The selected user is stored in Redux and in `localStorage` (read/write wrapped in try/catch;
  it falls back to "no user selected").
- With no user selected, workspace pages show "Choose a development user to continue" and make
  no workspace requests.
- Every workspace request sends `X-DevSync-User`. Workspace queries include the user id in their
  cache key, so data cached for one user is never shown to another after switching.

## Routes

| Route | Page | States that MUST be rendered |
|-------|------|------------------------------|
| `/` | Redirects to `/workspaces` | — |
| `/workspaces` | Workspace list | loading; empty ("You have no workspaces yet" + create link); list (name, role, created date, newest first); previous/next paging when `totalCount > pageSize`; error with correlation ID |
| `/workspaces/new` | Create form | name (required) and description fields; client-side checks for required name and lengths; server `errors` shown next to matching fields; submit disabled while pending; on 201 navigate to details |
| `/workspaces/:workspaceId` | Details | loading; details (name, description or "No description", owner, created date, members with roles); 404 → "Workspace not found" (same text for missing and forbidden); error with correlation ID |
| `*` | Not found | "Page not found" + link to `/workspaces` |

## Error display

- 401 → prompt to choose a development user.
- 503 → "DevSync can't reach its database right now. Try again shortly." plus the correlation ID.
- Other errors → the Problem Details `title` plus the correlation ID. Raw response bodies or stack
  traces are never shown.
