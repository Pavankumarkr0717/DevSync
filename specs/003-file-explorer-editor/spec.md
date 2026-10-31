# Feature Specification: Project Files and Code Editor

**Feature Branch**: `003-file-explorer-editor`

**Created**: 2026-10-31

**Status**: Draft

**Input**: User description: "S03: project file explorer with nested folders; create, rename,
delete and move files and folders; common programming file types (.cs, .js, .ts, .tsx, .jsx,
.json, .html, .css, .py, .java, .md); a code editor with syntax highlighting and multiple files
open in tabs; files saved durably on the server; debounced autosave with Saving / Saved / Save
Failed status; optimistic concurrency so newer content is never silently overwritten; S02 roles
enforced (Owner full access, Editor create and modify, Viewer read-only); file-size limits and
server-side validation; content preserved across navigation and browser refresh. Out of scope:
real-time collaborative editing, live cursors, Git integration, code execution, terminals,
language servers, cloud storage."

## Context

Feature 001 delivered workspaces. Feature 002 added accounts and per-workspace roles (Owner,
Editor, Viewer), checked on the server for every operation. Workspaces have a name and
description but no content yet. Feature 003 gives each workspace a tree of folders and text
source files that members can browse and, depending on their role, edit in the browser.

Editing is **single-writer at a time per save**: two people may have the same file open, but
there is no live merging. When two saves race, the later one is refused as a conflict rather
than overwriting the other person's work. Real-time co-editing is a later feature.

## Clarifications

### Session 2026-10-31

- Q: May Editors delete files and folders? → A: No. Only the Owner deletes; Editors create, edit,
  rename and move (FR-015, FR-024).
- Q: Are text files other than the 11 listed extensions allowed? → A: No. Only the 11 extensions;
  everything else is rejected (FR-010).
- Q: What happens to unsaved edits on reload or close? → A: Save immediately and also keep a
  draft in this browser, offered for restore on return (FR-035–FR-037).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Build a Project Tree (Priority: P1)

An Owner or Editor opens a workspace and sees its file explorer. They create folders inside
folders and create source files in any folder. The tree shows folders before files, each sorted
by name, and folders can be expanded and collapsed.

**Why this priority**: Nothing can be edited until files exist. The tree is the backbone of
every other story.

**Independent Test**: In an empty workspace, create `src/`, `src/api/` and `src/api/server.ts`,
reload the page, and see the same tree.

**Acceptance Scenarios**:

1. **Given** an empty workspace, **When** an Editor opens it, **Then** the explorer shows an empty
   state with actions to create a file or folder at the top level.
2. **Given** a folder, **When** an Editor creates a folder inside it, **Then** the new folder
   appears nested under its parent, and nesting can continue to the depth limit (FR-007).
3. **Given** a folder, **When** an Editor creates a file named with a supported extension
   (FR-010), **Then** an empty file appears in that folder and opens in the editor.
4. **Given** a name that is invalid (FR-005), uses an unsupported extension, or matches an
   existing item in the same folder ignoring letter case, **When** it is submitted, **Then** it is
   rejected with a message that says why, and nothing is created.
5. **Given** a tree with items, **When** anyone with access reloads the page, **Then** the same
   tree is shown.

---

### User Story 2 - Open and Edit Files with Autosave (Priority: P1)

A member opens a file from the explorer. It opens in the editor with syntax highlighting chosen
from its extension. Owners and Editors type; the change is saved automatically shortly after
they pause, and a status shows Unsaved changes, Saving, Saved or Save Failed. Reloading the
browser shows the saved content.

**Why this priority**: Editing and keeping code is the core value of the feature.

**Independent Test**: Open `server.ts`, type a few lines, wait for "Saved", reload the browser,
reopen the file, and see the typed lines.

**Acceptance Scenarios**:

1. **Given** a file with a supported extension, **When** a member opens it, **Then** its content
   is shown with syntax highlighting for that language.
2. **Given** an open file, **When** an Editor types continuously, **Then** the status shows
   "Unsaved changes" and no save is sent for each keystroke; a save is sent once typing pauses
   for the autosave delay, or at the latest after the maximum wait (FR-020).
