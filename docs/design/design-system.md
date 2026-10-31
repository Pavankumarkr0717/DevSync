# DevSync Design System

**Status**: Adopted 2026-10-31 with spec 004 (UI amendment) | **Reference**: [mockups](reference/)

The visual language comes from the reference mockups: a calm, Google-inspired workspace UI with
white surfaces, soft greys, one indigo accent, rounded cards and pastel avatars. **Only the look
is adopted.** Features shown in the mockups that no approved spec covers are **not** built and
have no placeholders. That includes activity feed, presence and live cursors, favorites, trash,
search, notifications, sticky notes, drawing tools, comments, share, present, per-board
permissions and templates. The product name stays **DevSync**.

## Tokens

All colours are CSS custom properties on `:root`, with a dark variant under
`prefers-color-scheme: dark`.

| Token | Light | Dark | Use |
|-------|-------|------|-----|
| `--bg` | `#F7F8FA` | `#111318` | App background |
| `--surface` | `#FFFFFF` | `#1A1D24` | Sidebar, top bar, cards, panels |
| `--surface-muted` | `#F3F4F6` | `#22262F` | Card previews, inputs at rest, hover |
| `--border` | `#E5E7EB` | `#2E333D` | Card and panel borders, dividers |
| `--text` | `#111827` | `#E8EAF0` | Primary text |
| `--muted` | `#6B7280` | `#9AA1AE` | Secondary text, captions |
| `--accent` | `#4F46E5` | `#8B8CFF` | Primary buttons, links, focus, selection |
| `--accent-hover` | `#4338CA` | `#A5A6FF` | Primary button hover |
| `--accent-soft` | `#EEF0FF` | `#2A2C52` | Active nav item, selected node fill, chips |
| `--success` | `#16A34A` | `#4ADE80` | "Saved", success notes |
| `--danger` | `#DC2626` | `#F87171` | Errors, destructive actions |
| `--warning-soft` | `#FEF3C7` | `#3A3320` | Draft and unsupported banners |
| Pastels | yellow `#FDEFB2`, green `#D1F2E4`, pink `#FBDCE6`, blue `#DBEAFE`, purple `#EDE9FE`, orange `#FFE4CC` | 20% tints on dark | Avatars, previews, component accents |

| Token | Value |
|-------|-------|
| Font | `Inter, "Segoe UI", Roboto, system-ui, sans-serif` (no web-font download; Inter is used when installed) |
| Type scale | 32/40 page title (600) · 20/28 section (600) · 16/24 card title (600) · 14/20 body · 12/16 caption |
| Radius | 8 px controls · 12 px cards and panels · 999 px avatars and pills |
| Shadow | cards `0 1px 2px rgb(16 24 40 / 6%)`; floating panels `0 8px 24px rgb(16 24 40 / 10%)` |
| Spacing | 4 px base: 4, 8, 12, 16, 24, 32, 48 |
| Focus | 2 px `--accent` outline, offset 2 px, on every interactive element |

## Layout

- **App shell**:
  - a 264 px white **sidebar** with the logo, a primary nav (icon and label, the active item
    with `--accent-soft` fill and accent text), and the signed-in person's card at the bottom;
  - a 64 px white **top bar** with breadcrumbs on the left and the avatar menu on the right;
  - content on `--bg` with 32 px padding.
- **Responsive**:
  - below 1100 px the sidebar becomes a 88 px **icon rail**, with labels under the icons;
  - below 640 px it becomes a top bar with a menu button, and content padding drops to 16 px.
- **Cards**: white, 1 px border, 12 px radius, card shadow, and a `--surface-muted` preview band
  on top for boards and diagrams.
- **Canvas page** (no app sidebar):
  - a 64 px header with back, the logo, an editable title (pencil), the save status, and the
    avatar on the right;
  - a 48 px toolbar with breadcrumbs, then zoom −/%/+ and Fit on the right;
  - a dotted canvas background;
  - a floating **component palette** card on the left, styled like "TOOLS";
  - a floating **properties** card on the right, styled like the "Board permissions" card;
  - a minimap card bottom right ("Overview").
- **Diagram nodes**: white 12 px-radius cards with a type icon on top, a bold title and a muted
  subtitle (the description's first line), matching the "happy path" cards. The selected node
  gets a 2 px accent border and `--accent-soft` fill. Edges are grey arrows; selected edges are
  accent.

## Components

| Component | Notes |
|-----------|-------|
| Button | Primary (accent fill, white text, 40 px, radius 8, leading icon); secondary (white, border); ghost (text only); danger |
| Icon button | 32 px square, radius 8, grey icon, hover `--surface-muted` |
| Input / select / textarea | 40 px, radius 8, border, focus ring accent |
| Tabs / segmented filter | Pill tabs; active pill `--accent-soft` with accent text (as "All boards") |
| Avatar | Circle with initials on a pastel derived from the person's id; sizes 28 and 36 |
| Card | See Layout |
| List row | Inside a card; 72 px rows with a thumbnail, title, caption, trailing meta and `⋯` menu |
| Banner | Rounded 12 px, soft fill (accent-soft for info, warning-soft for drafts), icon, text, actions |
| Toast | Bottom-right floating card |
| Empty state | Centered icon, title, one-line explanation, primary action |

Icons are inline SVG, 20 px, 1.75 px stroke, in the style of the mockups. They live in
`src/frontend/src/shared/ui/icons.tsx`; there is no icon dependency.
