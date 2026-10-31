import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { listHandler, pageOf } from '../../support/handlers/workspaces';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

describe('LandingPage', () => {
  it('welcomes signed-out visitors with Get started and Sign in', async () => {
    const { user } = renderApp({ route: '/', auth: 'signedOut' });

    expect(await screen.findByRole('heading', { level: 1, name: /Build together/ })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Get started' })).toHaveAttribute('href', '/register');
    expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute('href', '/login');

    await user.click(screen.getByRole('link', { name: 'Get started' }));
    expect(await screen.findByRole('heading', { name: 'Create your account' })).toBeInTheDocument();
  });

  it('sends signed-in people to their workspaces', async () => {
    server.use(listHandler(() => Response.json(pageOf([]))));
    renderApp({ route: '/' });

    expect(await screen.findByRole('heading', { name: 'Your workspaces' })).toBeInTheDocument();
  });
});
