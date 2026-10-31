import { screen, waitFor } from '@testing-library/react';
import { http } from 'msw';
import { describe, expect, it } from 'vitest';
import { myInvitationsHandler } from '../support/handlers/auth';
import { USERS } from '../support/handlers/users';
import { listHandler, pageOf } from '../support/handlers/workspaces';
import { renderApp } from '../support/renderWithProviders';
import { server } from '../support/server';

describe('AppShell', () => {
  it('sends signed-out visitors to sign in, remembering where they were going, without calling the API', async () => {
    let workspaceRequests = 0;
    server.use(
      http.all('*/api/workspaces*', () => {
        workspaceRequests++;
        return Response.json({});
      }),
    );

    renderApp({ route: '/workspaces/abc?tab=members', auth: 'signedOut' });

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(workspaceRequests).toBe(0);
  });

  it('shows the signed-in user, navigation and the pending invitation count', async () => {
    server.use(
      listHandler(() => Response.json(pageOf([]))),
      myInvitationsHandler([
        { id: 'i1', workspace: { id: 'w1', name: 'W1' }, role: 'editor', invitedBy: USERS[1], expiresAt: '2026-11-06T08:00:00Z' },
        { id: 'i2', workspace: { id: 'w2', name: 'W2' }, role: 'viewer', invitedBy: USERS[1], expiresAt: '2026-11-06T08:00:00Z' },
      ]),
    );

    renderApp();

    const nav = await screen.findByRole('navigation', { name: 'Main' });
    expect(nav).toHaveTextContent('Ana');
    await waitFor(() => expect(screen.getByRole('link', { name: 'Invitations (2)' })).toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument();
  });

  it('redirects / to the workspace list', async () => {
    server.use(listHandler(() => Response.json(pageOf([]))));

    renderApp({ route: '/' });

    expect(await screen.findByRole('heading', { name: 'Your workspaces' })).toBeInTheDocument();
  });
});
