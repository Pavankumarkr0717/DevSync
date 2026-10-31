# Quickstart & Validation: Identity and Workspace Permissions

End-to-end scenarios that prove feature 002 works. Contracts:
[openapi.yaml](./contracts/openapi.yaml), [ui-routes.md](./contracts/ui-routes.md). Rules:
[data-model.md](./data-model.md).

## Prerequisites and setup

Same as [001 quickstart](../001-workspace-foundation/quickstart.md). No auth secret is required
locally.

```bash
cp .env.example .env            # set DB_PORT if 5432 is taken
docker compose up --build -d
docker compose logs api | grep -i "removed"   # upgrading from 001: shows users/workspaces deleted by the migration
```

Use a fresh browser profile, or clear site data for `localhost:8080`, between runs.

## Browser scenarios (http://localhost:8080)

| # | Steps | Expected |
|---|-------|----------|
| B1 | Open `/workspaces` while signed out | Redirected to `/login?returnTo=%2Fworkspaces` |
| B2 | Register **Ana** (`ana@example.com`, 12+ char password) | Lands on the empty workspace list; header shows "Ana" |
| B3 | Register again with `ANA@example.com` | Email-field error; no second account |
| B4 | Sign out → sign in with the wrong password, then with `nobody@example.com` | The same "Email or password is incorrect" both times |
| B5 | Ana creates "Design Review" | Detail page shows Ana as Owner, plus the Edit, Members and Invitations panels |
| B6 | Ana invites `ben@example.com` as Editor and `cy@example.com` as Viewer | Both listed as pending |
| B7 | In a private window, register Ben and Cy; each opens `/invitations` and accepts | Workspace appears in their lists with the right role |
| B8 | Ben edits the name → "Design Review v2" | Saved; Ben sees no member or invitation controls |
| B9 | Cy opens the workspace | No Edit button; members visible without emails |
| B10 | Ana changes Cy to Editor; Cy edits the description | Allowed on Cy's next action without signing in again |
| B11 | Ana removes Ben; Ben refreshes the page | "Workspace not found"; gone from his list |
| B12 | Ana: Profile → Sign out everywhere; use the other open Ana tab | The other tab's next action shows "Your session has ended — please sign in again" |
| B13 | Inspect browser storage (DevTools → Application) | No token in local, session or IndexedDB storage; `devsync_rt` cookie is HttpOnly, SameSite Strict, Path `/api/auth` |

## API scenarios (curl)

All `/api/auth/*` calls need `-H "X-DevSync-Client: web"`. Use `-c/-b jar.txt` for the cookie.

| # | Call | Expected |
|---|------|----------|
| A1 | `POST /api/auth/login` without `X-DevSync-Client` | 403 `csrf_check_failed` |
| A2 | `POST /api/auth/login` wrong password vs unknown email | 401 `invalid_credentials`, identical bodies except `correlationId` |
| A3 | Six failed logins for one email (existing or not) | The 6th → 429 `too_many_attempts` |
| A4 | `POST /api/auth/refresh` with the jar, twice in sequence with the *old* cookie copy, more than 10 s apart | First 200 (rotated); replaying the old cookie → 401 `session_ended`; the newer cookie then also → 401 (session revoked) |
| A5 | `GET /api/workspaces` with the access token after `POST /api/auth/logout` | 401 `session_ended` |
| A6 | `GET /api/workspaces/{id}` as a non-member | 404 `not_found` |
| A7 | `PUT /api/workspaces/{id}` as a Viewer | 403 `forbidden` |
| A8 | `PATCH /api/workspaces/{id}/members/{ownerId}` as Owner | 400 `cannot_change_owner` |
| A9 | `POST /api/workspaces/{id}/invitations` for a registered vs unregistered email | Both 201 with the same body shape |
| A10 | `GET /health/ready` | `Healthy` |

## Automated tests

```bash
dotnet test --solution src/backend/DevSync.sln     # unit + integration (Docker running)
npm install --prefix src/frontend
npm run typecheck --prefix src/frontend && npm run lint --prefix src/frontend
npm test --prefix src/frontend
npm run build --prefix src/frontend
```

Expected: all pass, including the role matrix, the security scans and the ported 001 tests.

## Evidence

Record results (date, versions, pass/fail per scenario, deviations) in
`specs/002-identity-and-permissions/verification.md`.