3. **Given** a save in progress, **When** it is in flight, **Then** the status shows "Saving";
   when the server accepts it, the status shows "Saved".
4. **Given** a save that fails (network loss, server error, permission removed, size limit),
   **When** the failure is received, **Then** the status shows "Save Failed" with the reason, the
   person's edits stay in the editor, and they can retry.
5. **Given** a file saved as "Saved", **When** the browser is reloaded, **Then** the file shows
   the saved content.
6. **Given** unsaved edits, **When** the person switches to another file, closes the tab, or
   leaves the workspace, **Then** a save is sent immediately instead of waiting for the delay.
7. **Given** unsaved edits, **When** the person reloads or closes the browser before they are
   saved, **Then** the app tries to save them immediately and also keeps them as a draft in this
   browser. On return, if the draft was not saved, the person is offered to restore it or
   discard it (FR-035, FR-036).
8. **Given** content that exceeds the file-size limit (FR-016), **When** a save is attempted,
   **Then** the save is rejected, the status shows "Save Failed: file too large", and the edits
   stay in the editor.

---

### User Story 3 - Never Lose Someone Else's Newer Work (Priority: P1)

Two members have the same file open. Each sees the version they loaded. When the first saves, the
second person's next save is refused as a conflict instead of overwriting the first person's
change. The second person is told and chooses what to do.

**Why this priority**: Silent overwrites destroy work and trust. Without this, autosave makes
data loss more likely, not less.

**Independent Test**: Open the same file in two browsers signed in as two Editors. Save in
browser A, then edit and save in browser B. Browser B shows a conflict, and browser A's content
is still on the server.

**Acceptance Scenarios**:

1. **Given** a file at revision N open in two places, **When** place A saves, **Then** the file
   becomes revision N+1.
2. **Given** place B still based on revision N, **When** B saves, **Then** the save is refused as
   a conflict, nothing is overwritten, and B's status shows the conflict.
3. **Given** a conflict, **When** the person views it, **Then** they can (a) reload the latest
   version and discard their edits, (b) keep their edits and overwrite the latest version
   knowingly, or (c) copy their edits before choosing. Autosave pauses for that file until they
   choose.
4. **Given** the same person has the file open in two browser tabs, **When** both save, **Then**
   the same conflict rule applies.
5. **Given** a file that was renamed or moved by someone else, **When** a person with it open
   saves, **Then** the save succeeds against the same file (identity does not depend on its
   path), and the tab shows the new name.
6. **Given** a file that was deleted by someone else, **When** a person with it open saves,
   **Then** the save fails with "This file was deleted", and their edits stay in the editor so
   they can copy them.

---

### User Story 4 - Organize: Rename, Move and Delete (Priority: P2)

An Editor renames files and folders and moves them to other folders (including the top level).
The Owner can also delete them. Deleting a folder deletes everything inside after a confirmation
that says how many items will be removed.

**Why this priority**: Projects need restructuring, but a fixed tree is still usable for editing.

**Independent Test**: As an Editor, rename `server.ts` to `app.ts` and move `src/api` under
`lib/`. As the Owner, delete `lib/` after confirming. Confirm the tree and open tabs update, and
that the Editor is offered no delete action.

**Acceptance Scenarios**:

1. **Given** a file or folder, **When** an Editor renames it to a valid, unused name, **Then** it
   keeps its content and children, and open tabs show the new name.
2. **Given** a file, **When** it is renamed to a different supported extension, **Then** syntax
   highlighting follows the new extension. Renaming to an unsupported extension is rejected.
3. **Given** a file or folder, **When** an Editor moves it to another folder, **Then** it and its
   whole subtree appear under the new parent.
4. **Given** a folder, **When** someone tries to move it into itself or one of its descendants,
   **Then** the move is rejected.
5. **Given** a move or rename whose target folder already has an item with that name (ignoring
   case), or that would exceed the depth limit, **When** it is submitted, **Then** it is rejected
   and nothing changes.
