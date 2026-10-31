import { Navigate, Outlet, useLocation } from 'react-router';
import { useAppSelector } from '../../app/hooks';
import { Loading } from '../../shared/ui/Loading';
import { selectAuthStatus } from './authSlice';

/**
 * Shows child routes only to signed-in users; others go to /login with a return path.
 * This is a user-experience guard only — the server authorizes every request (spec 002 FR-026).
 */
export function RequireAuth() {
  const status = useAppSelector(selectAuthStatus);
  const location = useLocation();

  if (status === 'checking') {
    return <Loading />;
  }

  if (status === 'signedOut') {
    const returnTo = encodeURIComponent(location.pathname + location.search);
    return <Navigate to={`/login?returnTo=${returnTo}`} replace />;
  }

  return <Outlet />;
}
