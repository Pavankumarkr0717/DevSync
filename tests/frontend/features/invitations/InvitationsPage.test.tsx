import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { ReceivedInvitation } from '@/shared/api/types';
import { USERS } from '../../support/handlers/users';
import { detailsHandler, workspaceDetails } from '../../support/handlers/workspaces';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

const received: ReceivedInvitation = {
  id: 'inv-1',
  workspace: { id: 'ws-9', name: 'Design Review' },
  role: 'editor',
  invitedBy: USERS[1],
  expiresAt: '2026-11-06T08:00:00Z',
};

function inbox(initial: ReceivedInvitation[]) {
  let current = initial;
  server.use(
    http.get('*/api/invitations', () => HttpResponse.json(current)),
    http.post('*/api/invitations/:id/decline', ({ params }) => {
      current = current.filter((i) => i.id !== params.id);
      return new HttpResponse(null, { status: 204 });
    }),
  );
}

describe('InvitationsPage', () => {
  it('shows the empty state', async () => {
    inbox([]);
    renderApp({ route: '/invitations' });

    expect(await screen.findByText('No pending invitations.')).toBeInTheDocument();
  });

  it('lists invitations with workspace, role and inviter', async () => {
    inbox([received]);
    renderApp({ route: '/invitations' });

    const list = await screen.findByRole('list', { name: 'Pending invitations' });
    const row = within(list).getByRole('listitem');
    expect(row).toHaveTextContent('Design Review as editor');
    expect(row).toHaveTextContent('from Ben');
  });

  it('accepting opens the workspace', async () => {
    inbox([received]);
    server.use(
      http.post('*/api/invitations/:id/accept', () =>
        HttpResponse.json({ id: 'ws-9', name: 'Design Review', role: 'editor', createdAt: '2026-10-29T10:00:00Z' }),
      ),
      detailsHandler(() => HttpResponse.json(workspaceDetails({ id: 'ws-9', name: 'Design Review', myRole: 'editor' }))),
    );
    const { user } = renderApp({ route: '/invitations' });

    await user.click(await screen.findByRole('button', { name: 'Accept' }));

    expect(await screen.findByRole('heading', { name: 'Design Review' })).toBeInTheDocument();
  });

  it('declining removes the invitation', async () => {
    inbox([received]);
    const { user } = renderApp({ route: '/invitations' });

    await user.click(await screen.findByRole('button', { name: 'Decline' }));

    await waitFor(() => expect(screen.getByText('No pending invitations.')).toBeInTheDocument());
  });
});