6. **Given** a folder with contents, **When** the Owner deletes it, **Then** they are asked to
   confirm with the number of files and folders inside; on confirmation, the folder and its whole
   subtree are permanently removed and their tabs close.
7. **Given** an Editor, **When** they look for a delete action or send a delete request directly,
   **Then** no action is offered and the request is refused as forbidden.
8. **Given** two people acting on the same item at once (one renames while the other deletes or
   moves), **When** both requests arrive, **Then** each either fully applies or is refused; the
   tree is never left half-changed.

---

### User Story 5 - Role-Appropriate Access to Files (Priority: P1)

The S02 roles decide what each member can do with files. Viewers can browse and read but cannot
change anything, through the web app or by calling the server directly. Non-members cannot tell
that the workspace's files exist.

**Why this priority**: File content is the most sensitive data in the product so far. A
permission hole here is a data breach.

**Independent Test**: As a Viewer, open a file and see it read-only with no create, rename, move
or delete actions. Then send each file-changing request directly to the server and see each one
refused.

**Acceptance Scenarios**:

1. **Given** a Viewer, **When** they open the explorer and a file, **Then** they can browse and
   read; the editor is read-only and change actions are not offered.
2. **Given** a Viewer, **When** they send any create, save, rename, move or delete request
   directly, **Then** it is refused as forbidden and nothing changes.
3. **Given** a non-member, **When** they request the tree or any file of the workspace, **Then**
   they get the same "not found" response as for a workspace that does not exist.
4. **Given** an Editor or Owner, **When** they create, edit, rename or move files and folders,
   **Then** it is allowed. Only the Owner can delete; an Editor's delete is refused as forbidden.
5. **Given** an Editor downgraded to Viewer while editing, **When** their next save arrives,
   **Then** it is refused as forbidden, the editor becomes read-only, and their unsaved text stays
   visible so they can copy it.
6. **Given** a member removed from the workspace while it is open, **When** they next act on it,
   **Then** they get "not found" and are returned to their workspace list.

---

### User Story 6 - Work Across Several Open Files (Priority: P2)

A member opens several files at once, each in its own tab. They switch between tabs without
losing their place or unsaved edits, and close tabs individually. Each tab shows the file name
and its own save status. After a browser refresh, the same tabs reopen.

**Why this priority**: Real work spans several files, but a single open file is still useful.

**Independent Test**: Open three files, edit two, switch between them, see each tab's own status,
refresh the browser, and see the same three tabs with the saved content.

**Acceptance Scenarios**:

1. **Given** a file is already open, **When** it is opened again from the explorer, **Then** the
   existing tab is focused instead of opening a duplicate.
2. **Given** several open tabs, **When** the person switches tabs, **Then** each tab keeps its
   content, scroll position and cursor, and shows its own save status.
3. **Given** two open files with the same name in different folders, **When** both are open,
   **Then** the tabs show enough of the path to tell them apart.
4. **Given** open tabs, **When** the browser is refreshed, **Then** the same tabs reopen in the
   same order with the same active tab, showing current saved content.
5. **Given** the open-tab limit (FR-031) is reached, **When** another file is opened, **Then** the
   person is told to close a tab first.

---

### Edge Cases

- **Empty file**: a file with no content is valid and can be saved.
- **Content exactly at the size limit**: accepted; one byte over is rejected.
- **Non-text content**: content must be text (UTF-8). Content with a NUL character is rejected.
- **Line endings**: line endings are stored exactly as sent; the editor does not rewrite them.
- **Name case**: `Readme.md` and `README.md` cannot coexist in one folder; renaming an item to a
  different letter case of its own name is allowed.
- **Name rules**: names with `/`, `\`, control characters, characters invalid on common operating
  systems (`< > : " | ? *`), leading or trailing spaces, a trailing dot, or the names `.` and
  `..` are rejected (FR-005).
- **Dotfiles**: names like `.eslintrc.json` are allowed when the final extension is supported.
- **Path traversal**: items are addressed by identifier, never by a client-supplied path, so
  `../` in a request cannot reach anything outside the workspace.
