import { act, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import { getPresenceClient } from '@/features/presence/presenceClient';
import { fakeHub, presenceUser, snapshot } from '../../support/fakeHub';
import { renderApp } from '../../support/renderWithProviders';
import { presenceButton, serverSends, useWorkspaceHandlers, WS_A, WS_B } from './presenceTestKit';

// Spec 005 US6 (FR-022, FR-023, FR-027, SC-004): navigation keeps presence correct and clean.

describe('presence across navigation', () => {
  beforeEach(useWorkspaceHandlers);

  it('keeps one join while moving between the pages of a workspace', async () => {
    const { user } = renderApp({ route: `/workspaces/${WS_A}` });
    await presenceButton();

    await user.click(screen.getByRole('link', { name: 'Files' }));
    await presenceButton();
    await user.click(screen.getByRole('link', { name: 'Diagrams' }));
    await presenceButton();

    expect(fakeHub.connections).toHaveLength(1);
    expect(fakeHub.last.joins()).toEqual([WS_A]);
    expect(fakeHub.last.stops).toBe(0);
  });

  it('switches workspaces on the same connection and never mixes their members', async () => {
    fakeHub.answerJoin = (ws) => snapshot(ws, 1, ws === WS_A ? [presenceUser(0), presenceUser(1)] : [presenceUser(0)]);
    const { store } = renderApp({ route: `/workspaces/${WS_A}` });
    expect(await presenceButton()).toHaveAccessibleName('2 online — show who');

    // What WorkspaceScope does when the :workspaceId route parameter changes.
    await act(async () => getPresenceClient(store).join(WS_B));

    await waitFor(() => expect(store.getState().presence.users).toHaveLength(1));
    expect(store.getState().presence.workspaceId).toBe(WS_B);
    expect(fakeHub.connections).toHaveLength(1);
    expect(fakeHub.last.joins()).toEqual([WS_A, WS_B]);
    expect(fakeHub.last.stops).toBe(0);
  });

  it('drops a slow join for a workspace that is no longer shown', async () => {
    let releaseA!: () => void;
    fakeHub.answerJoin = (ws) =>
      ws === WS_A
        ? new Promise((resolve) => {
            releaseA = () => resolve(snapshot(WS_A, 9, [presenceUser(0), presenceUser(1), presenceUser(2)]));
          })
        : snapshot(WS_B, 1, [presenceUser(0)]);
    const { store } = renderApp({ route: `/workspaces/${WS_A}` });
    await waitFor(() => expect(fakeHub.connections.at(-1)?.joins()).toEqual([WS_A]));

    await act(async () => getPresenceClient(store).join(WS_B));
    await waitFor(() => expect(store.getState().presence.joined).toBe(true));
    await act(async () => releaseA());

    expect(store.getState().presence.workspaceId).toBe(WS_B);
    expect(store.getState().presence.users.map((u) => u.displayName)).toEqual(['Ana']);
  });

  it('ignores another workspace’s snapshot', async () => {
    renderApp({ route: `/workspaces/${WS_A}` });
    await presenceButton();

    await serverSends('OnlineUsersUpdated', snapshot(WS_B, 50, [presenceUser(1), presenceUser(2)]));

    expect(await presenceButton()).toHaveAccessibleName('1 online — show who');
  });

  it('closes the connection when leaving the workspace pages', async () => {
    const { user } = renderApp({ route: `/workspaces/${WS_A}` });
    await presenceButton();

    await user.click(within(screen.getByRole('navigation', { name: 'Breadcrumb' })).getByRole('link', { name: 'Workspaces' }));

    await waitFor(() => expect(fakeHub.last.stops).toBe(1));
    expect(fakeHub.connections).toHaveLength(1);
  });
});
