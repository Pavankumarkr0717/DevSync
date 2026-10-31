# Feature Specification: Identity and Workspace Permissions

**Feature Branch**: `002-identity-and-permissions`

**Created**: 2026-10-30

**Status**: Draft

**Input**: User description: "Create specification S02 — Identity and Workspace Permissions for
DevSync: secure registration and login, short-lived access with rotating and revocable refresh
sessions, profile and logout, workspace membership with Owner/Editor/Viewer roles, owner-managed
membership, server-side authorization on every protected operation, login/registration and
protected areas in the web app, and clear unauthorized/forbidden/validation/expired-session
behavior. Out of scope: social login, Entra ID, SSO, MFA, Redis, distributed sessions, cloud
deployment."

## Context

Feature 001 (Workspace Foundation) delivered create, list and view of workspaces with durable
storage. It identified people with a **development-only identity**: three seeded users chosen
from a picker, with no password. That mechanism is explicitly not authentication (001 spec
FR-011). Feature 002 replaces it with real accounts and extends the single `owner` role into
Owner, Editor and Viewer.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Register and Sign In (Priority: P1)

A new person creates an account with their name, email and password, then signs in with their
email and password. Signed-in people reach their workspaces. Signed-out people are sent to sign
in.

**Why this priority**: Every other story depends on knowing who the user is. Without accounts,
no permission can be enforced.

**Independent Test**: Register a new account, sign out, sign in again, and reach the workspace
list. Wrong passwords and unknown emails are rejected with the same message.

**Acceptance Scenarios**:

1. **Given** no account exists for an email, **When** a person registers with a valid name, email
   and password, **Then** an account is created, they are signed in, and they land on their
   (empty) workspace list.
2. **Given** an account exists, **When** the person signs in with the correct email (in any letter
   case) and password, **Then** they are signed in and see their workspaces.
3. **Given** an account exists, **When** someone signs in with a wrong password, **Then** sign-in
   is rejected with a generic "email or password is incorrect" message.
4. **Given** no account exists for an email, **When** someone tries to sign in with it, **Then**
   the response is identical to scenario 3 (same status, same message, same shape).
5. **Given** a registration with an invalid email, a missing name, or a password that breaks the
   password rules, **When** it is submitted, **Then** it is rejected with field-level errors and
   no account is created.
6. **Given** an email that is already registered (in any letter case), **When** someone registers
   with it, **Then** registration is rejected and no second account is created (see FR-006 for
   the enumeration trade-off).
7. **Given** a signed-out person, **When** they open any workspace page directly, **Then** they
   are sent to sign in and, after signing in, returned to the page they asked for.

---

### User Story 2 - Stay Signed In Safely and Sign Out (Priority: P1)

A signed-in person keeps working without being asked to sign in again every few minutes. Their
session renews quietly. They can view their profile, sign out of the current device, or sign
out everywhere. Ended or expired sessions cannot be reused.

**Why this priority**: Short-lived access is only usable with safe renewal. Revocation is a core
security requirement of this feature.

**Independent Test**: Sign in and keep working past the access-credential lifetime without
interruption. Sign out, then confirm that neither the old access credential nor the old renewal
credential gets any data.

**Acceptance Scenarios**:

1. **Given** a signed-in person whose short-lived access has expired, **When** they keep using the
   app, **Then** access is renewed without them re-entering credentials, as long as their session
   is still valid.
2. **Given** a signed-in person, **When** they open their profile, **Then** they see their name,
   email and account creation date, and never any password or credential material.
3. **Given** a signed-in person, **When** they sign out, **Then** that session ends immediately.
   Its access credential and renewal credential are both rejected from then on.
4. **Given** a person signed in on two devices, **When** they choose "sign out everywhere",
   **Then** every session ends immediately on both devices.
5. **Given** a renewal credential that has already been used once, **When** anyone presents it
   again, **Then** it is rejected and the whole session it belongs to is ended (suspected theft).
6. **Given** a session past its maximum lifetime, **When** renewal is attempted, **Then** it is
   rejected and the person is asked to sign in again. The app tells them their session expired
   and returns them to where they were afterwards.
7. **Given** an access credential that has expired or is malformed, **When** it is used on any
   protected operation, **Then** the operation is rejected as unauthenticated.

---

### User Story 3 - Own My Workspaces Under My Account (Priority: P1)

