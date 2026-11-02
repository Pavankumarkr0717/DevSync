# Feature Specification: Real-Time Workspace Presence

**Feature Branch**: `005-workspace-presence`

**Created**: 2026-11-01

**Status**: Draft

**Input**: User description: "S05 — Real-Time Workspace Presence using ASP.NET Core SignalR:
authenticated workspace members connect to a real-time channel, join a workspace (checked on the
server on every join), and see who else is online, who joins, who leaves and who reconnects,
plus the state of their own connection. A user counts as online while at least one of their
connections (tabs, devices) is in the workspace. Automatic reconnection restores membership and
re-fetches the online list without duplicates. Presence shows in the workspace header across the
workspace pages, the code editor and the design canvas. Membership revocation and session
revocation are honoured for live connections. Presence is in memory on a single server instance,
documented with its limits and a path to distributed presence. Out of scope: collaborative code
or diagram editing, CRDT/OT, Yjs, cursors and selections, Redis, RabbitMQ, a backplane,
microservices, cloud deployment, code execution, chat."

## Context

- **001** gave each team a workspace.
- **002** added accounts, short-lived access tokens with server-side sessions (revocable at any
  time), and the Owner, Editor and Viewer roles, checked on the server for every operation.
- **003** added project files and a code editor; **004** added architecture diagrams. Both save
  one change at a time with conflict detection; nothing is live yet.

Feature 005 is DevSync's **first real-time feature**. It does not change how files or diagrams are
edited. It lets the people in a workspace **see each other**: who is here now, who just arrived,
who just left, and whether their own live connection is healthy. It also establishes the
real-time channel, its authorization rules and its connection lifecycle, which later
collaboration features will build on.

## Clarifications

### Session 2026-11-01

No questions were raised. The brief settled scope, security and out-of-scope items; the remaining
choices use the defaults recorded under **Assumptions** (one workspace per connection, a short
grace period before announcing someone offline, initials instead of avatar images, and no role
shown in presence).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See Who Is Online in the Workspace (Priority: P1)

Priya opens the "Checkout" workspace. The workspace header shows the avatars of the teammates
who are currently in it, for example Ben and Alex, with a count "3 online" (including herself).
Hovering or focusing the avatars lists their names. The header looks the same on the overview,
Files, the code editor, the Diagrams list and the design canvas.

**Why this priority**: Knowing who is around is the whole point of presence and the first value
a team gets from the real-time channel.

**Independent Test**: Sign in as two members of the same workspace in two browsers. Each sees the
other in the header with the count "2 online", on every workspace page.

**Acceptance Scenarios**:

1. **Given** a member opens any page of a workspace, **When** their live connection is
   established, **Then** they receive the current list of online members of **that** workspace
   and the header shows those members' avatars (initials) and the online count, themselves
   included.
2. **Given** the header shows online members, **When** the person hovers, focuses or opens the
   presence summary, **Then** they see each online member's display name, with "(you)" next to
   their own.
3. **Given** more online members than fit in the header, **When** it is shown, **Then** the first
   few avatars appear followed by a "+N" overflow, and the summary lists everyone.
4. **Given** a member alone in the workspace, **When** the header is shown, **Then** it shows
   "1 online" (themselves) and no one else.
5. **Given** two workspaces A and B, **When** members are online in A, **Then** nobody viewing B
   sees them.

---

### User Story 2 - Notice Teammates Joining and Leaving (Priority: P1)

While Priya edits a file, Alex opens the workspace. Priya sees a short notification "Alex joined
the workspace." and Alex's avatar appears. Later Alex closes his last tab; after a short moment
Priya sees "Alex left the workspace." and the avatar disappears.

**Why this priority**: Presence that does not update live is a stale list. Join and leave
updates are what make it real-time.

**Independent Test**: With Priya in the workspace, have Alex open it, then close it. Priya sees
exactly one "joined" and one "left" notification and the list updates both times.

