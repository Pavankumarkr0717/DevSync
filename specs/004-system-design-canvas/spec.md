# Feature Specification: System Design Canvas

**Feature Branch**: `004-system-design-canvas`

**Created**: 2026-10-31

**Status**: Draft

**Input**: User description: "S04 — System Design Canvas: a visual architecture-design canvas
where developers design, edit, save and reopen system architecture diagrams inside a workspace.
Nodes of typed components (Client, API Gateway, Microservice, Database, Cache, Message Queue,
Load Balancer, External Service) can be added, selected, moved, resized and deleted. Nodes are
connected by directed, labelled edges, and the canvas has a component palette and a node
configuration panel. Titles, descriptions and metadata are editable. Zoom, pan, fit-to-view,
selection and grid alignment are supported. Diagrams are saved as a versioned document of nodes,
edges and viewport, validated by the server, and the S02 roles are enforced. Multiple diagrams
per workspace if consistent with the product model. Clear empty states, validation messages and
save status. Out of scope: real-time sync, AI-generated architecture, automatic layout, code
generation, cloud export, collaborative cursors."

## Context

- **001** gave each team a workspace.
- **002** added accounts and the Owner, Editor and Viewer roles, checked on the server for every
  operation.
- **003** added a tree of source files with an editor, autosave, and conflict detection, so a
  stale save never silently overwrites newer work.

Feature 004 adds the second kind of workspace content: **architecture diagrams**. A diagram is a
canvas of typed components (boxes) joined by directed, labelled connections (arrows), with a
remembered view position and zoom.

**Multiple diagrams per workspace.** This follows the product model set by 003, where a
workspace holds many independent files. A team typically keeps several views of one system,
such as "Current", "Target" and "Payments flow". Each diagram is saved, versioned and
conflict-checked on its own.

As in 003, editing is **one save at a time**, not live: two people may have the same diagram
open, and the second save based on an outdated version is refused as a conflict instead of
overwriting the first person's work.

## Clarifications

### Session 2026-10-31

- Q: Should diagrams save automatically or only on Save? → A: Automatically, like files in 003:
  after changes pause, with the same status, conflict handling and browser draft (FR-026).
- Q: Which connections are refused? → A: Structural rules only: no self-connections, no duplicate
  same-direction edges, both ends must exist. Any component type may connect to any other
  (FR-016, FR-018).
- Q: Who may create, rename and delete diagrams? → A: As for files: Owner and Editor create and
  rename; only the Owner deletes (FR-030).
- Q: Should the product adopt the reference mockups' visual design
  ([docs/design/reference](../../docs/design/reference/))? → A: Yes, app-wide, as part of this
  feature: the existing pages and the new diagram pages follow
  [docs/design/design-system.md](../../docs/design/design-system.md). Only the look is adopted:
  features shown in the mockups that no spec covers are not built, and the product name stays
  DevSync (FR-039, FR-040).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create a Diagram and Lay Out Components (Priority: P1)

An Owner or Editor opens the workspace's diagrams, creates a diagram named "Checkout
architecture", and drags components from the palette onto the canvas: a Client, an API Gateway,
two Microservices and a Database. They move and resize them, snap them to the grid, and give each
a title.

**Why this priority**: Without diagrams and nodes there is nothing to connect, save or share.
This is the core of the feature.

**Independent Test**: Create a diagram, add five components of different types, move and resize
two of them, and confirm each appears where it was put, at its size, with its type's look.

**Acceptance Scenarios**:

1. **Given** a workspace with no diagrams, **When** a member opens the diagrams area, **Then**
   they see an empty state explaining what diagrams are. Owners and Editors also see an action to
   create one.
2. **Given** an Owner or Editor, **When** they create a diagram with a valid name, **Then** it
   opens on an empty canvas with a hint to drag components from the palette.
3. **Given** an open diagram, **When** a component type is dragged from the palette, or clicked
   in it, **Then** a node of that type appears with a default title and size, snapped to the
   grid when snapping is on.
4. **Given** a node, **When** it is dragged or its corner handles are dragged, **Then** it moves
   or resizes. Sizes stay within the allowed minimum and maximum (FR-012).
5. **Given** one or more selected nodes, **When** Delete is pressed or the delete action is used,
   **Then** the nodes **and every connection attached to them** are removed.
6. **Given** a diagram name that is empty, too long, or (ignoring case) already used by another
   diagram in the workspace, **When** it is submitted, **Then** it is rejected with a message
   saying why.