A signed-in person creates, lists and views workspaces exactly as in feature 001. Every
workspace they create is owned by their account, and nobody else can see it unless granted
access.

**Why this priority**: This keeps 001 working under the new identity model, which is an explicit
acceptance requirement.

**Independent Test**: Two accounts each create a workspace. Each sees only their own in the list.
Opening the other's workspace gives "not found".

**Acceptance Scenarios**:

1. **Given** a signed-in person, **When** they create a workspace, **Then** their account is
   recorded as its Owner. All 001 validation rules (name and description limits, trimming) still
   apply.
2. **Given** a signed-in person, **When** they list workspaces, **Then** they see every workspace
   where they hold any role (Owner, Editor or Viewer), each with their own role, newest first.
3. **Given** a signed-in person who has no role in a workspace, **When** they try to view or
   change it, **Then** they get the same "not found" as for a workspace that does not exist (001
   FR-006 still holds).
4. **Given** the system is upgraded from 001, **When** it starts, **Then** existing 001 data is
   handled as described in FR-030.

---

### User Story 4 - Owners Manage Members and Roles (Priority: P2)

A workspace Owner adds people to the workspace as Editor or Viewer, changes their roles, and
removes them. Editors and Viewers can see who the members are but cannot change membership.

**Why this priority**: This is where collaboration starts. It depends on Stories 1–3.

**Independent Test**: The Owner adds an Editor and a Viewer, changes the Viewer to Editor, and
removes the Editor. Each person's access changes immediately. Editors and Viewers who try the
same actions are refused.

**Acceptance Scenarios**:

1. **Given** an Owner, **When** they add a person as Editor or Viewer through the membership
   workflow (FR-020), **Then** that person gains that role, and the workspace appears in their
   list with that role.
2. **Given** an Owner, **When** they change a member's role between Editor and Viewer, **Then** the
   new role takes effect on that member's very next request.
3. **Given** an Owner, **When** they remove a member, **Then** that member's next request for the
   workspace gets "not found", and the workspace disappears from their list.
4. **Given** an Editor or a Viewer, **When** they try to add, remove or change the role of any
   member, **Then** they are refused with a "forbidden" response, and membership is unchanged.
5. **Given** any member, **When** they view the workspace, **Then** they see the member list with
   each member's name and role. Members' email addresses are visible only to the Owner.
6. **Given** an Owner, **When** they try to add someone who is already a member, remove
   themselves, or change their own role, **Then** they are refused with a clear validation
   message (see FR-024).

---

### User Story 5 - Role-Appropriate Access to Workspace Content (Priority: P2)

Editors can change the workspace's editable content; Viewers can only read it. In this release
the editable content is the workspace's **name and description**. Later features (files,
diagrams) inherit the same rule.

**Why this priority**: This proves the Editor/Viewer distinction with a real operation. Without
one, the roles would be untestable.

**Independent Test**: An Owner, an Editor and a Viewer each try to edit the workspace name. The
Owner and Editor succeed; the Viewer is refused and nothing changes.

**Acceptance Scenarios**:

1. **Given** an Owner or Editor, **When** they change the workspace name or description with valid
   values, **Then** the change is saved and its last-updated time changes.
2. **Given** a Viewer, **When** they try to change the name or description, **Then** they get a
   "forbidden" response and nothing changes.
3. **Given** an Owner or Editor, **When** they submit an invalid name or description, **Then** the
   001 validation rules apply (field-level errors, nothing saved).
4. **Given** a Viewer, **When** they use the web app, **Then** edit controls are hidden or disabled
   for them. The server still refuses edits that bypass the UI.

---

### Edge Cases

- Email letter case and surrounding spaces: `Ana@Example.com ` and `ana@example.com` are the same
  account, for both registration and sign-in.
- Two registrations for the same email at the same moment: exactly one succeeds.
- Many failed sign-ins for one account or from one source: further attempts are temporarily
  slowed or refused (FR-008). Legitimate sign-in works again after the cooldown.
- A renewal credential presented twice concurrently, for example from two browser tabs: only
  one renewal succeeds. The client must survive the other being refused without signing the
  person out, if its session is still valid (FR-013).
- A member is removed or downgraded while they have the workspace open: their next action gets
  "not found" or "forbidden", and the UI explains that their access changed.
- The Owner tries to remove or downgrade themselves: refused, so a workspace always has exactly
  one Owner (FR-024).
- An Owner invites an email with no account: the invitation is created like any other, and
  whoever later registers that email sees it. This is an accepted risk, because emails aren't
  verified in this release.
