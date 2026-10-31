import { screen, waitFor, within } from '@testing-library/react';
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

  it('the menu button collapses the navigation to a rail and remembers it', async () => {
    server.use(listHandler(() => Response.json(pageOf([]))));
    window.localStorage.setItem('devsync:nav-collapsed', 'false');

    const { user, container } = renderApp();

    const menu = await screen.findByRole('button', { name: 'Main menu' });
    expect(menu).toHaveAttribute('aria-expanded', 'true');
    expect(menu).toHaveAttribute('aria-controls', 'main-navigation');

    await user.click(menu);
    expect(menu).toHaveAttribute('aria-expanded', 'false');
    expect(container.querySelector('.shell')).toHaveClass('shell--collapsed');
    expect(window.localStorage.getItem('devsync:nav-collapsed')).toBe('true');
    // The rail keeps every destination reachable.
    const nav = screen.getByRole('navigation', { name: 'Main' });
    expect(within(nav).getByRole('link', { name: 'Workspaces' })).toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: /Invitations/ })).toBeInTheDocument();

    await user.click(menu);
    expect(menu).toHaveAttribute('aria-expanded', 'true');
    expect(container.querySelector('.shell')).not.toHaveClass('shell--collapsed');
  });

  it('switches between the light and dark themes and remembers the choice', async () => {
    server.use(listHandler(() => Response.json(pageOf([]))));

    const { user } = renderApp();

    const toggle = await screen.findByRole('button', { name: /Switch to (dark|light) theme/ });
    // DevSync starts dark unless light was chosen earlier.
    const start = toggle.getAttribute('aria-label') === 'Switch to light theme' ? 'dark' : 'light';
    await user.click(toggle);

    const next = start === 'dark' ? 'light' : 'dark';
    expect(document.documentElement.dataset.theme).toBe(next);
    expect(window.localStorage.getItem('devsync:theme')).toBe(next);
    expect(document.getElementById('devsync-fluent-theme')?.textContent).toContain('--colorBrandBackground:');
    expect(screen.getByRole('button', { name: `Switch to ${start} theme` })).toBeInTheDocument();
  });

  it('offers the theme switch on the sign-in page too', async () => {
    renderApp({ route: '/login', auth: 'signedOut' });

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Switch to (dark|light) theme/ })).toBeInTheDocument();
  });
});
