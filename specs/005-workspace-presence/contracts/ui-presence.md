# Contract: Presence UI

Follows [docs/design/design-system.md](../../../docs/design/design-system.md) in both themes.

## Where it appears

| Page | Placement |
|------|-----------|
| Workspace overview, Files (and the code editor), Diagrams list | Right end of the `WorkspaceNav` tabs row |
| Design canvas | In the canvas header, left of the theme toggle |

Routes are unchanged. `workspaces/:workspaceId`, `/files`, `/diagrams` and
`/diagrams/:diagramId` are nested under a layout route (`WorkspaceScope`) that owns the join.

## Presence bar

- A button (`aria-haspopup="dialog"`, `aria-expanded`), accessible name "N online — show who".
  It contains:
  - up to **4** avatars (initials on a pastel circle; the existing `Avatar`), each with a green
    online dot;
  - an overflow chip "+N" for the rest;
  - the text "N online" (hidden on phones; the button keeps its accessible name).
- Order: the current user first, then the others by display name.
- While the status is not `connected`, the bar is dimmed and its name ends with ", may be out of
  date".
- Clicking or pressing Enter opens a **popover**: heading "Online now", a list of names ("Priya
  Shah (you)"). Escape, a second click or a click outside closes it, and focus returns to the
  button.

## Connection status (next to the bar, `role="status"`, polite)

| Client status | Shown |
|---------------|-------|
| `connecting` (first connect) | Nothing (the bar is dimmed) |
| `connected` | Nothing; after a reconnection, "Connection restored." for 4 s |
| `reconnecting` | Spinner + "Reconnecting to DevSync…" |
| `failed` | "Live presence unavailable" + a **Retry** button |

## Notifications

The existing snackbar (`Toast`):
- "Alex Kim joined the workspace."
- "Alex Kim left the workspace."

They are never shown about the current user, and each event version is shown once. Join and
leave notifications are not shown for changes that predate the person's own join result.

## Access removed

On `AccessRevoked` for the current workspace:
- presence is cleared;
- the toast shows "You no longer have access to this workspace.";
- the workspace queries are invalidated, so the page shows the existing "Workspace not found"
  state.
