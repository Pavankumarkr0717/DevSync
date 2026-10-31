import { screen, waitFor } from '@testing-library/react';
import { delay, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { safeReturnTo } from '@/features/auth/returnTo';
import { loginHandler } from '../../support/handlers/auth';
import { authSession } from '../../support/handlers/users';
import { detailsHandler, listHandler, pageOf, problem, workspaceDetails } from '../../support/handlers/workspaces';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

async function fillAndSubmit(user: ReturnType<typeof renderApp>['user'], email: string, password: string) {
  await user.type(await screen.findByLabelText('Email'), email);
  await user.type(screen.getByLabelText('Password'), password);
  await user.click(screen.getByRole('button', { name: 'Sign in' }));
}

describe('LoginPage', () => {
  it('requires email and password before sending anything', async () => {
    let requests = 0;
    server.use(loginHandler(() => (requests++, HttpResponse.json(authSession()))));
    const { user } = renderApp({ route: '/login', auth: 'signedOut' });

    await user.click(await screen.findByRole('button', { name: 'Sign in' }));

    expect(screen.getByText('Email is required.')).toBeInTheDocument();
    expect(screen.getByText('Password is required.')).toBeInTheDocument();
    expect(requests).toBe(0);
  });

  it('shows one generic message for wrong password or unknown email', async () => {
    server.use(loginHandler(() => problem(401, 'invalid_credentials', 'Email or password is incorrect.')));
    const { user, store } = renderApp({ route: '/login', auth: 'signedOut' });

    await fillAndSubmit(user, 'ana@example.com', 'wrong password!!');

    expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.');
    expect(store.getState().auth.status).toBe('signedOut');
  });

  it('explains throttling', async () => {
    server.use(loginHandler(() => problem(429, 'too_many_attempts', 'Too many attempts.')));
    const { user } = renderApp({ route: '/login', auth: 'signedOut' });

    await fillAndSubmit(user, 'ana@example.com', 'whatever password');

    expect(await screen.findByRole('alert')).toHaveTextContent('Too many attempts. Try again in a few minutes.');
  });

  it('disables submit while signing in', async () => {
    server.use(
      loginHandler(async () => {
        await delay(100);
        return HttpResponse.json(authSession());
      }),
      listHandler(() => HttpResponse.json(pageOf([]))),
    );
    const { user } = renderApp({ route: '/login', auth: 'signedOut' });

    await fillAndSubmit(user, 'ana@example.com', 'correct horse battery');

    expect(await screen.findByRole('button', { name: 'Signing in…' })).toBeDisabled();
  });

  it('signs in and returns to the page the person asked for, sending the client header', async () => {
    let clientHeader: string | null = null;
    const workspace = workspaceDetails({ id: 'w-42', name: 'Return Target' });
    server.use(
      loginHandler((_body, request) => {
        clientHeader = request.headers.get('X-DevSync-Client');
        return HttpResponse.json(authSession());
      }),
      detailsHandler(() => HttpResponse.json(workspace)),
    );
    const { user, store } = renderApp({ route: '/workspaces/w-42', auth: 'signedOut' });

    await fillAndSubmit(user, 'ana@example.com', 'correct horse battery');

    expect(await screen.findByRole('heading', { name: 'Return Target' })).toBeInTheDocument();
    expect(clientHeader).toBe('web');
    expect(store.getState().auth.accessToken).toBe('fresh-access-token');
  });

  it('shows the session-ended notice', async () => {
    renderApp({ route: '/login', auth: 'signedOut', notice: 'Your session has ended — please sign in again.' });

    expect(await screen.findByRole('status')).toHaveTextContent('Your session has ended — please sign in again.');
  });

  it('never redirects off-site after sign-in', () => {
    expect(safeReturnTo('/workspaces/x?y=1')).toBe('/workspaces/x?y=1');
    expect(safeReturnTo('//evil.com')).toBe('/workspaces');
    expect(safeReturnTo('https://evil.com')).toBe('/workspaces');
    expect(safeReturnTo('/\\evil.com')).toBe('/workspaces');
    expect(safeReturnTo(null)).toBe('/workspaces');
  });

  it('ignores an off-site returnTo in the URL', async () => {
    server.use(loginHandler(() => HttpResponse.json(authSession())), listHandler(() => HttpResponse.json(pageOf([]))));
    const { user } = renderApp({ route: '/login?returnTo=%2F%2Fevil.com', auth: 'signedOut' });

    await fillAndSubmit(user, 'ana@example.com', 'correct horse battery');

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Your workspaces' })).toBeInTheDocument());
  });
});
