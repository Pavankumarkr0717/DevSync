import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { detailsHandler, invitation, problem, teamWorkspace, workspaceInvitationsHandler } from '../../support/handlers/workspaces';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

const workspace = teamWorkspace('owner');
const route = `/workspaces/${workspace.id}`;

describe('InvitationsPanel', () => {
  it('lists pending invitations and revokes one', async () => {
    let revoked: string | null = null;
    server.use(
      detailsHandler(() => HttpResponse.json(workspace)),
      workspaceInvitationsHandler([invitation()]),
      http.delete('*/api/workspaces/:id/invitations/:invitationId', ({ params }) => {
        revoked = String(params.invitationId);
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const { user } = renderApp({ route });

    const list = await screen.findByRole('list', { name: 'Pending invitations' });
    expect(within(list).getByRole('listitem')).toHaveTextContent('dee@example.com — editor');
    await user.click(screen.getByRole('button', { name: 'Revoke invitation for dee@example.com' }));

    await waitFor(() => expect(revoked).toBe(invitation().id));
  });

  it('invites an email with a role and clears the form', async () => {
    let body: unknown = null;
    server.use(
      detailsHandler(() => HttpResponse.json(workspace)),
      workspaceInvitationsHandler([]),
      http.post('*/api/workspaces/:id/invitations', async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(invitation({ email: 'eve@example.com', role: 'viewer' }), { status: 201 });
      }),
    );
    const { user } = renderApp({ route });

    const form = await screen.findByRole('form', { name: 'Invite a member' });
    await user.type(within(form).getByLabelText('Email'), 'eve@example.com');
    await user.selectOptions(within(form).getByLabelText('Role'), 'viewer');
    await user.click(within(form).getByRole('button', { name: 'Invite' }));

    await waitFor(() => expect(body).toEqual({ email: 'eve@example.com', role: 'viewer' }));
    await waitFor(() => expect(within(form).getByLabelText('Email')).toHaveValue(''));
  });

  it.each([
    [409, 'already_member', 'That person is already a member.'],
    [409, 'invitation_exists', 'A pending invitation for this email already exists.'],
    [400, 'cannot_invite_self', 'You cannot invite yourself.'],
  ] as const)('explains %s %s', async (status, code, title) => {
    server.use(
      detailsHandler(() => HttpResponse.json(workspace)),
      workspaceInvitationsHandler([]),
      http.post('*/api/workspaces/:id/invitations', () => problem(status, code, title)),
    );
    const { user } = renderApp({ route });

    const form = await screen.findByRole('form', { name: 'Invite a member' });
    await user.type(within(form).getByLabelText('Email'), 'someone@example.com');
    await user.click(within(form).getByRole('button', { name: 'Invite' }));

    expect(await screen.findByText(title)).toBeInTheDocument();
  });

  it('requires an email', async () => {
    server.use(detailsHandler(() => HttpResponse.json(workspace)), workspaceInvitationsHandler([]));
    const { user } = renderApp({ route });

    const form = await screen.findByRole('form', { name: 'Invite a member' });
    await user.click(within(form).getByRole('button', { name: 'Invite' }));

    expect(within(form).getByText('Email is required.')).toBeInTheDocument();
  });
});
