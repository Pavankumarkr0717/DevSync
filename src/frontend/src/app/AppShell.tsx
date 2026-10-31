import { Link, NavLink, Outlet } from 'react-router';
import { useLogoutMutation } from '../features/auth/authApi';
import { selectAuth } from '../features/auth/authSlice';
import { useSessionKeepAlive } from '../features/auth/useSessionKeepAlive';
import { useListMyInvitationsQuery } from '../features/invitations/invitationsApi';
import { Toast } from '../shared/ui/Toast';
import { useAppSelector } from './hooks';

export function AppShell() {
  const { status, user } = useAppSelector(selectAuth);
  const signedIn = status === 'signedIn';
  const { data: invitations } = useListMyInvitationsQuery(undefined, { skip: !signedIn });
  const [logout] = useLogoutMutation();
  useSessionKeepAlive();

  return (
    <>
      <header className="app-header">
        <Link to={signedIn ? '/workspaces' : '/login'} className="brand">
          DevSync
        </Link>
        {signedIn && user && (
          <nav aria-label="Main" className="nav">
            <NavLink to="/workspaces">Workspaces</NavLink>
            <NavLink to="/invitations">
              Invitations{invitations && invitations.length > 0 ? ` (${invitations.length})` : ''}
            </NavLink>
            <NavLink to="/profile">{user.displayName}</NavLink>
            <button type="button" onClick={() => void logout()}>
              Sign out
            </button>
          </nav>
        )}
      </header>
      <Toast />
      <main className="content">
        <Outlet />
      </main>
    </>
  );
}
