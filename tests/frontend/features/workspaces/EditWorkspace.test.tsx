import { screen, waitFor, within } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { WorkspaceDetails } from '@/shared/api/types';
import { detailsHandler, problem, teamWorkspace, updateHandler, workspaceInvitationsHandler } from '../../support/handlers/workspaces';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

function serve(initial: WorkspaceDetails) {
  let current = initial;
  server.use(
    detailsHandler(() => HttpResponse.json(current)),
    workspaceInvitationsHandler([]),
    updateHandler((body) => {
      const { name, description } = body as { name: string; description: string | null };
      current = { ...current, name: name.trim(), description };
      return HttpResponse.json(current);
    }),
  );
}

describe('Editing a workspace', () => {
  it.each(['owner', 'editor'] as const)('%s can edit name and description', async (role) => {
    serve(teamWorkspace(role));
    const { user } = renderApp({ route: `/workspaces/${teamWorkspace().id}` });

    await user.click(await screen.findByRole('button', { name: 'Edit' }));
    const form = screen.getByRole('form', { name: 'Edit workspace' });
    await user.clear(within(form).getByLabelText('Name'));
    await user.type(within(form).getByLabelText('Name'), 'Renamed');
    await user.click(within(form).getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('heading', { name: 'Renamed' })).toBeInTheDocument();
  });

  it('viewers have no Edit button', async () => {
    serve(teamWorkspace('viewer'));
    renderApp({ route: `/workspaces/${teamWorkspace().id}` });

    await screen.findByRole('heading', { name: 'Interview Prep' });
    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument();
  });

  it('shows field errors from the client and the server', async () => {
    serve(teamWorkspace('owner'));
    server.use(updateHandler(() => problem(400, 'validation_failed', 'Invalid', { description: ['Description must not contain control characters other than line breaks and tabs.'] })));
    const { user } = renderApp({ route: `/workspaces/${teamWorkspace().id}` });

    await user.click(await screen.findByRole('button', { name: 'Edit' }));
    const form = screen.getByRole('form', { name: 'Edit workspace' });
    await user.clear(within(form).getByLabelText('Name'));
    await user.click(within(form).getByRole('button', { name: 'Save' }));
    expect(within(form).getByText('Name is required.')).toBeInTheDocument();

    await user.type(within(form).getByLabelText('Name'), 'Valid');
    await user.click(within(form).getByRole('button', { name: 'Save' }));
    expect(await within(form).findByText(/must not contain control characters/)).toBeInTheDocument();
  });

  it('a stale editor role refused by the server shows the permission toast and refetches', async () => {
    let detailsCalls = 0;
    server.use(
      detailsHandler(() => (detailsCalls++, HttpResponse.json(detailsCalls === 1 ? teamWorkspace('editor') : teamWorkspace('viewer')))),
      updateHandler(() => problem(403, 'forbidden', "You don't have permission to do that.")),
    );
    const { user } = renderApp({ route: `/workspaces/${teamWorkspace().id}` });

    await user.click(await screen.findByRole('button', { name: 'Edit' }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText("You don't have permission to do that.", { selector: '.toast' })).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument());
  });
});
