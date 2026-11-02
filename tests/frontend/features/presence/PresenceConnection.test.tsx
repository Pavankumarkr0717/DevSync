import { act, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { RETRY_DELAYS_MS } from '@/features/presence/presenceClient';
import { fakeHub, presenceUser, snapshot } from '../../support/fakeHub';
import { authSession } from '../../support/handlers/users';
import { renderApp, TEST_ACCESS_TOKEN } from '../../support/renderWithProviders';
import { server } from '../../support/server';
import { presenceButton, useWorkspaceHandlers, WS_A } from './presenceTestKit';

// Spec 005 US3 (FR-024–FR-027, FR-031): reconnection, restored state, giving up, token refresh.

const statusText = () => screen.getAllByRole('status').map((s) => s.textContent ?? '').join(' | ');

async function elapse(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

describe('presence connection', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    useWorkspaceHandlers();
  });
  afterEach(() => vi.useRealTimers());

  it('shows Reconnecting, marks the list stale, then restores it with a fresh join', async () => {
    let version = 2;
    fakeHub.answerJoin = (ws) => snapshot(ws, version, version === 2 ? [presenceUser(0)] : [presenceUser(0), presenceUser(1)]);
    renderApp({ route: `/workspaces/${WS_A}` });
    await presenceButton();
    const connection = fakeHub.last;

    connection.failStarts = 1;
    await act(async () => connection.drop());
    expect(statusText()).toContain('Reconnecting to DevSync…');
    expect(await presenceButton()).toHaveAccessibleName('1 online — show who, may be out of date');

    version = 1; // e.g. the server restarted: a lower version must still replace the list
    await elapse(RETRY_DELAYS_MS[1]);

    await waitFor(() => expect(statusText()).toContain('Connection restored.'));
    expect(await presenceButton()).toHaveAccessibleName('2 online — show who');
    expect(connection.joins()).toEqual([WS_A, WS_A]);

    await elapse(4_000);
    expect(statusText()).not.toContain('Connection restored.');
  });

  it('gives up after the retry schedule, then Retry reconnects', async () => {
    const { user } = renderApp({ route: `/workspaces/${WS_A}` });
    await presenceButton();
    const connection = fakeHub.last;

    fakeHub.down = true;
    await act(async () => connection.drop());
    for (const delay of RETRY_DELAYS_MS) {
      await elapse(delay + 1);
    }

    await waitFor(() => expect(statusText()).toContain('Live presence unavailable'));
    expect(connection.starts).toBe(1 + RETRY_DELAYS_MS.length);

    fakeHub.down = false;
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    await waitFor(() => expect(statusText()).toContain('Connection restored.'));
    expect(await presenceButton()).toHaveAccessibleName('1 online — show who');
  });

  it('keeps the workspace usable while presence is unavailable', async () => {
    fakeHub.down = true;
    renderApp({ route: `/workspaces/${WS_A}` });

    expect(await screen.findByRole('navigation', { name: 'Workspace sections' })).toBeInTheDocument();
    for (const delay of RETRY_DELAYS_MS) {
      await elapse(delay + 1);
    }
    await waitFor(() => expect(statusText()).toContain('Live presence unavailable'));
    expect(screen.getByRole('link', { name: 'Files' })).toBeInTheDocument();
  });

  it('refreshes an expiring access token before connecting', async () => {
    let refreshed = 0;
    server.use(
      http.post('*/api/auth/refresh', () => {
        refreshed += 1;
        return HttpResponse.json(authSession('renewed-token'));
      }),
    );
    const { store } = renderApp({ route: `/workspaces/${WS_A}` });
    // Make the in-memory token almost expired before the connection starts.
    store.dispatch({
      type: 'auth/tokenRefreshed',
      payload: { ...authSession('old-token'), accessTokenExpiresAt: new Date(Date.now() + 5_000).toISOString() },
    });

    await presenceButton();
    expect(refreshed).toBeGreaterThanOrEqual(1);
    expect(await fakeHub.last.accessTokenFactory()).toBe('renewed-token');
  });

  it('renews a token the server rejects (e.g. after a restart with a new key) and reconnects', async () => {
    server.use(http.post('*/api/auth/refresh', () => HttpResponse.json(authSession('renewed-token'))));
    renderApp({ route: `/workspaces/${WS_A}` });
    await presenceButton();
    const connection = fakeHub.last;
    fakeHub.rejectedTokens.add(TEST_ACCESS_TOKEN);

    await act(async () => connection.drop());
    await elapse(RETRY_DELAYS_MS[1]);

    await waitFor(() => expect(statusText()).toContain('Connection restored.'));
    expect(await connection.accessTokenFactory()).toBe('renewed-token');
  });

  it('stops reconnecting when the session has ended', async () => {
    server.use(http.post('*/api/auth/refresh', () => new HttpResponse(null, { status: 401 })));
    const { store } = renderApp({ route: `/workspaces/${WS_A}` });
    await presenceButton();
    const connection = fakeHub.last;
    store.dispatch({ type: 'auth/tokenRefreshed', payload: { ...authSession('old'), accessTokenExpiresAt: new Date(Date.now() - 1).toISOString() } });

    await act(async () => connection.drop());
    await elapse(RETRY_DELAYS_MS[1] + RETRY_DELAYS_MS[2]);

    await waitFor(() => expect(store.getState().auth.status).toBe('signedOut'));
    expect(connection.starts).toBe(2);
    expect(screen.queryByText('Live presence unavailable')).not.toBeInTheDocument();
  });

  it('registers event handlers once per connection, across reconnects', async () => {
    renderApp({ route: `/workspaces/${WS_A}` });
    await presenceButton();
    const connection = fakeHub.last;
    const registered = connection.onCalls;

    for (let i = 0; i < 3; i++) {
      await act(async () => connection.drop());
      await waitFor(() => expect(statusText()).toContain('Connection restored.'));
    }

    expect(registered).toBe(4);
    expect(connection.onCalls).toBe(4);
    expect(fakeHub.connections).toHaveLength(1);
  });
});