- An invitation is accepted after it expires, or after the Owner revoked it: "not found", and no
  membership is created.
- Two invitations to the same email for one workspace: the second is refused while the first is
  pending.
- A person's account is used in a session while the database is unavailable: 503, as in 001. A
  database outage never grants access.
- A deliberately invalid or tampered access credential: rejected as unauthenticated. The error
  never says why it was invalid.
- A request that changes state comes from another website (cross-site): rejected (FR-016).
- Very long inputs (name, email, password): rejected with field errors. Passwords longer than the
  maximum are rejected, not silently truncated.

## Requirements *(mandatory)*

### Functional Requirements

**Accounts and sign-in**

- **FR-001**: People MUST be able to register with a display name (1–100 characters, trimmed), an
  email address, and a password.
- **FR-002**: Email addresses MUST be trimmed and compared case-insensitively. Each email can
  belong to at most one account. Emails MUST be syntactically valid and at most 254 characters.
- **FR-003**: Passwords MUST be 12–128 characters. Any characters are allowed, with no forced mix of
  character types. A password equal to the person's email is rejected. Passwords are never
  trimmed or truncated.
- **FR-004**: Passwords MUST be stored only as salted, deliberately slow one-way hashes produced by
  a widely reviewed password-hashing implementation. Plaintext or reversibly encrypted passwords
  MUST never be stored, logged or returned.
- **FR-005**: People MUST be able to sign in with email and password. A wrong password and an
  unknown email MUST produce the same response (status, message and body shape), and the server
  MUST do equivalent password-verification work in both cases.
- **FR-006**: Registration with an already-registered email MUST be rejected without creating or
  changing any account. Because there is no email delivery in this release, registration cannot
  hide whether an email is registered. This is an accepted, documented risk, limited by FR-008
  rate limits.
- **FR-007**: A successful registration MUST sign the person in.
- **FR-008**: Sign-in and registration attempts MUST be rate-limited per account email and per
  client source. After 5 failed sign-ins for one email within 15 minutes, further attempts for
  that email MUST be refused for 15 minutes, with the same generic message plus a "try again
  later" status.

**Sessions**

- **FR-009**: A successful sign-in or registration MUST start a **session**. A session provides a
  short-lived access credential (valid at most 15 minutes) and a renewal credential.
- **FR-010**: Renewal credentials MUST be single-use. Each renewal issues a new access credential
  and a new renewal credential, and invalidates the one presented (rotation).
- **FR-011**: A session MUST end after 7 days without renewal (idle limit) or 30 days after sign-in
  (absolute limit), whichever comes first. After that, the person must sign in again.
- **FR-012**: If an already-used renewal credential is presented again, the system MUST end the
  entire session it belongs to and record a security event.
- **FR-013**: Concurrent renewal attempts with the same renewal credential MUST result in at most
  one successful renewal. The web app MUST coordinate renewal so that normal multi-tab use does
  not trigger FR-012.
- **FR-014**: Renewal credentials MUST be stored server-side only in a non-reversible form (for
  example, a hash), never in plaintext.
- **FR-015**: In the browser, the renewal credential MUST NOT be readable by page scripts and MUST
  NOT be stored in localStorage, sessionStorage or IndexedDB. The access credential, if held by
  the page, MUST be kept in memory only.
- **FR-016**: If any credential is sent automatically by the browser (for example, a cookie),
  state-changing requests MUST be protected against cross-site request forgery, and credentials
  MUST be restricted to DevSync's own origin and to secure transport outside local development.
- **FR-017**: Signing out MUST end the current session immediately: its renewal credential is
  revoked, and its access credential is rejected on the next request, before its natural expiry.
- **FR-018**: People MUST be able to end all of their sessions at once ("sign out everywhere"),
  with the same immediacy as FR-017.
- **FR-019**: An authenticated profile view MUST return the person's id, display name, email and
  account creation time, and nothing else.

**Workspace membership and roles**

- **FR-020**: Owners MUST be able to add a person to a workspace as Editor or Viewer through an
  **invitation** to an email address *(resolved 2026-10-30)*:
  - The Owner invites any email address with a role (Editor or Viewer). The response is the same
    whether or not an account exists for that email, so it never reveals who is registered.
  - A signed-in person sees the pending invitations addressed to their account's email (compared
    case-insensitively) and can accept or decline each one. Accepting makes them a member with
    the offered role.
  - Invitations expire after 7 days. The Owner can list pending invitations and revoke them.
  - The Owner cannot invite their own email or an email that already belongs to a member. A
    workspace can have at most one pending invitation per email, and at most 50 pending
    invitations in total.
  - An invitation that is expired, revoked, already answered, or addressed to a different email
    behaves as "not found" for the person trying to use it.
