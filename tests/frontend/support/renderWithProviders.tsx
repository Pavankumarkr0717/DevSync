import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { MemoryRouter } from 'react-router';
import { AppRoutes } from '@/app/router';
import { makeStore } from '@/app/store';
import { SessionBootstrap } from '@/features/auth/SessionBootstrap';
import type { AuthState, AuthStatus } from '@/features/auth/authSlice';
import { profile, USER_A } from './handlers/users';

export const TEST_ACCESS_TOKEN = 'test-access-token';

interface Options {
  route?: string;
  /** Initial session state; signed in as Ana by default. */
  auth?: AuthStatus;
  userId?: string;
  notice?: string | null;
}

export function authState(status: AuthStatus = 'signedIn', userId: string = USER_A, notice: string | null = null): AuthState {
  return status === 'signedIn'
    ? {
        status,
        user: profile(userId),
        accessToken: TEST_ACCESS_TOKEN,
        accessTokenExpiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
        notice,
      }
    : { status, user: null, accessToken: null, accessTokenExpiresAt: null, notice };
}

/** Renders the whole app (bootstrap, routes, store, router) at a route. */
export function renderApp({ route = '/workspaces', auth = 'signedIn', userId = USER_A, notice = null }: Options = {}) {
  const store = makeStore({ auth: authState(auth, userId, notice) });
  const user = userEvent.setup();
  const view = render(
    <Provider store={store}>
      <MemoryRouter initialEntries={[route]}>
        <SessionBootstrap>
          <AppRoutes />
        </SessionBootstrap>
      </MemoryRouter>
    </Provider>,
  );
  return { ...view, store, user };
}
