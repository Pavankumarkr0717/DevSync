# UI Contract: System Design Canvas

**Feature**: [spec.md](../spec.md) | Extends the [003 ui-routes](../../003-file-explorer-editor/contracts/ui-routes.md)

## Routes

| Route | Access | Change |
|-------|--------|--------|
| `/workspaces/:workspaceId` | Member | **Changed**: an **Open diagrams** link next to **Open files**. |
| `/workspaces/:workspaceId/diagrams` | Member | **New**: the diagram list and an empty state. |
| `/workspaces/:workspaceId/diagrams/:diagramId` | Member | **New**: the canvas page. A deep link reopens the diagram after a refresh. |

A missing workspace, or a non-member, gets "Workspace not found" (002). A missing diagram gets
"Diagram not found" with a link back to the list.

## Diagram list

- **Rows**: each row shows the name, "N components · M connections" and the last-changed time,
  sorted by name ignoring case.
- **Empty state**: "No diagrams yet. Diagrams show how your system's components connect." Owners
  and Editors also see **New diagram**.
- **Actions per row**:

| Action | Owner | Editor | Viewer |
|--------|:-----:|:------:|:------:|
| Open | ✓ | ✓ | ✓ |
| New diagram | ✓ | ✓ | — |
| Rename (inline) | ✓ | ✓ | — |
| Delete (confirmation: "Delete *Checkout* and its 12 components? This can't be undone.") | ✓ | — | — |

- **Name errors**:

| Error | Message |
|-------|---------|
| Empty name | "Name is required." |
| Over 100 characters | "Name must be at most 100 characters." |
| `diagram_name_taken` | "A diagram with this name already exists." |
| `too_many_diagrams` | "This workspace has reached 50 diagrams." |

## Canvas page layout

```text
┌ ← Diagrams   Checkout architecture        Saved ✓        [−] 100% [+] [Fit] [Grid ✓]  ┐
├────────────┬─────────────────────────────────────────────────────┬────────────────────┤
│ PALETTE    │                                                     │ PROPERTIES         │
│ ▭ Client   │     [Client] ──REST──► [API Gateway] ──► [Orders]   │ Type  [Microservice▾]│
│ ▭ API GW   │                                       │             │ Title [Orders     ] │
│ ▭ Micro…   │                                       ▼             │ Desc  [          ]  │
│ ▭ Database │                                   [Orders DB]       │ Metadata            │
│ …          │   (grid · minimap)                                  │  owner = payments ✕ │
│            │                                                     │  [+ Add entry]      │
└────────────┴─────────────────────────────────────────────────────┴────────────────────┘
```

- **Palette** (Owner and Editor only): the 8 types with icon and name. Drag one onto the
  canvas, or press Enter or click to place it at the view centre. Each item is a button with
  the label "Add Database" and so on.
- **Canvas**: React Flow, with an `aria-label` of "Diagram canvas". Nodes are focusable and named
  "Database: Orders DB". Edges are named "Orders → Orders DB: SQL".
- **Properties panel**:

| Selection | Fields |
|-----------|--------|
| Nothing selected | A diagram summary (counts) |
| One node | Type (select), Title, Description (textarea), Metadata (key/value rows with add and remove), Delete component |
| One edge | Source → Target (read-only), Label, Delete connection |
| Several items | A count and "Delete selected" |

  Fields enforce their limits as the person types: a character counter, and an inline message
  for a duplicate metadata key. For Viewers every field is read-only.
- **Empty canvas**: "Drag a component from the palette to start." Viewers see "This diagram is
  empty."
- **Refused connection**: a toast saying "A component can't connect to itself." or "These
  components are already connected in this direction."

## Save status and failures

These mirror files in 003: the same states, reasons, Retry, conflict bar and draft bar.

| State | Shown |
|-------|-------|
| saved | "Saved" |
| dirty | "Unsaved changes" |
| saving | "Saving…" |
| failed: conflict | "Someone else saved a newer version". Autosave pauses. Actions: **Reload latest** · **Overwrite with mine** |
| failed: deleted | "This diagram was deleted". The canvas is kept read-only |
| failed: forbidden | "You no longer have permission to edit". The canvas becomes read-only |
| failed: invalid | "The diagram couldn't be saved: <first problem message>". A **Details** toggle lists up to 50 problems, and the referenced node or edge is selected when clicked |
| failed: tooLarge | "Diagram is larger than 1 MiB" |
| failed: offline | "Couldn't reach the server — retrying". Retry at 2, 4 and 8 s (capped at 30 s), plus a Retry button |
| unsupported version | Banner: "This diagram was saved by a newer version of DevSync (format N) and can't be opened here." Owners and Editors get **Start over** (confirmation, then replaces it with an empty diagram) |

**Drafts**: stored at `devsync:draft:<userId>:diagram-<diagramId>`. They are offered back as in
003 and cleared on sign-out.

## Test ids

- `diagram-list`, `diagram-canvas`, `palette`, `properties-panel`, `save-status`,
  `conflict-bar`, `draft-bar`, `unsupported-banner`
- React Flow nodes keep their `data-id` (the node id). Component tests run React Flow in jsdom
  with ResizeObserver and DOMMatrix stubs.

## Visual design (FR-039, FR-040)

Every page follows [docs/design/design-system.md](../../../docs/design/design-system.md), using
the reference mockups in [docs/design/reference](../../../docs/design/reference/).

**App shell (all signed-in pages except the canvas)**:
- **Sidebar**: the DevSync logo, then the nav items **Workspaces**, **Invitations** and
  **Profile**, then the person's card at the bottom with **Sign out**.
- **Top bar**: breadcrumbs (`Workspaces › Demo › Diagrams`).

**Workspace pages**:
- **Header**: the workspace name, the person's role chip, and segmented tabs **Overview ·
  Files · Diagrams**. These are links to the existing routes. The S03 **Open files** link and
  the S04 **Open diagrams** link remain on the Overview page.
- **Workspace list**: a "Good morning/afternoon/evening, {first name}" heading,
  **Create workspace** (primary), and workspace cards with initials, name, role and created
  date.
- **Diagram list**: cards with a pastel preview band showing the component and connection
  counts, the name, "Edited …" and a `⋯` menu (Rename, Delete per role).

**Canvas page** (mockup 05 and 06):
- **Header**: back to diagrams, logo, the title (the pencil renames for Owner and Editor), and
  the save status with a cloud icon.
- **Toolbar row**: breadcrumbs `Workspace / Diagrams / Name`, then zoom − % + and **Fit**.
- **Palette card** (left, "COMPONENTS") and **Properties card** (right).
- **Minimap** card (bottom right) and a grid toggle.

**Not built** (mockup-only): search, notifications, favorites, trash, activity, presence, live
cursors, share, present, comments, templates, the workspace switcher, and drawing tools.