**Acceptance Scenarios**:

1. **Given** members already in a workspace, **When** another member joins and was not already
   online there, **Then** every other online member is told once that they joined, sees "Alex
   joined the workspace.", and sees them in the list.
2. **Given** a member who is online, **When** their **last** connection to the workspace ends,
   **Then** after the grace period (FR-014) every other online member is told once that they
   left, sees "Alex left the workspace.", and no longer sees them in the list.
3. **Given** a member with a connection that ends and a new one that starts within the grace
   period (for example a page reload), **When** this happens, **Then** nobody is told they left
   or joined; they simply stay online.
4. **Given** a member's own other tabs or devices, **When** the member joins from another tab,
   **Then** they are not notified about themselves.
5. **Given** a connection that drops without closing (network loss, sleeping laptop), **When**
   the server detects it is gone, **Then** it is treated like a closed connection: the member is
   announced as having left only if no other connection of theirs remains.

---

### User Story 3 - Stay Online Across Tabs, Devices and Network Hiccups (Priority: P1)

Alex has the workspace open in two tabs and on a laptop. Closing one tab does not make him look
offline. When his Wi-Fi drops for a few seconds, his page shows "Reconnecting to DevSync…",
then "Connection restored." Afterwards he sees the correct online list, without duplicate
avatars, and his teammates saw no join/leave noise if he came back within the grace period.

**Why this priority**: Without correct multi-connection handling and reconnection, presence is
wrong most of the time in real use, which is worse than no presence.

**Independent Test**: Open the same workspace as one user in two tabs, close one, and confirm the
user stays online for others. Take the network down and up in a tab, and confirm the status
messages, the restored list and that no duplicate avatars or notifications appear.

**Acceptance Scenarios**:

1. **Given** a user with several connections in a workspace, **When** any but the last ends,
   **Then** they remain online for everyone and no "left" notification is sent.
2. **Given** a user with several connections, **When** the list is shown to anyone, **Then** the
   user appears **once**.
3. **Given** a lost connection, **When** the app is trying to reconnect, **Then** the person sees
   a "Reconnecting to DevSync…" status, and the presence list is marked as possibly out of date
   rather than silently kept as if live.
4. **Given** a reconnection succeeds, **When** the connection is back, **Then** the app rejoins
   the workspace it was showing (the server re-checks membership), replaces the online list with
   a fresh one, and shows "Connection restored."
5. **Given** reconnection keeps failing, **When** the retry schedule is exhausted, **Then** the
   person sees that live presence is unavailable and can retry by hand. The rest of the
   workspace (files, editor, diagrams) keeps working without presence.
6. **Given** the server restarts, **When** clients reconnect, **Then** presence is rebuilt from
   the members who reconnect; nobody stays "online" because of a connection the restart lost.

---

### User Story 4 - Only Members Can See or Join a Workspace's Presence (Priority: P1)

Mallory is signed in but not a member of "Checkout". Even if she edits the workspace id in her
browser or calls the real-time channel directly, she cannot join its presence, learn who is
online there, or be shown to its members. Unauthenticated visitors cannot connect at all.

**Why this priority**: The real-time channel is a new way into workspace data. It must be as
tightly authorized as the existing HTTP API (constitution IV).

**Independent Test**: Try to connect without signing in, try to join a workspace the user is not
a member of, and try to join with a malformed id. Every attempt is refused and no presence
information about the workspace is revealed.

**Acceptance Scenarios**:

1. **Given** no valid sign-in, **When** a client tries to open the live connection, **Then** it is
   refused.
2. **Given** a signed-in user who is not a member, **When** they ask to join the workspace,
   **Then** the request is refused with the same "not found" outcome as for a workspace that
   does not exist, and no presence is revealed or announced.
3. **Given** a malformed workspace id, **When** a join is requested, **Then** it is refused with a
   predictable "not found" outcome.
