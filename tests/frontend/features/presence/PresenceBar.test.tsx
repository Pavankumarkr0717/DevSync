import { screen, waitFor, within } from '@testing-library/react';
import { beforeAll, beforeEach, describe, expect, it } from 'vitest';
import { diagramHandlers, seedDiagrams } from '../../support/handlers/diagrams';
import { documentOf, nodeDoc, openCanvas, preloadCanvas } from '../../support/diagramCanvas';
import { server } from '../../support/server';
import { fakeHub, presenceUser, snapshot } from '../../support/fakeHub';
import { USERS } from '../../support/handlers/users';
import { renderApp } from '../../support/renderWithProviders';
import { presenceButton, serverSends, useWorkspaceHandlers, WS_A } from './presenceTestKit';

// Spec 005 US1 (FR-028, FR-029) and US4 (FR-006): who is online, shown on every workspace page.

const many = (n: number) =>
  Array.from({ length: n }, (_, i) =>
    i < USERS.length ? presenceUser(i) : { userId: `user-${i}`, displayName: `Member ${i}`, avatarUrl: null, status: 'online' as const },
  );

describe('PresenceBar', () => {
  beforeEach(useWorkspaceHandlers);

  it('connects, joins the workspace and shows who is online', async () => {
    fakeHub.answerJoin = (ws) => snapshot(ws, 3, [presenceUser(1), presenceUser(0)]);
    renderApp({ route: `/workspaces/${WS_A}` });

    const button = await presenceButton();
    expect(button).toHaveAccessibleName('2 online — show who');
    expect(screen.getAllByTestId('presence-avatar')).toHaveLength(2);
    expect(button).toHaveTextContent('AB'); // self (Ana) first, then Ben
    expect(fakeHub.connections).toHaveLength(1);
    expect(fakeHub.last.joins()).toEqual([WS_A]);
    expect(fakeHub.last.invocations[0].args[0]).toEqual({ v: 1, workspaceId: WS_A });
  });

  it('collapses avatars beyond four into +N and lists everyone in the summary, with (you)', async () => {
    fakeHub.answerJoin = (ws) => snapshot(ws, 1, many(6));
    const { user } = renderApp({ route: `/workspaces/${WS_A}` });

    const button = await presenceButton();
    expect(screen.getAllByTestId('presence-avatar')).toHaveLength(4);
    expect(button).toHaveTextContent('+2');
    expect(button).toHaveTextContent('6 online');

    await user.click(button);
    const summary = screen.getByRole('dialog', { name: 'Online now' });
    const names = within(summary).getAllByRole('listitem').map((li) => li.textContent);
    expect(names).toHaveLength(6);
    expect(names[0]).toBe('AAna (you)');
    expect(button).toHaveAttribute('aria-expanded', 'true');

    await user.keyboard('{Escape}');
    expect(screen.queryByRole('dialog', { name: 'Online now' })).not.toBeInTheDocument();
  });

  it('updates the list from server snapshots, ignoring stale ones', async () => {
    renderApp({ route: `/workspaces/${WS_A}` });
    await presenceButton();

    await serverSends('OnlineUsersUpdated', snapshot(WS_A, 4, [presenceUser(0), presenceUser(2)]));
    expect(await presenceButton()).toHaveAccessibleName('2 online — show who');

    await serverSends('OnlineUsersUpdated', snapshot(WS_A, 2, [presenceUser(0)]));
    await serverSends('OnlineUsersUpdated', { v: 2, workspaceId: WS_A, version: 9, users: [] });
    await serverSends('OnlineUsersUpdated', { v: 1, workspaceId: WS_A, version: 10, users: [{ nope: true }] });
    expect(await presenceButton()).toHaveAccessibleName('2 online — show who');
  });

  it.each([
    ['overview', `/workspaces/${WS_A}`],
    ['files', `/workspaces/${WS_A}/files`],
    ['diagrams', `/workspaces/${WS_A}/diagrams`],
  ])('appears on the %s page', async (_page, route) => {
    renderApp({ route });

    expect(await presenceButton()).toHaveAccessibleName('1 online — show who');
  });

  it('shows nothing, and does not break the page, when the join is refused', async () => {
    fakeHub.answerJoin = () => {
      throw new Error("An unexpected error occurred invoking 'JoinWorkspace' on the server. HubException: workspace_not_found");
    };
    renderApp({ route: `/workspaces/${WS_A}` });

    await screen.findByRole('navigation', { name: 'Workspace sections' });
    await waitFor(() => expect(fakeHub.last.joins()).toEqual([WS_A]));
    expect(screen.queryByRole('button', { name: /online — show who/ })).not.toBeInTheDocument();
  });

  it('opens no connection outside workspace pages', async () => {
    renderApp({ route: '/profile' });

    await screen.findByRole('heading', { name: /profile/i });
    expect(fakeHub.connections).toHaveLength(0);
  });

  describe('on the design canvas', { timeout: 20_000 }, () => {
    beforeAll(preloadCanvas, 60_000);

    it('shows who is online in the canvas header', async () => {
      server.use(...diagramHandlers);
      fakeHub.answerJoin = (ws) => snapshot(ws, 1, [presenceUser(0), presenceUser(2)]);
      const [id] = seedDiagrams([{ name: 'Checkout', document: documentOf([nodeDoc('a')]) }]);

      await openCanvas(id);

      expect(await presenceButton()).toHaveAccessibleName('2 online — show who');
      expect(fakeHub.last.joins()).toEqual([WS_A]);
    });
  });
});