- **Moving to the same place**: moving an item to its current folder is a successful no-op.
- **Deleting an open file elsewhere**: the other person's tab shows "This file was deleted" on
  its next save or refresh and keeps their unsaved text for copying.
- **Deleting a folder while a child is being saved**: one of the two operations wins; the save
  either succeeds before the delete or fails with "deleted".
- **Quick successive edits**: a new edit during a save is held and sent after that save finishes;
  saves for one file are never sent in parallel.
- **Network loss**: saves fail with "Save Failed"; edits stay in the editor; saving resumes on the
  next edit or a retry.
- **Session expiry while editing**: the S02 silent renewal applies. If renewal fails, the person
  is sent to sign in and their unsaved edits are handled as in User Story 2, scenario 7.
- **Workspace and item limits reached**: creating beyond the item limit (FR-008) is rejected
  with a clear message.
- **Very long lines or many lines** within the size limit open and stay editable.
- **Unknown or malformed identifiers**: treated as "not found".
- **Draft older than the server copy**: restoring it leads to a conflict on the next save, never
  a silent overwrite (FR-036).
- **Draft for a deleted file**: removed without being offered.
- **Browser storage unavailable or full** (private mode, quota): drafts are not kept; editing and
  autosave work normally.

## Requirements *(mandatory)*

### Functional Requirements

**File tree**

- **FR-001**: Each workspace MUST have a tree of folders and files with a single, implicit top
  level. A new workspace starts empty.
- **FR-002**: Members MUST be able to view the whole tree of a workspace they belong to. Folders
  are listed before files, each sorted by name ignoring letter case.
- **FR-003**: Each folder and file MUST have a stable identity that does not change when it is
  renamed or moved.
- **FR-004**: Names MUST be unique within a folder, ignoring letter case.
- **FR-005**: Names MUST be 1–255 characters. They MUST NOT contain `/`, `\`, `< > : " | ? *` or
  control characters, MUST NOT start or end with a space, MUST NOT end with a dot, and MUST NOT
  be `.` or `..`.
- **FR-006**: The full path of an item (all folder names plus its own, separated by `/`) MUST NOT
  exceed 1,024 characters.
- **FR-007**: Folders MUST NOT be nested deeper than 20 levels, counting the top level as 0.
- **FR-008**: A workspace MUST NOT hold more than 5,000 files and folders in total.

**Supported file types**

- **FR-010**: Files MUST have one of these extensions, compared ignoring letter case: `.cs`,
  `.js`, `.ts`, `.tsx`, `.jsx`, `.json`, `.html`, `.css`, `.py`, `.java`, `.md`. Creating or
  renaming a file to any other extension, or to no extension, MUST be rejected. This includes
  common text files such as `.txt`, `.yml` or `Dockerfile`. Folders have no extension rule.
- **FR-011**: The editor MUST apply syntax highlighting for the language implied by the file's
  extension.

**Structure operations**

- **FR-012**: Owners and Editors MUST be able to create files and folders at the top level or in
  any folder.
- **FR-013**: Owners and Editors MUST be able to rename files and folders. Renaming keeps
  content and children.
- **FR-014**: Owners and Editors MUST be able to move a file or folder (with its subtree) into
  another folder or to the top level. Moving a folder into itself or one of its descendants MUST
  be rejected, as must a move that breaks FR-004, FR-006 or FR-007.
- **FR-015**: Only the Owner MUST be able to delete files and folders. Deleting a folder MUST
  delete its whole subtree permanently. The web app MUST confirm before deleting, stating how
  many files and folders will be removed. There is no trash or undo.

**Content and saving**

- **FR-016**: File content MUST be UTF-8 text without NUL characters and MUST NOT exceed
  1 MiB (1,048,576 bytes) when encoded as UTF-8.
- **FR-017**: Saved content MUST be stored durably on the server and MUST be returned exactly
  as saved, byte for byte, including line endings.
- **FR-018**: Each file MUST carry a revision that increases by one on every successful content
  save.
- **FR-019**: A save MUST state the revision it was based on. If that is not the file's current
  revision, the save MUST be refused as a conflict, the stored content MUST NOT change, and the
  response MUST include the current revision.
