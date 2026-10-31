import type { Dispatch } from '@reduxjs/toolkit';
import { sessionEnded, signedOut, tokenRefreshed } from '../../features/auth/authSlice';
import type { AuthSession } from './types';

export const CLIENT_HEADER = 'X-DevSync-Client';
export const CLIENT_HEADER_VALUE = 'web';

export type RefreshOutcome = 'refreshed' | 'ended' | 'unavailable';

interface RefreshOptions {
  /** On app start a missing session is normal: sign out quietly instead of showing "session ended". */
  quiet?: boolean;
}

// One renewal at a time per store (keyed by its dispatch), shared by every caller.
const inFlight = new WeakMap<Dispatch, Promise<RefreshOutcome>>();

/**
 * Renews the session using the HttpOnly refresh cookie. Single-flight per tab, and serialized
 * across tabs with the Web Locks API where available, so normal multi-tab use never presents
 * a used refresh token (spec 002 FR-013). A 409 conflict is retried once with the newer cookie.
 */
export function refreshSession(dispatch: Dispatch, options: RefreshOptions = {}): Promise<RefreshOutcome> {
  let pending = inFlight.get(dispatch);
  if (!pending) {
    pending = withCrossTabLock(() => attemptRefresh(dispatch, options, 0)).finally(() => inFlight.delete(dispatch));
    inFlight.set(dispatch, pending);
  }
  return pending;
}

async function attemptRefresh(dispatch: Dispatch, options: RefreshOptions, attempt: number): Promise<RefreshOutcome> {
  let response: Response;
  try {
    response = await fetch(`${window.location.origin}/api/auth/refresh`, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { [CLIENT_HEADER]: CLIENT_HEADER_VALUE },
    });
  } catch {
    if (options.quiet) {
      dispatch(signedOut());
    }
    return 'unavailable';
  }

  if (response.ok) {
    dispatch(tokenRefreshed((await response.json()) as AuthSession));
    return 'refreshed';
  }

  if (response.status === 409 && attempt === 0) {
    return attemptRefresh(dispatch, options, 1);
  }

  dispatch(options.quiet ? signedOut() : sessionEnded());
  return 'ended';
}

function withCrossTabLock<T>(work: () => Promise<T>): Promise<T> {
  const locks = (globalThis.navigator as Navigator | undefined)?.locks;
  return locks ? locks.request('devsync-refresh', work) : work();
}