4. **Given** any join, **When** it is processed, **Then** the user's identity comes from their
   sign-in, never from anything the client sends, and their membership is checked at that
   moment.
5. **Given** Owner, Editor and Viewer members, **When** they join presence, **Then** all of them
   may see and be seen. Joining presence grants no ability to read or change workspace content
   beyond what their role already allows.

---

### User Story 5 - Lose Live Access When Access Is Taken Away (Priority: P2)

The Owner removes Ben from the workspace while Ben has it open. Ben immediately stops receiving
the workspace's presence, is shown that he no longer has access, and the others see that Ben
left. When Ben signs out everywhere, or his session is revoked, his open connections end too.

**Why this priority**: A real-time channel that outlives a revoked membership or session is a
security hole. It is P2 only because it follows from the joins being checked at all.

**Independent Test**: With Ben online, remove him as Owner and confirm Ben stops receiving
updates and the others see him leave. Sign Ben out everywhere and confirm his connections end and
cannot rejoin.

**Acceptance Scenarios**:

1. **Given** an online member, **When** the Owner removes them, **Then** the server stops
   delivering that workspace's presence to all of their connections at once, tells those
   connections that access was removed, and the remaining members see them leave.
2. **Given** a member whose role changes between Editor and Viewer, **When** it happens, **Then**
   their presence is unaffected (presence does not show roles).
3. **Given** a signed-in user, **When** they sign out, sign out everywhere, or their session is
   otherwise ended, **Then** the live connections belonging to the ended sessions are closed by
   the server and cannot rejoin until the user signs in again.
4. **Given** an access token that expires while the connection is open, **When** it expires,
   **Then** the connection is closed; the app obtains a new token through the normal session
   refresh and reconnects. If the session has ended, the person is sent to sign in, as for any
   expired session in 002.

---

### User Story 6 - Move Between Workspaces Without Stale Presence (Priority: P2)

Priya switches from "Checkout" to "Payments" and back, and later goes to her profile page. Each
workspace shows only its own online members, she is never shown as online in a workspace she is
not viewing, and leaving the workspace pages ends her live connection.

**Why this priority**: Navigation is where duplicate handlers, ghost presence and leaks appear.
It matters, but only once presence itself works.

**Independent Test**: Navigate A → B → A quickly while a second user watches each workspace. The
watcher sees Priya leave A and join B, then the reverse. No list ever mixes members of A and B,
and no notification is shown twice.

**Acceptance Scenarios**:

1. **Given** a member viewing workspace A, **When** they navigate to workspace B, **Then** they
   leave A (others in A see them leave after the grace period, unless they still have another
   connection in A) and join B.
2. **Given** a navigation that happens while the connection or a join is still in progress,
   **When** the earlier join finishes, **Then** its result is discarded and only the latest
   workspace is joined and shown.
3. **Given** a member leaving the workspace pages entirely (for example to their profile or the
   workspace list), **When** no workspace page remains open in that tab, **Then** the tab's live
   connection is closed.
4. **Given** repeated navigation, **When** events arrive, **Then** each event is handled once:
   no duplicate notifications, no duplicate avatars, and no members from another workspace.

---

### Edge Cases

- **Repeated join** of the same workspace on the same connection: accepted, returns the current
  list, announces nothing new.
- **Repeated leave**, or leave without a prior join: accepted, no effect, nothing announced.
- **Join while already in another workspace** on the same connection: the connection leaves the
  previous workspace first (one workspace per connection).
- **Disconnect reported twice**, or a disconnect for a connection already removed: no effect.
- **Two connections of one user ending at the same time**: the user is announced as having left
  exactly once.
- **Join and disconnect racing** (a tab closes while its join is in flight): the end state never
  keeps a connection that has gone, and no "joined" without a matching "left" is left behind.
- **A member whose display name changes**: presence shows the name read when they joined; this is
  refreshed on their next join. (Names cannot be changed in the product today.)