- **FR-020**: The web app MUST save automatically after typing pauses for 1 second, and at least
  every 10 seconds during continuous typing. It MUST NOT send a save per keystroke, MUST NOT
  send two saves for one file at once, and MUST NOT send a save when the content has not
  changed since the last successful save.
- **FR-021**: The web app MUST save pending edits immediately when the person switches files,
  closes a tab or leaves the workspace.
- **FR-022**: Each open file MUST show one save status: Unsaved changes, Saving, Saved, or Save
  Failed (with the reason: conflict, deleted, too large, no permission, or could not reach the
  server). On Save Failed, edits MUST stay in the editor and the person MUST be able to retry.
- **FR-023**: On a conflict, the web app MUST pause autosave for that file and let the person
  either reload the latest version (discarding their edits) or overwrite it with their edits.
  Overwriting is a new save based on the latest revision. Their edits MUST remain copyable until
  they choose.

**Authorization**

- **FR-024**: File operations MUST follow this table, enforced on the server for every request:

  | Operation | Owner | Editor | Viewer |
  |-----------|:-----:|:------:|:------:|
  | View tree, open and read files | ✓ | ✓ | ✓ |
  | Create files and folders | ✓ | ✓ | ✗ |
  | Edit and save file content | ✓ | ✓ | ✗ |
  | Rename and move files and folders | ✓ | ✓ | ✗ |
  | Delete files and folders | ✓ | ✗ | ✗ |

- **FR-025**: Non-members MUST receive the same "not found" response for a workspace's tree or
  items as for a workspace that does not exist. A member without permission MUST receive
  "forbidden". This matches S02 FR-027/FR-028.
- **FR-026**: Roles MUST be checked against the member's current role on every request (S02
  FR-025), so a downgrade or removal applies to the next save.
- **FR-027**: Items MUST be addressed by workspace and item identity. An item identity from
  another workspace MUST be treated as "not found".

**Validation and errors**

- **FR-028**: All rules in FR-004–FR-008, FR-010, FR-014 and FR-016 MUST be enforced by the
  server regardless of what the web app checks. Each rejection MUST return a stable error code
  distinct from the others (for example: invalid name, duplicate name, unsupported extension,
  too deep, too many items, path too long, invalid move, content too large, invalid content,
  conflict).
- **FR-029**: Structure changes (create, rename, move, delete) MUST each be all-or-nothing.
  Concurrent structure changes MUST NOT leave duplicate names in a folder, cycles, or orphaned
  items.

**Web app**

- **FR-030**: The workspace page MUST show the explorer beside the editor area. Viewers MUST see
  no create, rename, move or delete actions, and the editor MUST be read-only for them. Editors
  MUST see no delete action.
- **FR-031**: Several files MUST be openable at once in tabs (up to 20). Opening an open file
  focuses its tab. Tabs show the file name, plus enough of the path to tell same-named files
  apart, and their save status.
- **FR-032**: Switching tabs MUST keep each tab's unsaved content, cursor and scroll position.
- **FR-033**: After a browser refresh, the web app MUST reopen the same tabs in the same order
  with the same active tab, showing the current saved content. Tabs for files that no longer
  exist or are no longer accessible are dropped silently.
- **FR-034**: Moves MUST be possible from a menu action ("Move to…" with a folder picker).
  Drag-and-drop MAY also be offered.

**Unsaved-edit drafts**

- **FR-035**: When a page is reloaded or closed with unsaved edits, the web app MUST try to save
  them immediately and MUST also keep them as a draft in this browser, recording the revision
  they were based on. A draft MUST be removed as soon as its content is saved successfully.
- **FR-036**: When a file with a draft is opened again in that browser, the web app MUST offer to
  restore or discard the draft. Restoring puts the draft in the editor as unsaved edits based on
  the draft's revision, so if the file changed since, the next save is a conflict (FR-019,
  FR-023) instead of an overwrite.
