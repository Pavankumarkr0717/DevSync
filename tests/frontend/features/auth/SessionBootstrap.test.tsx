import { screen } from '@testing-library/react';
import { delay, http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { loginHandler, refreshHandler } from '../../support/handlers/auth';
import { authSession } from '../../support/handlers/users';
import { listHandler, pageOf } from '../../support/handlers/workspaces';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

describe('SessionBootstrap', () => {
  it('shows only Loading and sends no protected request while checking', async () => {
    let protectedRequests = 0;
    server.use(
      refreshHandler(async () => {
        await delay(100);
        return HttpResponse.json(authSession());
      }),
      http.all('*/api/workspaces*', () => {
        protectedRequests++;
        return HttpResponse.json(pageOf([]));
      }),
    );

    renderApp({ auth: 'checking' });

    expect(screen.getByRole('status')).toHaveTextContent('Loading');
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    expect(protectedRequests).toBe(0);
  });

  it('restores the session from the refresh cookie', async () => {
    let clientHeader: string | null = null;
    server.use(
      refreshHandler((request) => {
        clientHeader = request.headers.get('X-DevSync-Client');
        return HttpResponse.json(authSession('restored-token'));
      }),
      listHandler(() => HttpResponse.json(pageOf([]))),
    );

    const { store } = renderApp({ auth: 'checking' });

    expect(await screen.findByRole('heading', { name: 'Your workspaces' })).toBeInTheDocument();
    expect(store.getState().auth.accessToken).toBe('restored-token');
    expect(clientHeader).toBe('web');
  });

  it('goes to sign-in quietly when there is no session to restore', async () => {
    renderApp({ auth: 'checking' });

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(screen.queryByText(/session has ended/)).not.toBeInTheDocument();
  });
});

describe('credential storage', () => {
  const writes: string[] = [];

  beforeEach(() => {
    writes.length = 0;
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation((key: string) => {
      writes.push(key);
    });
  });

  afterEach(() => vi.restoreAllMocks());

  it('never writes anything to web storage while signing in, renewing and signing out', async () => {
    const openSpy = vi.fn();
    vi.stubGlobal('indexedDB', { open: openSpy });
    server.use(
      refreshHandler(() => HttpResponse.json(authSession('restored-token'))),
      loginHandler(() => HttpResponse.json(authSession())),
      listHandler(() => HttpResponse.json(pageOf([]))),
    );
    const { user } = renderApp({ auth: 'checking' });

    await screen.findByRole('heading', { name: 'Your workspaces' });
    await user.click(screen.getByRole('button', { name: 'Sign out' }));
    await screen.findByRole('heading', { name: 'Sign in' });

    expect(writes).toEqual([]);
    expect(openSpy).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });
});