- **FR-021**: Each workspace MUST have exactly one Owner. Each person MUST have at most one role
  per workspace. Roles are Owner, Editor and Viewer.
- **FR-022**: Owners MUST be able to change any other member's role between Editor and Viewer, and
  remove any other member.
- **FR-023**: Each workspace has exactly one Owner, its creator, for its whole life. Ownership
  cannot be transferred, and nobody can be invited or promoted to Owner in this release
  *(resolved 2026-10-30)*.
- **FR-024**: The Owner MUST NOT be able to remove themselves, change their own role, or add an
  existing member again. These attempts MUST be rejected with a validation error and leave
  membership unchanged.
- **FR-025**: Role and membership changes MUST take effect on the affected person's next request.
  Authorization MUST use the current stored membership, never a copy embedded in a credential.

**Authorization**

- **FR-026**: Every operation other than registration, sign-in, renewal and health checks MUST
  require a valid session, and MUST be authorized on the server.
- **FR-027**: Permissions per role:

  | Operation | Owner | Editor | Viewer | Non-member |
  |-----------|-------|--------|--------|------------|
  | View workspace, its details and member list | ✓ | ✓ | ✓ | not found |
  | Edit workspace name/description | ✓ | ✓ | forbidden | not found |
  | See members' email addresses | ✓ | — (hidden) | — (hidden) | not found |
  | Add member, change role, remove member | ✓ | forbidden | forbidden | not found |

- **FR-028**: Responses MUST distinguish these cases: *unauthenticated* (no valid session),
  *session expired* (the client should renew or sign in), *forbidden* (a member without the
  required role), *not found* (missing, or the caller is not a member), and *validation failed*
  (with field errors). All errors keep 001's error shape, with a stable code and correlation
  id.

**Migration from 001**

- **FR-029**: The development-only identity (header-selected users, user picker, dev-users
  listing) MUST be removed. No request may be authenticated by naming a user id.
- **FR-030**: The upgrade MUST delete all 001 users (all are password-less development users by
  construction), along with their workspaces and memberships, and then apply the new schema.
  The upgrade MUST log how many users and workspaces were removed. After it, the system starts
  with no accounts *(resolved 2026-10-30; 001 ran only locally, so no real data is lost)*.
- **FR-031**: All 001 workspace behaviors (create, list, details, validation, paging, error shape,
  correlation ids, health checks) MUST keep working for signed-in people, with roles replacing
  the single `owner` role.

**Web app**

- **FR-032**: The web app MUST provide registration, sign-in, profile, sign-out and "sign out
  everywhere" screens or actions. It must show field-level errors and the generic sign-in failure
  message.
- **FR-033**: Workspace screens MUST be reachable only while signed in. Signed-out visitors are
  redirected to sign in and returned to their original destination afterwards.