---

### User Story 2 - Connect Components and Describe Them (Priority: P1)

The developer draws arrows between components, for example "API Gateway → Orders Service"
labelled "REST", and uses the configuration panel to set each node's title, description and
metadata, such as `owner: payments-team` or `replicas: 3`.

**Why this priority**: The connections and descriptions are what make a set of boxes an
architecture. Without them the diagram has little value.

**Independent Test**: Connect four nodes with three labelled arrows. Edit one node's title,
description and two metadata entries. Edit one arrow's label. Confirm everything shows on the
canvas and in the panel.

**Acceptance Scenarios**:

1. **Given** two nodes, **When** a connection is drawn from one to the other, **Then** a directed
   edge appears with an arrowhead at the target.
2. **Given** an edge, **When** its label is edited, **Then** the label shows on the edge. An
   empty label is allowed.
3. **Given** a selected node, **When** the configuration panel is used, **Then** its title,
   description, type and metadata (key–value pairs) can be changed, within the limits of FR-012.
4. **Given** an attempt to connect a node to itself, or to add a second edge in the same
   direction between the same two nodes, **When** it is made, **Then** it is refused with a
   message, and nothing is added.
5. **Given** any two different component types (for example a Database and a Client), **When**
   they are connected in either direction, **Then** the connection is allowed. Only the
   structural rules in FR-016 refuse connections.
6. **Given** a node with edges, **When** the node is deleted, **Then** its edges are deleted too,
   so no edge is ever left pointing at a missing node.

---

### User Story 3 - Save, Reopen and Keep Work Safe (Priority: P1)

The developer's work is saved, and the save status is always visible. Closing the browser and
coming back shows the diagram exactly as they left it: the same nodes, positions, sizes, edges,
labels and view. If someone else saved the same diagram in the meantime, their work is not
overwritten silently.

**Why this priority**: A design tool that loses work is unusable, and a silent overwrite of a
teammate's diagram is worse than a lost edit.

**Independent Test**: Edit a diagram until it shows "Saved", reload the browser, and see the
same diagram and view. In two browsers, change the same diagram. The second save is reported as
a conflict, and the first person's version is still stored.

**Acceptance Scenarios**:

1. **Given** a change to a diagram (a node added, moved, resized or edited; an edge added,
   relabelled or removed), **When** changes pause, **Then** the diagram is saved automatically.
   The status shows "Unsaved changes", then "Saving…", then "Saved". A drag in progress is never
   saved halfway; it counts as one change when the drag ends.
2. **Given** a saved diagram, **When** the browser is reloaded or the diagram is reopened later,
   **Then** every node (type, title, description, metadata, position, size), every edge (ends,
   direction, label) and the view (position and zoom) are as saved.
3. **Given** a diagram open in two places at the same version, **When** both save changes,
   **Then** the first save succeeds and the second is refused as a conflict. Nothing is
   overwritten, and the second person can reload the latest version or overwrite it knowingly
   with their version, as in 003.
4. **Given** a save that fails (network, server, no permission, validation), **When** the failure
   is reported, **Then** the status shows "Save failed" with the reason, the person's changes
   stay on the canvas, and they can retry.
5. **Given** a diagram document stored in an **older** supported format version, **When** it is
   opened, **Then** it is upgraded to the current version and opens normally. The upgrade is
   stored the next time the diagram is saved.
6. **Given** a diagram document in a format version **newer** than this release understands, or
   one that is unreadable, **When** it is opened, **Then** a clear message says it cannot be
   opened by this version. The diagram is shown read-only or not at all, and **it is never
   overwritten** by this version.
7. **Given** unsaved changes, **When** the browser is reloaded or closed before they are saved,
   **Then** the app tries to save them at once and also keeps them as a draft in this browser.
   On return, a draft that differs from the saved diagram is offered to restore or discard. A
   restored draft keeps the revision it was based on, so a stale draft leads to a conflict, not
   an overwrite (as in 003).

---

### User Story 4 - Navigate Large Diagrams (Priority: P2)

On a large diagram the developer zooms in and out, pans around, uses fit-to-view to see
everything at once, selects several nodes with a selection box to move them together, and turns
grid snapping on or off.

**Why this priority**: Real architectures grow beyond one screen. Small diagrams are still usable
without this, so it follows the core stories.

