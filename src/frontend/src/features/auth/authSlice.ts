import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import type { AuthSession, UserProfile } from '../../shared/api/types';

export const SESSION_ENDED_NOTICE = 'Your session has ended — please sign in again.';

export type AuthStatus = 'checking' | 'signedIn' | 'signedOut';

/**
 * Who is signed in. Held in memory only: nothing here is ever written to localStorage,
 * sessionStorage or IndexedDB (spec 002 FR-015). The refresh token lives in an HttpOnly cookie.
 */
export interface AuthState {
  status: AuthStatus;
  user: UserProfile | null;
  accessToken: string | null;
  accessTokenExpiresAt: string | null;
  /** Shown on the sign-in page, e.g. after a session ended. */
  notice: string | null;
}

export const initialAuthState: AuthState = {
  status: 'checking',
  user: null,
  accessToken: null,
  accessTokenExpiresAt: null,
  notice: null,
};

function applySession(state: AuthState, session: AuthSession) {
  state.status = 'signedIn';
  state.user = session.user;
  state.accessToken = session.accessToken;
  state.accessTokenExpiresAt = session.accessTokenExpiresAt;
}

function clear(state: AuthState) {
  state.status = 'signedOut';
  state.user = null;
  state.accessToken = null;
  state.accessTokenExpiresAt = null;
}

const authSlice = createSlice({
  name: 'auth',
  initialState: initialAuthState,
  reducers: {
    signedIn(state, action: PayloadAction<AuthSession>) {
      applySession(state, action.payload);
      state.notice = null;
    },
    tokenRefreshed(state, action: PayloadAction<AuthSession>) {
      applySession(state, action.payload);
    },
    sessionEnded(state, action: PayloadAction<string | undefined>) {
      clear(state);
      state.notice = action.payload ?? SESSION_ENDED_NOTICE;
    },
    signedOut(state) {
      clear(state);
      state.notice = null;
    },
    noticeDismissed(state) {
      state.notice = null;
    },
  },
});

export const { signedIn, tokenRefreshed, sessionEnded, signedOut, noticeDismissed } = authSlice.actions;
export const authReducer = authSlice.reducer;

type WithAuth = { auth: AuthState };
export const selectAuth = (state: WithAuth) => state.auth;
export const selectAuthStatus = (state: WithAuth) => state.auth.status;
export const selectCurrentUser = (state: WithAuth) => state.auth.user;