- **FR-034**: When a session expires or is revoked, the app MUST tell the person ("Your session
  has ended — please sign in again"), discard in-memory credentials and user data, and send them
  to sign in.
- **FR-035**: The app MUST show members and roles on the workspace details page. Owners get
  controls to add members, change roles and remove members; Owners and Editors get controls to
  edit name and description. Controls a person cannot use are hidden or disabled. Server checks
  remain authoritative.
- **FR-036**: A "forbidden" response MUST show "You don't have permission to do that". A role
  change while a page is open MUST be reflected after the next request.

### Non-Functional Requirements

- **NFR-001 (Secrets in logs)**: Passwords, credentials, renewal tokens, password hashes and
  Authorization or cookie header values MUST never appear in logs, error responses, or analytics.
  Logs MAY contain user ids and session ids.
- **NFR-002 (Security events)**: Sign-in success and failure, lockout, renewal-reuse detection,
  sign-out, sign-out-everywhere, and membership/role changes MUST be logged as structured events
  with correlation id, acting user id, and (for membership) the workspace id and target user id.
- **NFR-003 (Server-side enforcement)**: Every protected operation MUST be covered by automated
  tests across the full role matrix in FR-027 (Owner, Editor, Viewer, non-member,
  unauthenticated).
- **NFR-004 (Configuration)**: Signing keys and other secrets MUST come from configuration, never
  from source control. The app MUST refuse to start without them outside local development.
- **NFR-005 (Constitution)**: No new infrastructure (caches, brokers, external identity providers)
  is introduced. Sessions are stored in the existing database (Principle IX).

### Key Entities

- **User (account)**: A person who can sign in. It has a stable id, display name, unique email
  (case-insensitive), a password hash (never exposed), creation time, and lockout state. It
  extends the 001 `User`.
- **Session**: One signed-in device or browser for a user. It records when it started, last
  renewal time, idle and absolute expiry, revocation time and reason. It owns a chain of renewal
  credentials, of which only the latest is valid. Ending a session invalidates its access and
  renewal credentials.
- **Renewal credential record**: The non-reversible form of one renewal credential, which session
  it belongs to, when it was issued, and when it was used or revoked. Used to detect reuse
  (FR-012).
- **Workspace Membership**: Links a user to a workspace with role Owner, Editor or Viewer. It is
  unique per (workspace, user), with exactly one Owner per workspace. It extends 001's membership,
  which only allowed `owner`.
- **Invitation**: Records an offer to join a workspace. It has the workspace, invitee email
  (compared case-insensitively), offered role (Editor or Viewer), inviting Owner, status
  (pending, accepted, declined, revoked or expired), creation and expiry times, and who
  accepted it and when. At most one pending invitation exists per (workspace, email).
- **Workspace**: Unchanged from 001, except name and description become editable (US5), with
  `updated at` maintained.

Detailed tables, constraints and indexes are a planning output. The business rules above
(uniqueness, one Owner, one role per person per workspace, non-reversible secret storage) MUST
be enforced by the data store as well as by application logic.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new person can register and reach their workspace list in under 1 minute, and a
  returning person can sign in in under 15 seconds.
- **SC-002**: The full role matrix (FR-027 × 5 caller types) is covered by automated tests, and
  100% of them produce the expected outcome (allowed, forbidden, not found or unauthenticated).
- **SC-003**: After sign-out, sign-out-everywhere, renewal-reuse detection, or session expiry, 0
  requests using that session's access or renewal credentials succeed (verified by tests that
  retry each credential).
- **SC-004**: Sign-in with an unknown email and with a wrong password produce byte-identical
  response bodies (except correlation id) in 100% of tests.
- **SC-005**: In a full scan of the database, logs and API responses produced by the test suite,
  0 plaintext passwords or renewal credentials are found.
- **SC-006**: A signed-in person working continuously for 2 hours is never asked to sign in again
  (silent renewal works), while a session idle beyond its limit always requires signing in.
- **SC-007**: A removed or downgraded member loses the corresponding access on their very next
  request in 100% of tests.
- **SC-008**: All 001 acceptance scenarios still pass when run as signed-in accounts instead of
  development users.

## Out of Scope

- Social login (Google, GitHub, etc.), Microsoft Entra ID, enterprise SSO, SAML/OIDC providers.
- Multi-factor authentication.
- Email delivery of any kind: email verification, password reset, invitation emails.
- Changing email or password, editing the profile, and account deletion.
- Workspace deletion or archiving.
- Distributed or external session stores, Redis, and cloud deployment.
- Admin or super-user roles that span workspaces.
- Real-time notification of role changes (changes apply on the next request).

## Assumptions

- DevSync still runs locally in this release. Secure-transport rules (FR-016) are enforced
  everywhere except local development over plain HTTP, where the plan documents the exception.
- Without email verification, owning an email address is not proven. FR-020 options must account
  for this. For example, invitations to an email can be claimed by whoever registers that email.
- Session limits (15 min access, 7 day idle, 30 day absolute) and lockout values (5 attempts / 15
  min) are defaults chosen for this release. They are configurable, and changing them is not a
  spec change.
- "Workspace resources" means the workspace name and description in this release. Files and
  diagrams (future features) inherit FR-027's Editor/Viewer rule.
- Breached-password checks against external services are out of scope (no external calls).
- The technology stack (React, TypeScript, Redux Toolkit, ASP.NET Core, EF Core, PostgreSQL)
  and the request to use the framework's built-in authentication and authorization and a
  reviewed password hasher are recorded for the plan. The plan will choose the specific
  credential and cookie design that satisfies FR-009 to FR-018.
