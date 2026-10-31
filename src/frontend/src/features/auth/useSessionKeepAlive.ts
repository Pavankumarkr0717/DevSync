import { useEffect } from 'react';
import { useAppDispatch, useAppSelector } from '../../app/hooks';
import { refreshSession } from '../../shared/api/session';
import { selectAuth } from './authSlice';

const RENEW_BEFORE_MS = 60_000;

/**
 * Renews the access token about a minute before it expires while the tab is visible, so a person
 * working continuously is never interrupted (spec 002 SC-006). Hidden tabs renew on demand instead.
 */
export function useSessionKeepAlive() {
  const dispatch = useAppDispatch();
  const { status, accessTokenExpiresAt } = useAppSelector(selectAuth);

  useEffect(() => {
    if (status !== 'signedIn' || !accessTokenExpiresAt) {
      return;
    }

    const dueAt = Date.parse(accessTokenExpiresAt) - RENEW_BEFORE_MS;
    const renewIfVisible = () => {
      if (document.visibilityState === 'visible' && Date.now() >= dueAt) {
        void refreshSession(dispatch);
      }
    };

    const timer = window.setTimeout(renewIfVisible, Math.max(0, dueAt - Date.now()));
    document.addEventListener('visibilitychange', renewIfVisible);
    return () => {
      window.clearTimeout(timer);
      document.removeEventListener('visibilitychange', renewIfVisible);
    };
  }, [status, accessTokenExpiresAt, dispatch]);
}
