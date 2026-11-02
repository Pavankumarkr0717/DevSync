import { screen, waitFor } from '@testing-library/react';
import { http } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import { fakeHub, presenceUser, snapshot } from '../../support/fakeHub';
import { problem } from '../../support/handlers/workspaces';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';
import { presenceButton, serverSends, useWorkspaceHandlers, WS_A, WS_B } from './presenceTestKit';

// Spec 005 US2 (FR-030) and US5 (FR-032): join/leave notifications, once each, never about oneself.

const toast = () => screen.queryByRole('alert');

const joined = (version: number, index = 1, workspaceId = WS_A) => ({ v: 1, workspaceId, version, user: presenceUser(index) });
const left = (version: number, index = 1, reason = 'left') => ({ ...joined(version, index), reason });

describe('presence notifications', () => {
  beforeEach(useWorkspaceHandlers);

  async function openWorkspace() {
    fakeHub.answerJoin = (ws) => snapshot(ws, 5, [presenceUser(0)]);
    const view = renderApp({ route: `/workspaces/${WS_A}` });
    await presenceButton();
    return view;
  }

  it('announces a member joining and leaving', async () => {
    await openWorkspace();

    await serverSends('UserJoined', joined(6));
    expect(toast()).toHaveTextContent('Ben joined the workspace.');

    await serverSends('UserLeft', left(7));
    expect(toast()).toHaveTextContent('Ben left the workspace.');
  });

  it('shows each event once, even if it is delivered twice', async () => {
    const { store } = await openWorkspace();

    await serverSends('UserJoined', joined(6));
    store.dispatch({ type: 'ui/toastDismissed' });
    await serverSends('UserJoined', joined(6));

    expect(toast()).not.toBeInTheDocument();
  });

  it('never notifies about oneself', async () => {
    await openWorkspace();

    await serverSends('UserJoined', joined(6, 0));
    await serverSends('UserLeft', left(7, 0));

    expect(toast()).not.toBeInTheDocument();
  });

  it('ignores events from before the join, from another workspace, or of an unknown schema', async () => {
    await openWorkspace();

    await serverSends('UserJoined', joined(5));
    await serverSends('UserJoined', joined(3));
    await serverSends('UserJoined', joined(9, 1, WS_B));
    await serverSends('UserJoined', { ...joined(10), v: 2 });
    await serverSends('UserLeft', { ...left(11), reason: 'kicked' });

    expect(toast()).not.toBeInTheDocument();
  });

  it('when access is removed, says so and shows the workspace as not found', async () => {
    await openWorkspace();
    server.use(http.get('*/api/workspaces/:ws', () => problem(404, 'not_found', 'Not found')));

    await serverSends('AccessRevoked', { v: 1, workspaceId: WS_A });

    expect(toast()).toHaveTextContent('You no longer have access to this workspace.');
    expect(await screen.findByRole('heading', { name: 'Workspace not found' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /online — show who/ })).not.toBeInTheDocument();
  });

  it('ignores a revocation for another workspace', async () => {
    await openWorkspace();

    await serverSends('AccessRevoked', { v: 1, workspaceId: WS_B });

    await waitFor(() => expect(toast()).not.toBeInTheDocument());
    expect(await presenceButton()).toBeInTheDocument();
  });
});
