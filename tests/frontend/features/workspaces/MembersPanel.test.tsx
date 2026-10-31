import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { USERS } from '../../support/handlers/users';
import { detailsHandler, problem, teamWorkspace, workspaceInvitationsHandler } from '../../support/handlers/workspaces';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

const workspace = teamWorkspace('owner');
const route = `/workspaces/${workspace.id}`;

function memberRows() {
  return within(screen.getByRole('list', { name: 'Members' })).getAllByRole('listitem');
}

describe('MembersPanel', () => {
  afterEach(() => vi.restoreAllMocks());

  it('gives the owner role selectors and Remove for everyone but themselves, with emails', async () => {
    server.use(detailsHandler(() => HttpResponse.json(workspace)), workspaceInvitationsHandler([]));
    renderApp({ route });

    await screen.findByRole('heading', { name: 'Members' });
    const [ana, ben, cy] = memberRows();
    expect(within(ana).queryByRole('combobox')).not.toBeInTheDocument();
    expect(within(ana).queryByRole('button', { name: 'Remove' })).not.toBeInTheDocument();
    expect(within(ben).getByRole('combobox', { name: 'Role for Ben' })).toHaveValue('editor');
    expect(within(cy).getByRole('combobox', { name: 'Role for Cy' })).toHaveValue('viewer');
    expect(ben).toHaveTextContent('ben@example.com');
  });

  it('changes a role', async () => {
    let body: unknown = null;
    server.use(
      detailsHandler(() => HttpResponse.json(workspace)),
      workspaceInvitationsHandler([]),
      http.patch('*/api/workspaces/:id/members/:userId', async ({ request, params }) => {
        body = { userId: params.userId, ...((await request.json()) as object) };
        return HttpResponse.json({ ...workspace.members[1], role: 'viewer' });
      }),
    );
    const { user } = renderApp({ route });

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Role for Ben' }), 'viewer');

    await waitFor(() => expect(body).toEqual({ userId: USERS[1].id, role: 'viewer' }));
  });

  it('asks before removing a member', async () => {
    let removed = 0;
    server.use(
      detailsHandler(() => HttpResponse.json(workspace)),
      workspaceInvitationsHandler([]),
      http.delete('*/api/workspaces/:id/members/:userId', () => (removed++, new HttpResponse(null, { status: 204 }))),
    );
    const { user } = renderApp({ route });

    await screen.findByRole('heading', { name: 'Members' });
    await user.click(within(memberRows()[1]).getByRole('button', { name: 'Remove' }));
    const dialog = screen.getByRole('alertdialog', { name: 'Remove Ben?' });
    expect(dialog).toHaveTextContent('Remove Ben from Interview Prep?');
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(removed).toBe(0);

    await user.click(within(memberRows()[1]).getByRole('button', { name: 'Remove' }));
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Remove member' }));

    await waitFor(() => expect(removed).toBe(1));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
  });

  it.each(['editor', 'viewer'] as const)('shows %s members and roles but no controls or emails', async (role) => {
    server.use(detailsHandler(() => HttpResponse.json(teamWorkspace(role))));
    renderApp({ route });

    await screen.findByRole('heading', { name: 'Members' });
    expect(memberRows()).toHaveLength(3);
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Remove' })).not.toBeInTheDocument();
    expect(screen.queryByText(/@example\.com/)).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Invitations' })).not.toBeInTheDocument();
  });

  it('shows the permission toast and refetches when the server refuses', async () => {
    let detailsCalls = 0;
    server.use(
      detailsHandler(() => (detailsCalls++, HttpResponse.json(workspace))),
      workspaceInvitationsHandler([]),
      http.patch('*/api/workspaces/:id/members/:userId', () => problem(403, 'forbidden', "You don't have permission to do that.")),
    );
    const { user } = renderApp({ route });

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Role for Ben' }), 'viewer');

    expect(await screen.findByText("You don't have permission to do that.", { selector: '.toast' })).toBeInTheDocument();
    await waitFor(() => expect(detailsCalls).toBeGreaterThan(1));
  });
});
