# DevSync Design System

**Status**: Adopted 2026-10-31 with spec 004 (UI amendment); revised the same day to a dark-first,
Google-style layout on Fluent 2 tokens | **Reference**: [mockups](reference/)

**Layout and shapes** follow Google's apps (Gmail, Drive): a top app bar with a menu button, a
navigation drawer that collapses to a rail, the page on a rounded sheet, pill buttons and
navigation, underline tabs and snackbars. **Colours, shadows and small radii** come from
Microsoft's **Fluent 2 design tokens** (`@fluentui/react-theme`), generated for DevSync: an
electric-blue brand on **deep navy and charcoal** (dark, the default) or cool light greys, which
people switch with a toggle. **Inter** sets the UI and **JetBrains Mono** sets code and technical
labels. Only the look is adopted: there is no Google or Microsoft branding,
and the product name and logo stay **DevSync**. Features shown in the reference mockups that no
approved spec covers are **not** built and have no placeholders (activity feed, presence and live
cursors, favorites, trash, search, notifications, sticky notes, drawing tools, comments, share,
present, per-board permissions and templates).

## Themes

- `src/frontend/src/shared/theme/theme.ts` builds both themes with Fluent's generator
  (`createDarkTheme` / `createLightTheme`) from a 16-step **electric-blue brand ramp** (primary
  `#2563EB`), then overrides the neutrals: navy/charcoal for dark (app background `#0B0F17`,
  surfaces `#121826`, borders `#232C3D`, text `#E6EDF7` / `#8B98AD`) and cool greys for light (app
  background `#F4F6FA`, text `#111827` / `#5B6577`). It writes **every** token of the active theme
  onto `:root` as a CSS variable named after it (`--colorBrandBackground`, `--colorNeutralForeground1`, `--shadow16`…),
  the same way Fluent's own provider does. It also sets `data-theme="light|dark"` and
  `color-scheme` on `<html>`.
- DevSync is **dark-first**: dark is the default. The **theme toggle** (sun/moon in the top bar,
  on the public pages and in the canvas header) switches light ↔ dark, and the choice is stored in
  this browser as `devsync:theme`.
- Monaco uses DevSync's own `devsync-dark` / `devsync-light` editor themes (the stock syntax colours
  on the app's surfaces), and React Flow (`colorMode`) follows `useTheme()`.

## Tokens

`index.css` names DevSync's semantic tokens once and maps each to a Fluent token, so components
never use raw colours.

| DevSync token | Fluent token | Use |
|---------------|--------------|-----|
| `--bg` | `colorNeutralBackground3` | App background, top bar, drawer, canvas header |
| `--surface` | `colorNeutralBackground1` | The page sheet, cards, panels, dialogs |
| `--surface-muted` | `colorNeutralBackground1Hover` | Card previews, empty states, menus, read-only fields |
| `--surface-container` | `colorNeutralBackground1Selected` | Canvas toolbar |
| `--border` / `--outline` | `colorNeutralStroke2` / `colorNeutralStroke1` | Dividers and card borders / field and outlined-button borders |
| `--text` / `--muted` | `colorNeutralForeground1` / `colorNeutralForeground3` | Primary / secondary text |
| `--accent` | `colorBrandForeground1` | Links, brand text, active tab, focus ring, selection |
| `--accent-fill` / `--accent-hover` | `colorBrandBackground` / `colorBrandBackgroundHover` | Filled buttons |
| `--accent-contrast` | `colorNeutralForegroundOnBrand` | Text on filled buttons |
| `--accent-soft` / `--accent-on-soft` | `colorBrandBackground2` / `colorBrandForeground2` | Chips, info banners, palette hover |
| `--nav-active` / `--nav-active-text` | `colorBrandBackground2` / `colorBrandForeground2` | Active navigation item and tree row |
| `--state-hover` | `colorSubtleBackgroundHover` | Hover on rows and icon buttons |
| `--success` | `colorStatusSuccessForeground1` | "Saved" |
| `--danger` / `--danger-soft` | `colorStatusDangerForeground1` / `colorStatusDangerBackground1` | Errors, destructive actions, conflict banners |
| `--warning-soft` | `colorStatusWarningBackground1` | Draft and unsupported banners |
| `--inverse-surface` / `--inverse-text` | `colorNeutralBackgroundInverted` / `colorNeutralForegroundInverted` | Snackbars |
| `--scrim` | `colorBackgroundOverlay` | Behind the phone drawer |
| Pastels (blue, purple, green, yellow, pink, orange) | `colorPalette{Blue,Grape,LightGreen,Marigold,Berry,Pumpkin}Background2` with the matching `Foreground2` | Avatars, previews, component icons |
| `--shadow-card` / `--shadow-float` / `--shadow-dialog` | `shadow4` / `shadow16` / `shadow28` | Card hover / floating panels and menus / dialogs and the phone drawer |
| `--radius-xs` / `--radius-sm` | `borderRadiusMedium` (4 px) / `borderRadiusXLarge` (8 px) | Fields, menus, snackbars / chips, banners |