- **Membership revoked while a join is in flight**: the join either fails or is undone by the
  revocation; the revoked user never remains in the workspace's presence.
- **Network loss on the server side only** (the client still thinks it is connected): the
  connection is considered gone after the server's keep-alive timeout, and presence follows.
- **Live channel unavailable** (blocked by a proxy, server down): the workspace pages still work;
  only presence shows as unavailable.
- **Very large workspace** (all members online): the list shows the overflow "+N" and stays
  correct; the summary lists every online member.
- **The person's own presence**: they always see themselves in their list while connected, and
  never get join/leave notifications about themselves.

## Requirements *(mandatory)*

### Functional Requirements

**Live connection and identity**

- **FR-001**: The system MUST provide a live, two-way connection between a signed-in browser tab
  and the server for workspace presence.
- **FR-002**: Only requests carrying a valid, unexpired sign-in for an active session (002) MAY
  open the live connection; all others MUST be refused.
- **FR-003**: The server MUST take the user's identity solely from their sign-in. No user id,
  name, role or session sent by the client MAY be trusted or accepted.
- **FR-004**: A connection MUST be closed by the server when its access token expires, and
  connections belonging to a session MUST be closed when that session ends (sign out, sign out
  everywhere, revocation), so live access never outlives the sign-in it was opened with.

**Joining and leaving a workspace**

- **FR-005**: A connection MUST be able to ask to join a workspace by id. The server MUST check
  at that moment that the workspace id is well-formed, the workspace exists and the user is a
  member (any role), using the same membership data and rules as the HTTP API.
- **FR-006**: A refused join MUST return a predictable outcome without internal details: "not
  found" for malformed ids, missing workspaces and non-members alike (no existence leak), and
  "unauthenticated" for an ended session. A refused join MUST NOT reveal or announce any
  presence.
- **FR-007**: A connection MUST be in at most one workspace at a time; joining another workspace
  MUST first leave the current one.
- **FR-008**: A successful join MUST return the current list of online members of that workspace
  to the joining connection, including the joining user.
- **FR-009**: A connection MUST be able to leave its workspace. Leave MUST be idempotent: leaving
  twice, or without having joined, succeeds with no effect.
- **FR-010**: Repeated joins of the same workspace on the same connection MUST be idempotent: the
  current list is returned and nothing is announced.
- **FR-011**: Presence updates for a workspace MUST be delivered only to connections that have
  joined that workspace and are still authorized for it.

**Presence tracking**

- **FR-012**: A user MUST be considered online in a workspace while at least one of their
  connections has joined it. The list MUST contain each online user once, regardless of how many
  connections they have.
- **FR-013**: When a user goes from no connection to their first connection in a workspace, the
  other online members of that workspace MUST be notified once that the user joined.
- **FR-014**: When a user's last connection in a workspace ends or leaves, they MUST remain
  listed for a short grace period (default 5 seconds, configurable). If a new connection of
  theirs joins within it, nothing is announced; otherwise the other members MUST be notified once
  that the user left. A removal from the workspace (FR-020) is announced at once, with no grace
  period.
- **FR-015**: Connections that end unexpectedly (network loss, closed browser, server keep-alive
  timeout) MUST be treated like a leave.
- **FR-016**: Tracking updates MUST be safe under concurrent joins, leaves and disconnects of the
  same and different users, and duplicate or out-of-order disconnect reports MUST NOT produce
  wrong lists or duplicate notifications.
- **FR-017**: Presence information MUST contain only what members can already see in the
  workspace: the user id, display name, an avatar image address when one exists (none exists
  today, so initials are shown), and the online state. It MUST NOT contain emails, roles, session
  or connection identifiers, tokens or any other account detail.
- **FR-018**: Transient presence MUST NOT be written to the database. Workspace membership stored
  by 002 remains the sole source of truth for authorization.