**Independent Test**: In a diagram of 60 nodes, zoom to the minimum and maximum, pan to a far
corner, use fit-to-view, box-select 10 nodes and move them together, then toggle snapping and
move a node.

**Acceptance Scenarios**:

1. **Given** a diagram, **When** the person zooms with the controls, wheel or keyboard, **Then**
   the zoom changes in steps between the minimum and maximum (FR-019) and shows the current
   level.
2. **Given** a diagram, **When** the person drags the empty canvas, or uses the keyboard, **Then**
   the view pans.
3. **Given** a diagram with nodes, **When** fit-to-view is used, **Then** every node is visible,
   with a margin, within the zoom limits.
4. **Given** several nodes, **When** they are selected by box-select or with a modifier key and
   dragged, **Then** they move together and keep their relative positions.
5. **Given** grid snapping on, **When** a node is moved or resized, **Then** its position and
   size align to the grid. With snapping off, they move freely.
6. **Given** a saved view, **When** the diagram is reopened, **Then** it opens at that view. A
   diagram with no saved view opens fitted to its contents.

---

### User Story 5 - Role-Appropriate Access to Diagrams (Priority: P1)

The S02 roles decide what each member can do with diagrams. Viewers can open, pan, zoom and
inspect any diagram, including node details, but can change nothing, through the app or by
calling the server directly. Non-members cannot tell that the diagrams exist.

**Why this priority**: Diagrams describe a system's internals. A permission gap is a data leak,
and a Viewer able to change a design would undermine the role model.

**Independent Test**: As a Viewer, open a diagram, select a node to read its details, and pan
and zoom. Confirm there is no palette, no editing handles, no create, rename or delete action,
and that every change request sent directly to the server is refused.

**Acceptance Scenarios**:

1. **Given** a Viewer, **When** they open a diagram, **Then** they can pan, zoom, fit to view,
   and select nodes and edges to read their details. The palette, handles and editing controls
   are not offered, and the panel is read-only.
2. **Given** a Viewer, **When** they send a create, save, rename or delete request directly,
   **Then** it is refused as forbidden and nothing changes.
3. **Given** an Editor, **When** they edit and save a diagram, **Then** it is allowed.
4. **Given** an Editor, **When** they create or rename a diagram, **Then** it is allowed. **When**
   they try to delete a diagram, **Then** no delete action is offered, and a direct request is
   refused as forbidden. Only the Owner deletes diagrams.
5. **Given** a non-member, **When** they request the diagram list or any diagram of the
   workspace, **Then** they get the same "not found" as for a workspace that does not exist.
6. **Given** an Editor downgraded to Viewer while editing, **When** their next save arrives,
   **Then** it is refused, the canvas becomes read-only, and their unsaved changes stay visible.

---

### User Story 6 - Keep Several Independent Diagrams (Priority: P2)

A workspace holds several diagrams. Members see a list of diagrams with names and last-change
times, switch between them, and (with the right role) rename or delete them. Changes to one
diagram never affect another.

**Why this priority**: Teams need several views of a system, but one diagram is already useful.

**Independent Test**: Create "Current" and "Target". Add different nodes to each, rename
"Target" to "Target 2027", and delete "Current". Confirm "Target 2027" is unchanged throughout.

**Acceptance Scenarios**:

1. **Given** several diagrams, **When** the list is opened, **Then** each shows its name and when
   it was last changed, sorted by name ignoring case.
2. **Given** two diagrams, **When** one is edited and saved, **Then** the other's content,
   revision and view are unchanged.
3. **Given** a diagram, **When** it is renamed to a valid, unused name, **Then** its content and
   version history position (revision) are unchanged.
4. **Given** a diagram, **When** it is deleted after a confirmation stating its name and number
   of components, **Then** it is permanently removed and anyone who has it open is told it was
   deleted on their next save or reload.
5. **Given** the per-workspace diagram limit (FR-007) is reached, **When** another diagram is
   created, **Then** it is refused with a message.

---

### Edge Cases

- **Empty diagram**: a diagram with no nodes is valid and can be saved. Fit-to-view on an empty
  diagram resets to the default view.
- **Deleting a node with many edges**: all its edges are removed in the same action; saving never
  contains an edge to a missing node.
- **Same-direction duplicate**: A → B twice is refused. A → B plus B → A is allowed (two
  directions).
- **Self-connection** (A → A) is refused.
- **Overlapping nodes** are allowed; nothing is moved automatically (no auto-layout).
- **Nodes far off-screen**: positions are limited to the canvas bounds (FR-012). Fit-to-view
  always brings every node into view.