- **FR-037**: Drafts MUST belong to one account and one file. They MUST be removed when that
  account signs out in that browser, MUST NOT be shown to another account, and MUST be removed
  when the file no longer exists or is no longer accessible. Drafts are not shared with the
  server or other devices.

**Compatibility**

- **FR-038**: All S01 and S02 behaviour (workspaces, accounts, sessions, roles, invitations)
  MUST keep working unchanged.

### Non-Functional Requirements

- **NFR-001**: File content MUST NOT appear in logs, including error logs (constitution
  Principle VIII).
- **NFR-002**: The role × operation table in FR-024 MUST be covered by automated tests for every
  role, a non-member and an anonymous caller, through direct server calls.
- **NFR-003**: Conflict detection, concurrent structure changes, and the autosave timing in
  FR-020 MUST be covered by automated tests (constitution Principle III).
- **NFR-004**: No new infrastructure services. Files are stored in the existing database
  (constitution Principle IX).

### Key Entities

- **Workspace item**: A folder or a file in one workspace. Has a stable identity, a name, a
  parent folder (or the top level), and created/updated times. Belongs to exactly one
  workspace.
- **Folder**: A workspace item that contains other items.
- **File**: A workspace item with text content, a size, a language derived from its extension,
  and a revision number. Records who last saved it and when.
- **Open tabs (per person, per browser)**: The list of open files and the active one for a
  workspace. Kept in the browser only, so it does not follow the person to another device.
- **Draft (per person, per browser)**: Unsaved text for one file, the revision it was based on,
  and when it was written. Kept in the browser only; cleared on save, discard or sign-out.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A member can create a three-level folder structure with a file at the bottom and
  start typing in it in under 1 minute.
- **SC-002**: After a change shows "Saved", 100% of browser reloads show that change.
- **SC-003**: During 60 seconds of continuous typing in one file, the web app sends at most 7
  saves; after typing stops, the change is saved within 2 seconds under normal conditions.
- **SC-004**: In every tested race between two saves based on the same revision, exactly one
  succeeds and the other is reported as a conflict; no accepted change is ever overwritten
  without the person choosing to overwrite.
- **SC-005**: 100% of file-changing requests from Viewers, non-members and anonymous callers, and
  100% of delete requests from Editors, are refused in automated tests, and the stored files are
  unchanged afterwards.
- **SC-006**: 100% of invalid names, unsupported extensions, invalid moves and oversized or
  invalid content are refused with a specific error, in automated tests.
- **SC-007**: Every S01 and S02 automated test still passes.
- **SC-008**: A file at the 1 MiB limit opens and accepts typing without the page becoming
  unresponsive (manual check).
- **SC-009**: After a reload during the autosave delay, 100% of unsaved edits are either saved
  or offered as a draft for restore (in automated tests with browser storage available).

## Out of Scope

- Real-time collaborative editing, merging, presence and live cursors.
- Version history, diffs, undo after closing, and trash or restore for deleted items.
- Git integration, code execution, build, terminals, and language servers (no
  IntelliSense, diagnostics or formatting beyond what the editor offers by itself).
- Binary files, images, file upload and download, and archive import or export.
- Search across files, and find-and-replace across files.
- Cloud or object storage; files live in the existing database.
- Copying or duplicating items.

## Assumptions

- The Owner's "full access" differs from an Editor's only by delete (clarified 2026-10-31).
  Membership management is already the Owner's alone (S02).
- There are no hidden or system folders.
- Drafts hold source code, not credentials, so keeping them in the browser is consistent with
  S02's rule against long-lived credentials in browser storage. Clearing them on sign-out
  protects shared computers.
- The autosave timing (1 s pause, 10 s maximum wait) is fixed, not a user setting.
- Open tabs are remembered per browser, not per account across devices.
- Deletion is permanent. Version history and restore are later features.
- Editor features such as find, undo/redo within the session, and bracket matching are what the
  chosen editor provides by default. No configuration UI is included.
- Existing workspaces start with an empty tree after the upgrade; no sample files are created.
- The limits (1 MiB per file, 5,000 items, depth 20, 20 tabs) are starting values and can be
  revised in a later feature.
