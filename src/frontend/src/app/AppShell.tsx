import { useState } from 'react';
import { Link, NavLink, Outlet, useLocation, useMatch } from 'react-router';
import { useLogoutMutation } from '../features/auth/authApi';
import { selectAuth } from '../features/auth/authSlice';
import { useSessionKeepAlive } from '../features/auth/useSessionKeepAlive';
import { useListMyInvitationsQuery } from '../features/invitations/invitationsApi';
import { Avatar } from '../shared/ui/Avatar';
import { HomeIcon, LogoMark, LogoutIcon, MailIcon, MenuIcon, XIcon } from '../shared/ui/icons';
import { Toast } from '../shared/ui/Toast';
import { useAppSelector } from './hooks';

/**
 * The signed-in app frame from docs/design/design-system.md: a sidebar (an icon rail on medium
 * screens, a menu on small ones) and the page. The diagram canvas uses the whole window ("focus").
 */
export function AppShell() {
  const { status, user } = useAppSelector(selectAuth);
  const signedIn = status === 'signedIn';
  const { data: invitations } = useListMyInvitationsQuery(undefined, { skip: !signedIn });
  const [logout] = useLogoutMutation();
  const [menuOpen, setMenuOpen] = useState(false);
  const location = useLocation();
  const focus = useMatch('/workspaces/:workspaceId/diagrams/:diagramId') !== null;
  const [lastPath, setLastPath] = useState(location.pathname);
  useSessionKeepAlive();

  // Close the small-screen menu after navigating.
  if (lastPath !== location.pathname) {
    setLastPath(location.pathname);
    setMenuOpen(false);
  }

  const pending = invitations?.length ?? 0;
  const mode = !signedIn || !user ? 'shell--public' : focus ? 'shell--focus' : menuOpen ? 'shell--menu-open' : '';

  return (
    <div className={`shell ${mode}`}>
      {signedIn && user && !focus && (
        <>
          <header className="topbar">
            <button
              type="button"
              className="icon-button"
              aria-label={menuOpen ? 'Close menu' : 'Open menu'}
              aria-expanded={menuOpen}
              onClick={() => setMenuOpen((open) => !open)}
            >
              {menuOpen ? <XIcon /> : <MenuIcon />}
            </button>
            <Link to="/workspaces" className="brand">
              <LogoMark size={28} />
              <span className="brand-name">DevSync</span>
            </Link>
          </header>
          <aside className="sidebar">
            <Link to="/workspaces" className="brand">
              <LogoMark />
              <span className="brand-name">DevSync</span>
            </Link>
            <nav aria-label="Main" className="sidebar-nav">
              <NavLink to="/workspaces">
                <HomeIcon />
                <span className="nav-label">Workspaces</span>
              </NavLink>
              <NavLink to="/invitations">
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
                <NavLink to="/profile">
                  <Avatar id={user.id} name={user.displayName} size="sm" />
                  <span className="person">
                    {user.displayName}
                    <span className="person-caption" aria-hidden="true">
                      Profile
                    </span>
                  </span>
                </NavLink>
                <button type="button" onClick={() => void logout()}>
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