- **Text**: titles, labels, descriptions and metadata are plain text shown literally, never
  interpreted as markup. Control characters other than line breaks in descriptions are refused.
- **Metadata**: keys must be unique within a node (ignoring case); empty keys are refused; values
  may be empty.
- **Identifiers**: node and edge identifiers are unique within a diagram. A document that repeats
  one, or whose edge refers to an unknown node, is refused by the server even if the app should
  never produce it.
- **Unknown component type** in a submitted document is refused.
- **Very large documents**: beyond the node, edge or size limits, the save is refused with a
  message, and nothing on the canvas is lost.
- **Conflicting save** (another person saved first): refused as a conflict, never merged
  automatically.
- **Diagram deleted while open elsewhere**: the other person's next save fails with "This diagram
  was deleted". Their canvas stays visible so they can note what they had.
- **Session expiry while editing**: the S02 renewal applies. If it fails, unsaved changes are
  handled as in User Story 3.
- **Older format version**: upgraded on open (US3-AS5). **Newer or unknown version**: never
  overwritten (US3-AS6).
- **Unknown or malformed identifiers** in requests: treated as "not found".

## Requirements *(mandatory)*

### Functional Requirements

**Diagrams in a workspace**

- **FR-001**: A workspace MUST be able to hold several diagrams. A new workspace has none.
- **FR-002**: Members MUST be able to list a workspace's diagrams, showing each name and
  last-change time, sorted by name ignoring case.
- **FR-003**: Each diagram MUST have a stable identity that does not change when it is renamed.
- **FR-004**: Diagram names MUST be 1–100 characters after trimming, MUST NOT contain control
  characters, and MUST be unique within the workspace ignoring case.
- **FR-005**: A new diagram MUST start empty (no nodes, no edges) with the default view.
- **FR-006**: Deleting a diagram MUST remove it permanently after the app confirms with its name
  and its number of components. There is no trash or undo.
- **FR-007**: A workspace MUST NOT hold more than 50 diagrams.

**Nodes**

- **FR-010**: Each node MUST have one component type from this list: Client, API Gateway,
  Microservice, Database, Cache, Message Queue, Load Balancer, External Service. Each type MUST
  have a distinct look (icon and/or shape) and a default title and size.
- **FR-011**: Each node MUST have an identifier unique within its diagram, a type, a title, an
  optional description, optional metadata (key–value pairs), a position, and a size.
- **FR-012**: Limits per node:
  - **Title**: 1–100 characters.
  - **Description**: up to 2,000 characters; line breaks are allowed.
  - **Metadata**: up to 20 entries. Keys are 1–50 characters, unique ignoring case. Values are
    up to 500 characters.
  - **Size**: 40–2,000 units in width and in height.
  - **Position**: within ±100,000 units on each axis.

  No text field may contain control characters, except line breaks in the description.
- **FR-013**: Members with edit rights MUST be able to add nodes from the palette, select nodes
  (singly, by modifier key, or by box), move them, resize them, change their type, title,
  description and metadata, and delete them.
- **FR-014**: A diagram MUST NOT hold more than 500 nodes.

**Edges**

- **FR-015**: Each edge MUST have an identifier unique within its diagram, a source node, a
  target node (direction is source → target), and an optional label of up to 200 characters.
- **FR-016**: An edge's source and target MUST both be nodes of the same diagram. An edge MUST
  NOT connect a node to itself. There MUST NOT be two edges with the same source and target.
- **FR-017**: Deleting a node MUST delete every edge attached to it in the same action.
- **FR-018**: A diagram MUST NOT hold more than 1,000 edges. Apart from FR-016 there are no
  connection rules: any component type may connect to any other, in either direction.

**Canvas and view**

- **FR-019**: The canvas MUST support zoom between 10% and 400%, pan, fit-to-view, single,
  modifier and box selection, and a visible grid. Snapping to the grid MUST be on by default, can
  be switched off, and the choice is remembered per person in this browser.
- **FR-020**: Each diagram MUST store its view (position and zoom). A diagram without a stored
  view MUST open fitted to its contents.
- **FR-021**: The canvas MUST show an empty state for a diagram with no nodes. For people who
  can edit, it points to the palette.

**Saving, versions and conflicts**

- **FR-022**: A diagram's content MUST be saved as one versioned document: format version,
  nodes, edges and view. Saving replaces the whole document.
