import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderApp } from '../support/renderWithProviders';

describe('NotFoundPage', () => {
  it('renders for unknown routes with a link back to workspaces', async () => {
    renderApp({ route: '/no/such/page' });

    expect(await screen.findByRole('heading', { name: 'Page not found' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /go to your workspaces/i })).toHaveAttribute('href', '/workspaces');
  });
});