- **FR-019**: Presence state is held in the running server and starts empty after a restart; it
  MUST be rebuilt only from connections that join again.

**Revocation**

- **FR-020**: When a member is removed from a workspace, the server MUST at once stop delivering
  that workspace's presence to all of the removed user's connections, tell those connections
  that their access was removed, and notify the remaining members that the user left.
- **FR-021**: A change of role (Editor ↔ Viewer) MUST NOT affect presence.

**Client behaviour**

- **FR-022**: The app MUST open at most one live connection per browser tab, and only while a
  workspace page is open in that tab; it MUST close the connection when no workspace page
  remains open.
- **FR-023**: On navigation between workspaces, the app MUST leave the previous workspace and
  join the new one, discard the results of joins for workspaces it is no longer showing, and
  never show presence from one workspace in another.
- **FR-024**: The app MUST reconnect automatically after an unexpected loss, on an increasing
  retry schedule (immediately, then about 2, 5, 10 and 30 seconds), and, after reconnecting,
  MUST rejoin the current workspace and replace its online list with the fresh one returned by
  the server. It MUST NOT rely on any earlier connection identifier.
- **FR-025**: When reconnection fails because the sign-in expired, the app MUST refresh the
  sign-in through the normal 002 session refresh and try again; if the session has ended, the
  person MUST be sent to sign in as for any expired session.
- **FR-026**: When automatic reconnection gives up, the app MUST show that live presence is
  unavailable, offer a manual retry, and keep the rest of the workspace usable.
- **FR-027**: The app MUST subscribe to each kind of presence event once per connection, so
  every event is processed once, even across reconnection and navigation.

**Presence interface**

- **FR-028**: Every workspace page (overview, Files with the code editor, Diagrams list and the
  design canvas) MUST show, in its header: the avatars (initials) of online members, the number
  of members online, and an online indicator on each avatar.
- **FR-029**: Avatars beyond the space available MUST collapse into a "+N" overflow. A presence
  summary, opened by hover, focus or click and usable by keyboard and screen readers, MUST list
  the display names of everyone online, marking the person's own entry with "(you)".
- **FR-030**: The app MUST show short, non-blocking notifications "<Name> joined the
  workspace." and "<Name> left the workspace.", never about the person themselves, and at most
  once per event.
- **FR-031**: The app MUST show the state of the live connection: connecting, connected (quiet,
  no banner), "Reconnecting to DevSync…" while reconnecting, "Connection restored." once on
  recovery, and "Live presence unavailable" with a retry when it has given up. While not
  connected, the presence list MUST be shown as possibly out of date.
- **FR-032**: When the person's own access is removed (FR-020), the app MUST stop showing the
  workspace's presence and tell them they no longer have access to the workspace.
- **FR-033**: The presence interface MUST follow the existing design system
  ([docs/design/design-system.md](../../docs/design/design-system.md)) in both themes; no new
  visual system is introduced.

**Observability and documentation**

- **FR-034**: Connections, joins, refused joins, leaves, disconnects and revocations MUST be
  logged as structured events with the user id and workspace id, never with tokens or the
  query string that may carry them.
- **FR-035**: Project documentation MUST describe the real-time architecture, the workspace group
  naming, the connection endpoint, every event and its payload, the sign-in and authorization
  flow, the connection lifecycle, reconnection, multi-connection handling, the limits of
  in-memory presence, the path to distributed presence, local test instructions, and a sequence
  diagram (User → app → real-time hub → authorization → workspace group → other users).

### Non-Functional Requirements

- **NFR-001 (Security)**: Every real-time operation that touches a workspace is authorized on the
  server at the time it is made (constitution IV). Presence grants no permission beyond the
  member's role.
- **NFR-002 (No secret leakage)**: Tokens never appear in logs, presence payloads or error
  messages, including when carried in a connection URL.
