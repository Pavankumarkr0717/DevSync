import { useSyncExternalStore } from 'react';
import { getTheme, subscribeTheme, type ThemeMode } from './theme';

/** The active theme, re-rendering when it changes (used by Monaco and React Flow, which theme themselves). */
export function useTheme(): ThemeMode {
  return useSyncExternalStore(subscribeTheme, getTheme, getTheme);
}
