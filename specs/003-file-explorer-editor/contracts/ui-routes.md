# UI Contract: Project Files and Code Editor

**Feature**: [spec.md](../spec.md) | Extends [002 ui-routes](../../002-identity-and-permissions/contracts/ui-routes.md)

## Routes

| Route | Access | Change |
|-------|--------|--------|
| `/workspaces/:workspaceId` | Member | **Changed**: the existing details page gains an **Open files** link (all roles). Nothing else changes. |
| `/workspaces/:workspaceId/files` | Member (`RequireAuth`) | **New**: the files workspace: explorer on the left, tabs and editor on the right (FR-030). Full-width layout. |

Non-members and missing workspaces show the existing "Workspace not found" page from 002. A
signed-out visit redirects to `/login?returnTo=…`, as in 002.

## Files page layout

```text
┌ ← Overview   <Workspace name>                              role: Editor ┐
├──────────────────────┬───────────────────────────────────────────────────┤
│ EXPLORER  [+📄][+📁]  │ [server.ts ●] [app.ts] [index.ts — src/web] [×]   │
│ ▾ src                │ ┌ status: Unsaved changes | Saving… | Saved |     │
│   ▾ api              │ │         Save failed: <reason> [Retry]           │
│     server.ts        │ ├─────────────────────────────────────────────────┤
│   ▸ web              │ │  (code editor)                                  │
│ README.md            │ │                                                 │
└──────────────────────┴─┴─────────────────────────────────────────────────┘
```

## Explorer

- **Accessible tree**: `role="tree"` / `treeitem` with `aria-expanded`. Arrow keys move,
  Enter opens a file or toggles a folder, and F2 renames (Owner/Editor).
- **Sort order**: folders first, then files; each group sorted by name ignoring case.
- **Empty workspace**: "No files yet". Owners and Editors see **New file** and **New folder**;
  Viewers see "No files yet" only.
- **Item actions** (context menu and `…` button):

| Action | Owner | Editor | Viewer |
|--------|:-----:|:------:|:------:|
| Open | ✓ | ✓ | ✓ |
| New file / New folder (in a folder or at the top level) | ✓ | ✓ | — |
| Rename (inline input) | ✓ | ✓ | — |
| Move to… (dialog with a folder picker; the item and its descendants are disabled) | ✓ | ✓ | — |
| Delete (confirmation: "Delete *src* and 12 items inside? This can't be undone.") | ✓ | — | — |

- **Inline create and rename**: the input validates as you type, using the same rules and limits
  the tree endpoint returns. Server errors show next to the input:

| Code | Message |
|------|---------|
| `invalid_name` | the server's message for the broken rule |
| `name_taken` | "An item with this name already exists here" |
| `unsupported_extension` | "Supported: .cs .js .ts .tsx .jsx .json .html .css .py .java .md" |
| `too_deep` | "Folders can be nested at most 20 levels" |
| `path_too_long` | "The full path would be longer than 1,024 characters" |
| `too_many_items` | "This workspace has reached 5,000 files and folders" |

- **Tree refreshes** on window focus and reconnect, after every structure change by this person,
  and after any file request returns `item_not_found`.

## Tabs and editor

- **Opening files**: opening a file focuses its existing tab or adds a new one. The limit is 20
  tabs; past it, a toast says "Close a tab to open another file". Tabs whose names clash also
  show the parent path (`index.ts — src/web`).
- **Tab labels**: a `●` marks unsaved changes, and a `!` marks Save Failed. Closing a tab flushes
  any pending save first; if it can't save, the tab stays open with Save Failed.
- **Editor**: Monaco, with the language chosen from the extension. Viewers get
  `readOnly: true` and a "Read only" label. Each tab keeps its own undo history, cursor and
  scroll position.
- **Save status** for the active tab: `Saved`, `Unsaved changes`, `Saving…`, or
  `Save failed: <reason>` plus Retry. The reasons are:

| Reason | Shown | Autosave | Extra actions |
|--------|-------|----------|---------------|
| conflict (412) | "Someone else saved a newer version" | paused | **Reload latest** (discard mine) · **Overwrite with mine** · **Copy mine** |
| deleted (404) | "This file was deleted" | stopped | **Copy mine** · Close tab |
| no permission (403) | "You no longer have permission to edit" | stopped; editor becomes read-only | **Copy mine** |
| too large (413) | "File is larger than 1 MiB" | stopped until the text shrinks | — |
| invalid content (400) | "File contains characters that can't be saved" | stopped until the next edit | — |
| offline (network, 5xx) | "Couldn't reach the server — retrying" | retries at 2 s, 4 s and 8 s (capped at 30 s), then on the next edit | Retry |

- **Renames by others**: a file renamed by someone else keeps its tab, and the label updates on
  the next tree refresh. A file deleted by someone else shows "deleted" on its next save or
  refresh.

## Persistence across refresh

- **Open tabs**: stored per user and per workspace in `localStorage`
  (`devsync:tabs:<userId>:<workspaceId>` → `{ itemIds, activeId }`). After a refresh, tabs
  reopen in order. Ids that are no longer in the tree are dropped silently (FR-033).
- **Drafts** (FR-035–FR-037): stored at `devsync:draft:<userId>:<itemId>` →
  `{ text, baseRevision, savedAt }`.
  - When a file opens and its draft differs from the server text, a bar shows: "You have unsaved
    changes from <time>. **Restore** · **Discard**".
  - Explicit sign-out clears all of that user's drafts and tab lists. Session expiry doesn't.

## Test ids

These are for component tests. Prefer role and name queries; ids are used only where no
accessible name fits.

- `file-tree`, `editor-tabs`, `save-status`, `conflict-bar`, `draft-bar`
- `code-editor`: the editor host. It is replaced by a textarea stand-in in jsdom tests.