- **NFR-003 (Correctness under concurrency)**: Presence tracking is thread-safe; concurrency,
  reconnection and duplicate-event behaviour are covered by automated tests (constitution III).
- **NFR-004 (Isolation of failure)**: A failure of the live channel never breaks S01–S04
  features.
- **NFR-005 (Simplicity)**: Presence runs in the single API process with no new infrastructure
  (no Redis, message broker or backplane); the design keeps tracking behind one interface so a
  distributed store can replace it later.
- **NFR-006 (Regression)**: All S01–S04 behaviour and automated tests continue to pass.
- **NFR-007 (Accessibility)**: Presence changes and connection states are announced politely to
  screen readers; the summary is keyboard-operable.

### Key Entities

- **Live connection**: one open real-time link from a browser tab; belongs to one signed-in user
  and one session; in at most one workspace at a time; identified only on the server.
- **Workspace presence group**: the set of connections that joined one workspace; named
  `workspace:{workspaceId}`; receives that workspace's presence events.
- **Online member**: a user with at least one connection in a workspace group; shown as id,
  display name, optional avatar address and online state. Derived, never stored.
- **Presence event**: a message to a workspace group or to one connection: member joined, member
  left, online list, access removed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: When a member opens a workspace, the other online members see them appear within
  2 seconds in a local environment.
- **SC-002**: When a member closes their last tab, the others see them leave within the grace
  period plus 2 seconds; after an unannounced network loss, within the server's keep-alive
  timeout plus the grace period.
- **SC-003**: A page reload, or a network interruption shorter than the grace period, produces
  zero join or leave notifications for other members.
- **SC-004**: In every tested scenario (multiple tabs, multiple devices, reconnection,
  navigation A → B → A), each online member appears exactly once and every notification is shown
  exactly once.
- **SC-005**: 100% of join attempts by unauthenticated users, non-members and malformed ids are
  refused, with no presence revealed, in automated tests.
- **SC-006**: A removed member stops receiving the workspace's presence within 2 seconds of
  removal, and an ended session's connections are closed within 2 seconds of the session ending.
- **SC-007**: With the live channel unavailable, every S01–S04 acceptance test still passes, and
  all existing automated tests pass with the feature in place.
- **SC-008**: A developer can run the app locally and observe presence between two browsers by
  following the documented steps in under 10 minutes.

## Out of Scope

- Collaborative code editing, shared editor documents, CRDT or operational transformation, Yjs.
- Live cursors, selections or "who is viewing which file/diagram".
- Live diagram synchronization.
- Chat, messaging, typing indicators, "last seen" history.
- Redis, RabbitMQ, a distributed backplane, multiple API instances, microservices, cloud
  deployment.
- Code execution.
- Profile pictures (no avatar images exist; initials are shown).

## Assumptions

- **Single server instance**: presence lives in the memory of the one API process, as the
  existing rate limiter does. Running several instances would split presence; that needs the
  distributed path documented by FR-035 and is out of scope.
- **One workspace per connection**: a tab shows one workspace at a time, so one connection joins
  one workspace at a time. Several tabs on different workspaces use separate connections.
- **Grace period**: 5 seconds before announcing someone offline, to absorb reloads and brief
  network blips; configurable for tests and operations.
- **Initials, not images**: the product has no profile pictures; the avatar address is part of
  the payload for forward compatibility and is always empty today.
- **No roles in presence**: presence shows who is here, not their permissions; roles stay on the
  members list.
- **Sign-in for the live connection** reuses 002's short-lived access token (15 minutes by
  default); the connection closes when it expires and the client reconnects with a refreshed
  token, so revocation takes effect within one token lifetime at most, and at once for sign-out
  and session revocation (FR-004).
- **Keep-alive**: the server detects silent connection loss through its keep-alive timeout
  (about 30 seconds by default).
- **Workspace deletion** does not exist in the product; if it is added later it must also end the
  workspace's presence.
