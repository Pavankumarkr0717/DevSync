import { useState, useSyncExternalStore } from 'react';
import { Link, NavLink, Outlet, useLocation, useMatch } from 'react-router';
import { useLogoutMutation } from '../features/auth/authApi';
import { selectAuth } from '../features/auth/authSlice';
import { useSessionKeepAlive } from '../features/auth/useSessionKeepAlive';
import { useListMyInvitationsQuery } from '../features/invitations/invitationsApi';
import { Avatar } from '../shared/ui/Avatar';
import { HomeIcon, LogoMark, LogoutIcon, MailIcon, MenuIcon } from '../shared/ui/icons';
import { ThemeToggle } from '../shared/ui/ThemeToggle';
import { Toast } from '../shared/ui/Toast';
import { useAppSelector } from './hooks';

const NARROW = '(max-width: 640px)';
const COLLAPSED_KEY = 'devsync:nav-collapsed';

function subscribeNarrow(onChange: () => void) {
  const list = window.matchMedia?.(NARROW);
  list?.addEventListener('change', onChange);
  return () => list?.removeEventListener('change', onChange);
}

const isNarrow = () => window.matchMedia?.(NARROW).matches ?? false;

function readCollapsed(): boolean {
  try {
    const stored = localStorage.getItem(COLLAPSED_KEY);
    if (stored !== null) {
      return stored === 'true';
    }
  } catch {
    // Fall through to the default.
  }
  return window.innerWidth < 1100;
}

/**
 * The signed-in app frame from docs/design/design-system.md, laid out like Google's apps: a top app
 * bar (menu button, logo, theme switch, profile) over a navigation drawer and the page sheet. The
 * menu button collapses the drawer to an icon rail on larger screens and opens it as an overlay on
 * phones. The diagram canvas uses the whole window ("focus").
 */
export function AppShell() {
  const { status, user } = useAppSelector(selectAuth);
  const signedIn = status === 'signedIn';
  const { data: invitations } = useListMyInvitationsQuery(undefined, { skip: !signedIn });
  const [logout] = useLogoutMutation();
  const narrow = useSyncExternalStore(subscribeNarrow, isNarrow, () => false);
  const [collapsed, setCollapsed] = useState(readCollapsed);
  const [menuOpen, setMenuOpen] = useState(false);
  const location = useLocation();
  const focus = useMatch('/workspaces/:workspaceId/diagrams/:diagramId') !== null;
  const [lastPath, setLastPath] = useState(location.pathname);
  useSessionKeepAlive();

  // Close the phone menu after navigating.
  if (lastPath !== location.pathname) {
    setLastPath(location.pathname);
    setMenuOpen(false);
  }

  const toggleNav = () => {
    if (narrow) {
      setMenuOpen((open) => !open);
      return;
    }
    const next = !collapsed;
    setCollapsed(next);
    try {
      localStorage.setItem(COLLAPSED_KEY, String(next));
    } catch {
      // A preference only.
    }
  };

  const pending = invitations?.length ?? 0;
  const expanded = narrow ? menuOpen : !collapsed;
  const mode =
    !signedIn || !user
      ? 'shell--public'
      : focus
        ? 'shell--focus'
        : narrow
          ? menuOpen
            ? 'shell--menu-open'
            : ''
          : collapsed
            ? 'shell--collapsed'
            : '';

  return (
    <div className={`shell ${mode}`}>
      {signedIn && user && !focus && (
        <>
          <header className="topbar">
            <button
              type="button"
              className="icon-button"
              aria-label="Main menu"
              aria-expanded={expanded}
              aria-controls="main-navigation"
              onClick={toggleNav}
            >
              <MenuIcon />
            </button>
            <Link to="/workspaces" className="brand">
              <LogoMark size={32} />
              <span className="brand-name">DevSync</span>
            </Link>
            <span className="spacer" />
            <ThemeToggle />
            <Link to="/profile" className="topbar-avatar" aria-label="Your profile" title={user.displayName}>
              <Avatar id={user.id} name={user.displayName} size="sm" />
            </Link>
          </header>
          {narrow && menuOpen && <div className="scrim" aria-hidden="true" onClick={() => setMenuOpen(false)} />}
          <aside id="main-navigation" className="sidebar">
            <nav aria-label="Main" className="sidebar-nav">
              <NavLink to="/workspaces" title="Workspaces">
                <HomeIcon />
                <span className="nav-label">Workspaces</span>
              </NavLink>
              <NavLink to="/invitations" title="Invitations">
                <MailIcon />
                <span className="nav-label">
                  Invitations{pending > 0 && ' '}
                  {pending > 0 && <span className="visually-hidden">({pending})</span>}
                </span>
                {pending > 0 && (
                  <span className="nav-badge" aria-hidden="true">
                    {pending}
                  </span>
                )}
              </NavLink>
              <div className="sidebar-footer">
                <NavLink to="/profile" title="Profile">
                  <Avatar id={user.id} name={user.displayName} size="sm" />
                  <span className="person">
                    {user.displayName}
                    <span className="person-caption" aria-hidden="true">
                      Profile
                    </span>
                  </span>
                </NavLink>
                <button type="button" title="Sign out" onClick={() => void logout()}>
                  <LogoutIcon />
                  <span className="nav-label">Sign out</span>
                </button>
              </div>
            </nav>
          </aside>
        </>
      )}
      <div className="shell-main">
        {(!signedIn || !user) && (
          <header className="public-header">
            <Link to="/login" className="brand">
              <LogoMark />
              <span className="brand-name">DevSync</span>
            </Link>
            <span className="public-theme">
              <ThemeToggle />
            </span>
          </header>
        )}
        <Toast />
        <main className="content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
