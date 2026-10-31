import { createDarkTheme, createLightTheme, type BrandVariants, type Theme } from '@fluentui/react-theme';

/**
 * DevSync's light and dark themes, built with Fluent 2's theme generator (docs/design/design-system.md):
 * an electric-blue brand ramp, and navy/charcoal neutrals for the dark theme. Every token of the active
 * theme becomes a CSS variable named after it (`--colorBrandBackground`, `--shadow16`…), exactly as
 * Fluent's own provider does, and index.css maps DevSync's semantic tokens onto them.
 *
 * DevSync is dark-first: dark is the default until someone picks light, and the choice is remembered
 * per browser.
 */

/** Electric blue, darkest (10) to lightest (160); 80 is the primary brand colour. */
const BRAND: BrandVariants = {
  10: '#020617',
  20: '#061433',
  30: '#0a1f4d',
  40: '#0d2a66',
  50: '#113580',
  60: '#154199',
  70: '#1a4fbf',
  80: '#2563eb',
  90: '#3b74f0',
  100: '#4f86f5',
  110: '#6497f8',
  120: '#7aa8fa',
  130: '#91b9fc',
  140: '#a9cafd',
  150: '#c3dbfe',
  160: '#dfeaff',
};

/** Cool, slightly blue-grey neutrals for the light theme. */
const LIGHT_NEUTRALS: Partial<Theme> = {
  colorNeutralBackground3: '#f4f6fa',
  colorNeutralStroke1: '#cbd2dd',
  colorNeutralStroke2: '#e3e8f0',
  colorNeutralForeground1: '#111827',
  colorNeutralForeground2: '#374151',
  colorNeutralForeground3: '#5b6577',
};

/** Deep navy and charcoal for the dark theme. */
const DARK_NEUTRALS: Partial<Theme> = {
  colorNeutralBackground1: '#121826',
  colorNeutralBackground1Hover: '#1a2232',
  colorNeutralBackground1Pressed: '#161d2b',
  colorNeutralBackground1Selected: '#202a3c',
  colorNeutralBackground2: '#0f1520',
  colorNeutralBackground3: '#0b0f17',
  colorNeutralBackground4: '#080b12',
  colorSubtleBackgroundHover: '#1a2232',
  colorSubtleBackgroundPressed: '#161d2b',
  colorSubtleBackgroundSelected: '#202a3c',
  colorNeutralStroke1: '#334155',
  colorNeutralStroke2: '#232c3d',
  colorNeutralStroke3: '#1c2433',
  colorNeutralForeground1: '#e6edf7',
  colorNeutralForeground2: '#c3cede',
  colorNeutralForeground3: '#8b98ad',
  colorNeutralBackgroundInverted: '#e6edf7',
  colorNeutralForegroundInverted: '#0b0f17',
};

export type ThemeMode = 'light' | 'dark';

const STORAGE_KEY = 'devsync:theme';
const STYLE_ID = 'devsync-fluent-theme';
const THEMES: Record<ThemeMode, Theme> = {
  light: { ...createLightTheme(BRAND), ...LIGHT_NEUTRALS },
  dark: { ...createDarkTheme(BRAND), ...DARK_NEUTRALS },
};

const listeners = new Set<() => void>();
let current: ThemeMode = initialMode();

function storedMode(): ThemeMode | null {
  try {
    const value = localStorage.getItem(STORAGE_KEY);
    return value === 'light' || value === 'dark' ? value : null;
  } catch {
    return null;
  }
}

function initialMode(): ThemeMode {
  return storedMode() ?? 'dark';
}

/** The theme's tokens as CSS custom property declarations. */
export function themeCss(mode: ThemeMode): string {
  const theme = THEMES[mode];
  return Object.entries(theme)
    .map(([name, value]) => `--${name}: ${String(value)};`)
    .join('\n');
}

/** Writes the tokens for `mode` onto :root and marks the document with `data-theme`. */
function apply(mode: ThemeMode) {
  if (typeof document === 'undefined') {
    return;
  }
  let style = document.getElementById(STYLE_ID);
  if (!style) {
    style = document.createElement('style');
    style.id = STYLE_ID;
    document.head.prepend(style);
  }
  style.textContent = `:root {\n${themeCss(mode)}\n}`;
  document.documentElement.dataset.theme = mode;
  document.documentElement.style.colorScheme = mode;
}

function set(mode: ThemeMode) {
  current = mode;
  apply(mode);
  listeners.forEach((listener) => listener());
}

/** Applies the starting theme. Call once at startup. */
export function initTheme() {
  apply(current);
}

export function getTheme(): ThemeMode {
  return current;
}

/** Switches between light and dark and remembers the choice in this browser. */
export function toggleTheme() {
  const next: ThemeMode = current === 'dark' ? 'light' : 'dark';
  try {
    localStorage.setItem(STORAGE_KEY, next);
  } catch {
    // A preference only: the switch still applies for this visit.
  }
  set(next);
}

export function subscribeTheme(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
