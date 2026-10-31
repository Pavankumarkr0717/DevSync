import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { makeStore } from '@/app/store';
import { workspacesApi } from '@/features/workspaces/workspacesApi';
import { refreshHandler } from '../../support/handlers/auth';
import { authSession } from '../../support/handlers/users';
import { pageOf, problem } from '../../support/handlers/workspaces';
import { authState, TEST_ACCESS_TOKEN } from '../../support/renderWithProviders';
import { server } from '../../support/server';

function signedInStore() {
  return makeStore({ auth: authState('signedIn') });
}

/** Workspaces list that rejects the old token as expired and accepts only "fresh-access-token". */
function listAcceptingOnlyFreshToken(seen: string[]) {
  return http.get('*/api/workspaces', ({ request }) => {
    const auth = request.headers.get('Authorization') ?? '';
    seen.push(auth);
    return auth === 'Bearer fresh-access-token'
      ? HttpResponse.json(pageOf([]))
      : problem(401, 'token_expired', 'Your access token has expired.');
  });
}

describe('baseQueryWithReauth', () => {
  it('renews once for many concurrent expired requests, then retries each with the new token', async () => {
    let refreshCalls = 0;
    const seen: string[] = [];
    server.use(
      refreshHandler(() => (refreshCalls++, HttpResponse.json(authSession('fresh-access-token')))),
      listAcceptingOnlyFreshToken(seen),
    );
    const store = signedInStore();

    const results = await Promise.all(
      [1, 2, 3].map((page) => store.dispatch(workspacesApi.endpoints.listWorkspaces.initiate({ page, pageSize: 20 }))),
    );

    expect(refreshCalls).toBe(1);
    expect(results.every((r) => r.isSuccess)).toBe(true);
    expect(seen.filter((h) => h === `Bearer ${TEST_ACCESS_TOKEN}`)).toHaveLength(3);
    expect(seen.filter((h) => h === 'Bearer fresh-access-token')).toHaveLength(3);
    expect(store.getState().auth.accessToken).toBe('fresh-access-token');
  });

  it('retries a renewal once after a 409 conflict', async () => {
    let refreshCalls = 0;
    server.use(
      refreshHandler(() =>
        ++refreshCalls === 1
          ? problem(409, 'refresh_conflict', 'Session renewal is already in progress.')
          : HttpResponse.json(authSession('fresh-access-token')),
      ),
      listAcceptingOnlyFreshToken([]),
    );
    const store = signedInStore();

    const result = await store.dispatch(workspacesApi.endpoints.listWorkspaces.initiate({ page: 1, pageSize: 20 }));

    expect(refreshCalls).toBe(2);
    expect(result.isSuccess).toBe(true);
  });

  it('ends the session when renewal fails', async () => {
    server.use(listAcceptingOnlyFreshToken([])); // default refresh handler answers 401 session_ended
    const store = signedInStore();

    await store.dispatch(workspacesApi.endpoints.listWorkspaces.initiate({ page: 1, pageSize: 20 }));

    const auth = store.getState().auth;
    expect(auth.status).toBe('signedOut');
    expect(auth.accessToken).toBeNull();
    expect(auth.notice).toBe('Your session has ended — please sign in again.');
  });

  it('ends the session without renewing on any other 401', async () => {
    let refreshCalls = 0;
    server.use(
      refreshHandler(() => (refreshCalls++, HttpResponse.json(authSession()))),
      http.get('*/api/workspaces', () => problem(401, 'session_ended', 'Your session has ended.')),
    );
    const store = signedInStore();

    await store.dispatch(workspacesApi.endpoints.listWorkspaces.initiate({ page: 1, pageSize: 20 }));

    expect(refreshCalls).toBe(0);
    expect(store.getState().auth.status).toBe('signedOut');
  });

  it('turns 403 forbidden into a permission toast', async () => {
    server.use(http.put('*/api/workspaces/:id', () => problem(403, 'forbidden', "You don't have permission to do that.")));
    const store = signedInStore();

    await store.dispatch(workspacesApi.endpoints.updateWorkspace.initiate({ id: 'w1', name: 'x' }));

    expect(store.getState().ui.toast).toBe("You don't have permission to do that.");
    expect(store.getState().auth.status).toBe('signedIn');
  });

  it('sends the access token and client header on every request', async () => {
    let headers: Headers | null = null;
    server.use(
      http.get('*/api/workspaces', ({ request }) => {
        headers = request.headers;
        return HttpResponse.json(pageOf([]));
      }),
    );

    await signedInStore().dispatch(workspacesApi.endpoints.listWorkspaces.initiate({ page: 1, pageSize: 20 }));

    expect(headers!.get('Authorization')).toBe(`Bearer ${TEST_ACCESS_TOKEN}`);
    expect(headers!.get('X-DevSync-Client')).toBe('web');
  });
});
