import { toggleTheme } from '../theme/theme';
import { useTheme } from '../theme/useTheme';
import { MoonIcon, SunIcon } from './icons';

/** Switches between the light and dark Fluent themes; the choice is remembered in this browser. */
export function ThemeToggle() {
  const mode = useTheme();
  const label = mode === 'dark' ? 'Switch to light theme' : 'Switch to dark theme';

  return (
    <button type="button" className="icon-button" aria-label={label} title={label} onClick={toggleTheme}>
      {mode === 'dark' ? <SunIcon /> : <MoonIcon />}
    </button>
  );
}