- **FR-023**: Each diagram MUST carry a revision that increases by one on every successful
  content save. A save MUST state the revision it was based on; if that is not current, the save
  MUST be refused as a conflict, and nothing changes.
- **FR-024**: The app MUST always show the current diagram's save state: saved, unsaved changes,
  saving, or save failed with the reason (conflict, deleted, invalid, too large, no permission,
  could not reach the server). After a failure the changes MUST stay on the canvas, and the
  person MUST be able to retry.
- **FR-025**: On a conflict, the person MUST be able to reload the latest version (discarding
  their changes) or overwrite it with their version, knowingly.
- **FR-026**: Diagrams MUST save automatically, without a separate save action. Saving starts
  after changes pause for 1 second, and happens at least every 10 seconds while changes continue.
  A move or resize in progress is never saved halfway. The app MUST NOT send a save while
  another save of the same diagram is in flight, or when nothing has changed since the last
  successful save. Switching diagrams or leaving the diagrams area MUST save pending changes
  immediately.
- **FR-026a**: When the page is reloaded or closed with unsaved changes, the app MUST try to save
  them immediately and keep them as a draft in this browser, recording the revision they were
  based on. A draft MUST be offered to restore or discard when the diagram is reopened and
  differs from the saved version. It belongs to one account and one diagram, is removed when
  saved, discarded or the account signs out, and is never shared with the server or other
  devices.

**Format versions**

- **FR-027**: The current document format version is **1**. Every stored document MUST record
  its version.
- **FR-028**: When a document of an older supported version is opened, it MUST be upgraded to the
  current version before it is shown, and stored in the current version on the next save.
- **FR-029**: A document of a newer or unknown version, or one that cannot be read, MUST NOT be
  edited or overwritten by this release. The person MUST see a clear message, and the server
  MUST refuse saves that would replace it, unless the person explicitly chooses to start the
  diagram over.

**Authorization**

- **FR-030**: Diagram operations MUST follow this table, enforced on the server for every request:

  | Operation | Owner | Editor | Viewer |
  |-----------|:-----:|:------:|:------:|
  | List and open diagrams, inspect nodes and edges, pan, zoom | ✓ | ✓ | ✓ |
  | Edit and save diagram content | ✓ | ✓ | ✗ |
  | Create and rename diagrams | ✓ | ✓ | ✗ |
  | Delete diagrams | ✓ | ✗ | ✗ |

- **FR-031**: Non-members MUST receive the same "not found" for the diagram list and any diagram
  as for a workspace that does not exist. Members without permission MUST receive "forbidden".
  The member's current role MUST be checked on every request (S02).
- **FR-032**: Diagrams MUST be addressed by workspace and diagram identity. A diagram identity
  from another workspace MUST be treated as "not found".

**Validation: the app and the server**

- **FR-033**: The **server is authoritative**. Before storing a document it MUST check:
  - the format version;
  - that the structure is well formed;
  - that node and edge identifiers are unique;
  - that every edge's source and target are existing nodes of the document;
  - no self-connections and no duplicate edges;
  - only known component types;
  - every text, number and count limit in FR-004 and FR-012–FR-019.

  Any failure MUST refuse the whole save. Nothing is partially stored, and the response MUST
  identify the problem (for example "edge e7 refers to unknown node n12"). Each kind of failure
  MUST have its own stable error code.
- **FR-034**: The **app** MUST prevent what it can before saving: no self-connections or
  duplicate edges can be drawn, deleting a node removes its edges, and fields enforce their
  limits as the person types. These checks give instant feedback only; they never replace the
  server's checks.

**Web app**

- **FR-035**: The workspace page MUST link to the diagrams area. The diagrams area MUST show the
  diagram list and the open diagram with its palette (for people who can edit), canvas,
  configuration panel and save status.
- **FR-036**: Each user-visible validation failure MUST show a message that says what is wrong
  and, where possible, which node, edge or field.

**Visual design (UI amendment, clarified 2026-10-31)**

- **FR-039**: All pages MUST follow the DevSync design system
  ([docs/design/design-system.md](../../docs/design/design-system.md)): the sidebar shell (an icon
  rail on narrow screens), top bar with breadcrumbs, card-based lists, indigo accent, and light
  and dark themes. The canvas page MUST use the floating palette and properties panels and the
  card-style nodes shown in the reference.
