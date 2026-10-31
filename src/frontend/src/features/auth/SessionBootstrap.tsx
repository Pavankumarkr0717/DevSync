import { useEffect, type ReactNode } from 'react';
import { useAppDispatch, useAppSelector } from '../../app/hooks';
import { refreshSession } from '../../shared/api/session';
import { Loading } from '../../shared/ui/Loading';
import { selectAuthStatus } from './authSlice';

/** On app start, restores the session from the refresh cookie; shows only "Loading…" until known. */
export function SessionBootstrap({ children }: { children: ReactNode }) {
  const dispatch = useAppDispatch();
  const status = useAppSelector(selectAuthStatus);

  useEffect(() => {
    if (status === 'checking') {
      void refreshSession(dispatch, { quiet: true });
    }
  }, [status, dispatch]);

  return status === 'checking' ? <Loading /> : <>{children}</>;
}
