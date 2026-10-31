import { screen } from '@testing-library/react';
import { delay, http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import { fileHandlers, LIMITS, seedTree, WS } from '../../support/handlers/files';
import { detailsHandler, problem, teamWorkspace } from '../../support/handlers/workspaces';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

const route = `/workspaces/${WS}/files`;

describe('FilesPage', () => {
  beforeEach(() => {
    seedTree(['a.ts']);
    server.use(...fileHandlers);
  });

  it('shows loading, then the workspace name, the role and the tree', async () => {
    server.use(
      http.get('*/api/workspaces/:ws/tree', async () => {
        await delay(50);
        return HttpResponse.json({ items: [], limits: LIMITS });
      }),
    );
    renderApp({ route });

    expect(await screen.findByRole('status')).toHaveTextContent('Loading');
    expect(await screen.findByRole('heading', { name: 'Demo' })).toBeInTheDocument();
    expect(screen.getByText(/Your role: owner/i)).toBeInTheDocument();
    expect(await screen.findByText('No files yet')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /overview/i })).toHaveAttribute('href', `/workspaces/${WS}`);
  });

  it('shows "Workspace not found" for a missing workspace or a non-member', async () => {
    server.use(detailsHandler(() => problem(404, 'not_found', 'Workspace not found.')));
    renderApp({ route });

    expect(await screen.findByRole('heading', { name: 'Workspace not found' })).toBeInTheDocument();
  });

  it('sends signed-out visitors to sign in and back', async () => {
    renderApp({ route, auth: 'signedOut' });

    expect(await screen.findByRole('heading', { name: /sign in/i })).toBeInTheDocument();
  });

  it.each(['owner', 'editor', 'viewer'] as const)('the details page links to the files for a %s', async (role) => {
    server.use(detailsHandler(() => HttpResponse.json(teamWorkspace(role))));
    renderApp({ route: `/workspaces/${WS}` });

    expect(await screen.findByRole('link', { name: 'Open files' })).toHaveAttribute('href', `/workspaces/${WS}/files`);
  });
});