- **FR-040**: Restyling MUST NOT add, remove or change behaviour of existing features. Every
  existing action, message and permission rule stays as specified in 001–003. Features that
  appear only in the mockups MUST NOT be added.

**Compatibility**

- **FR-037**: All S01, S02 and S03 behaviour (workspaces, accounts, roles, invitations, files,
  editor) MUST keep working unchanged.

### Non-Functional Requirements

- **NFR-001**: Diagram content (titles, descriptions, metadata, labels) MUST NOT appear in logs.
- **NFR-002**: The role × operation table in FR-030 MUST be covered by automated tests for every
  role, a non-member and an anonymous caller, through direct server calls.
- **NFR-003**: Server-side document validation MUST have an automated test for every rule in
  FR-033, including duplicate identifiers, dangling references and unsupported versions.
  Conflict detection MUST be tested with concurrent saves, and autosave timing (FR-026) with
  automated tests.
- **NFR-004**: No new infrastructure services. Diagrams are stored in the existing database.

### Key Entities

- **Diagram**: A named architecture diagram in one workspace. It has a stable identity, a name
  unique in the workspace, a revision, created and last-changed times, and who last saved it. It
  owns exactly one current document.
- **Diagram document**: The saved content of a diagram as one versioned unit: format version,
  nodes, edges, and view. It is replaced whole on each save.
- **Node**: A component on the canvas. It has an identifier unique in the document, a component
  type, a title, a description, metadata (key–value pairs), a position and a size.
- **Edge**: A directed connection from a source node to a target node in the same document, with
  an identifier unique in the document and an optional label.
- **View**: Where the canvas is looking (position) and how far it is zoomed.
- **Component type**: One of the eight fixed kinds. It determines a node's look and default
  title and size.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A member can create a diagram, add five typed components, connect them with four
  labelled arrows and see it saved in under 3 minutes.
- **SC-002**: After a diagram shows "Saved", 100% of reloads show the same nodes, edges, labels,
  positions, sizes and view.
- **SC-003**: 100% of documents with duplicate identifiers, dangling references, self-connections,
  duplicate edges, unknown types, unsupported versions or exceeded limits are refused by the
  server in automated tests, and the stored diagram is unchanged afterwards.
- **SC-004**: In every tested race between two saves of one diagram from the same revision,
  exactly one succeeds and the other is reported as a conflict.
- **SC-005**: 100% of diagram-changing requests from Viewers, non-members and anonymous callers
  are refused in automated tests, and stored diagrams are unchanged.
- **SC-006**: Changing or deleting one diagram never changes another diagram in the same
  workspace, in automated tests.
- **SC-007**: Every S01, S02 and S03 automated test still passes.
- **SC-008**: A diagram at the node and edge limits (500 nodes, 1,000 edges) opens, pans, zooms
  and saves without the page becoming unresponsive (manual check).
- **SC-009**: Dragging a node across the canvas sends exactly one save (after the drag ends and
  changes pause), not one per movement, in automated tests.

## Out of Scope

- Real-time multi-user synchronization, presence, and collaborative cursors.
- AI-generated architecture, automatic layout or tidy-up, and code generation from diagrams.
- Export to cloud storage. Image, PDF or file export and import are also out for this release.
- Custom component types, icons or colours beyond the eight types; grouping, nesting or swimlanes.
- Edge waypoints and routing control; edges are drawn as straight or simple curved arrows.
- Version history, trash, and diff between diagram revisions.
- Linking diagram nodes to project files from 003.
- Undo and redo across saves. Whether in-session undo is offered is a planning decision; it is
  not required.

## Assumptions

- Diagrams follow 003's product model, confirmed in clarification: several independent items per
  workspace, each with its own revision and conflict check, autosave, Owner-only delete, and no
  live merging.
- Drafts hold diagram content, not credentials, so keeping them in the browser is consistent with
  S02's storage rule, as for files in 003.
- "Units" for position and size are canvas units. At 100% zoom one unit is one screen pixel.
- The grid is 20 units. Snapping rounds position and size to multiples of the grid.
- Metadata is free-form text key–value pairs for documentation (owner, technology, SLA…). The
  system does not interpret it.
- Changing a node's type keeps its title, description, metadata, position and size.
- The view stored with a diagram is shared: whoever saves last sets the view that others open
  at. Per-person views are not stored on the server.
- Existing workspaces start with no diagrams after the upgrade.
- Limits (50 diagrams, 500 nodes, 1,000 edges, text lengths) are starting values and can be
  revised later.
