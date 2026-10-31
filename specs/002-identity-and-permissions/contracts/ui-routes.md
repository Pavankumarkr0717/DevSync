# UI Contract: Identity and Workspace Permissions

Supersedes [001 ui-routes](../../001-workspace-foundation/contracts/ui-routes.md). API shapes are
in [openapi.yaml](./openapi.yaml).

## Session states

| State | Shown |
|-------|-------|
| `checking` (app start, restoring the session from the refresh cookie) | Neutral full-page "Loading…" with no app chrome. No protected request is sent. |
| `signedOut` | Public routes only. Protected routes redirect to `/login?returnTo=<path>`. |
| `signedIn` | Header shows the display name, an "Invitations" link with a pending count, and a user menu (Profile, Sign out). |

When a session ends (`session_ended`, refresh failure, or another tab signing out), the app
clears the user, access token and API cache, and goes to `/login?returnTo=…` with the banner
**"Your session has ended — please sign in again."**

The development-user picker from 001 is removed.

## Routes

| Route | Access | Page and required states |
|-------|--------|--------------------------|
| `/login` | public (signed-in users → `/workspaces`) | Email and password fields; submit disabled while pending; `invalid_credentials` → "Email or password is incorrect" (one message for both cases); `too_many_attempts` → "Too many attempts. Try again in a few minutes."; session-ended banner when applicable; link to `/register`; on success go to `returnTo` (same-origin paths only) or `/workspaces` |
| `/register` | public | Name, email and password (with a hint "12–128 characters"); client checks mirror spec FR-001–FR-003; server field errors shown per field; `email_taken` → message on the email field; on success → `/workspaces` |
| `/` | protected | Redirects to `/workspaces` |
| `/workspaces`, `/workspaces/new` | protected | As in 001; list rows show the role (owner, editor or viewer) |
| `/workspaces/:workspaceId` | protected | Details as in 001, plus: **Edit** button (Owner and Editor) opening inline name/description editing with 001 validation; **Members** list (name, role, email for Owner only); Owner-only controls: role selector (Editor/Viewer) and Remove for each non-owner, with a confirmation before removing; Owner-only **Invitations** panel: invite form (email + role), pending list with Revoke; `not_found` → "Workspace not found"; `forbidden` → toast "You don't have permission to do that" and the view refetches |
| `/invitations` | protected | Pending invitations addressed to me: workspace name, role, inviter, expiry; Accept → navigates to the workspace; Decline → removed from the list; empty state "No pending invitations" |
| `/profile` | protected | Display name, email, member since; **Sign out** and **Sign out everywhere** (with confirmation) |
| `*` | public | "Page not found" |

## Behaviour rules

- The access token lives only in Redux state. Nothing auth-related goes to `localStorage`,
  `sessionStorage` or IndexedDB.
- Every request to `/api/auth/*` sends `X-DevSync-Client: web`.
- On 401 `token_expired`: one shared refresh, single-flight across tabs where the browser supports
  it, then retry the request once. On 409 `refresh_conflict`: retry the refresh once.
- `returnTo` accepts only paths starting with `/` and not `//`, to prevent open redirects.
- Controls a role can't use are hidden (Viewer: no Edit; non-Owner: no member or invitation
  controls). The server remains authoritative, and tests check both.