| Token | Value |
|-------|-------|
| Fonts | **Inter** 400–700 for the UI and **JetBrains Mono** 400/500 for code and technical labels (file names, tabs, the editor and its status bar, panel titles, counters, zoom, edge labels). Both are Latin subsets **bundled** from `@fontsource` (never fetched from a font CDN) |
| Type scale | 48/56 landing title · 28/36 page title (600, −0.02em) · 18/26 section (600) · 16/24 card title · 14/20 body · 12/16 caption · 11 px mono uppercase panel titles |
| Large radii (Google shapes) | 16 px cards, panels and the page sheet · 28 px dialogs and the sign-in card · pills (999 px) for buttons, navigation items and tree rows |
| Spacing | 4 px base: 4, 8, 12, 16, 24, 32, 40, 48 |
| Focus | 2 px `--accent` outline, offset 2 px, on every interactive element |

## Layout

- **App shell** (Google-style):
  - a 64 px **top app bar** on `--bg`: the menu button (`Main menu`, with `aria-expanded` and
    `aria-controls`), the logo, then the theme toggle and the person's avatar (to Profile) on the
    right;
  - a 256 px **navigation drawer** below it: Workspaces and Invitations as pill items (the active
    one filled with `--nav-active` and bold), and the person and Sign out at the bottom;
  - the page on a white **sheet** (16 px radius) that fills the rest of the window.
- **Menu button**:
  - wider than 640 px it collapses the drawer to an 80 px **rail** (icons with short labels
    underneath) and back; the choice is remembered as `devsync:nav-collapsed`, and the rail is
    the default below 1,100 px;
  - on phones (640 px and narrower) the drawer is hidden and the button opens it over the page
    with a scrim; it closes on navigation or when the scrim is tapped.
- **Landing page** (`/`, signed-out visitors; signed-in people go to their workspaces): the
  tagline "Build together. Design together. Engineer together.", Get started and Sign in, an
  illustrated preview of the editor and the canvas (decorative, not interactive), and three cards
  for the shipped capabilities.
- **Workspace pages**: breadcrumbs, then **underline tabs** (Overview · Files · Diagrams; the
  active tab in accent with a 3 px indicator).
- **Cards**: white, 1 px border, 16 px radius, level-1 shadow on hover, and an inset
  `--surface-muted` preview band on top for diagrams.
- **Canvas page** (no app bar or drawer):
  - a 64 px header on `--bg` with back, the logo, the title (pencil to rename), the save status
    and the theme toggle;
  - a pill-shaped **toolbar** on `--surface-container` with breadcrumbs, zoom −/%/+, Fit and
    Snap to grid;
  - a dotted canvas background;
  - floating **component palette** and **properties** cards (level 2), and a minimap card.
- **Diagram nodes**: 16 px-radius cards with a type icon on top, a medium-weight title and a
  muted subtitle (the description's first line). The selected node gets a 2 px accent border
  and a level-2 shadow. Edges are grey arrows with pill labels; selected edges are accent.
- **Code editor**: Monaco follows the active theme (`vs` or `vs-dark`).
- **Fit view** on the canvas leaves room for the floating palette and properties panels on wide
  screens.

## Components

| Component | Notes |
|-----------|-------|
| Button | 40 px pills. Filled (accent, for the main action), outlined (1 px `--outline`, accent label; the default), text (`.btn-ghost`), danger (red label) |
| Icon button | 40 px circle, muted icon, `--state-hover` on hover |
| Input / select / textarea | Outlined, 4 px radius, `--outline` border; focus turns the border accent |
| Tabs | Underline tabs, 48 px high; active in accent with a 3 px indicator |
| Navigation item / tree row | Pill rows; active filled with `--nav-active` |
| Chip | 8 px radius, `--accent-soft` fill |
| Avatar | Circle with initials on a pastel derived from the person's id; sizes 28, 36 and 44 |
| Card | See Layout |
| Banner | 8 px radius, soft fill (accent-soft for info, warning-soft for drafts, danger-soft for conflicts), text, actions |
| Menu | 4 px radius on `--surface-muted`, level 2, 40 px rows |
| Dialog | 28 px radius, level 2, no border |
| Snackbar (toast) | Bottom left, `--inverse-surface`, 4 px radius |
| Role badge | Pill; Owner on `--accent-soft`, Editor on the green pastel, Viewer on `--surface-container`. It communicates permissions only; the server decides |
| File-type badge | A small mono badge per supported extension (C#, TS, TSX, JS, JSX, `{ }`, `<>`, #, PY, J, MD) on its pastel; folders show a folder icon |
| Editor status bar | Under the editor, mono 12 px: the language, UTF-8, and "Read only" (amber) when editing isn't allowed. The minimap is on |
| Password strength | Four bars under the password (red, amber, green) and "Password strength: …"; a guide only, the server requires 12–128 characters |
| Confirm dialog | `alertdialog` with a title, the consequence, Cancel (focused) and a red action; Escape cancels |
| Loading skeleton | Shimmering placeholder cards for card lists (no animation with reduced motion); screen readers hear "Loading…" |
| Empty state | `--surface-muted` panel with a centered icon, title, one-line explanation and primary action |

Icons are inline SVG, 20 px, 1.75 px stroke, in the style of the mockups. They live in
`src/frontend/src/shared/ui/icons.tsx`; there is no icon dependency.
