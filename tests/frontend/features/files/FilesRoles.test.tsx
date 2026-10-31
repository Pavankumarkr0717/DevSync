import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { contentOf, fileHandlers, idOf, saves, seedTree, setRole, WS } from '../../support/handlers/files';
import { detailsHandler, problem } from '../../support/handlers/workspaces';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

// Spec 003 US5: the UI offers each role only what the server allows (FR-024, FR-030).
const route = `/workspaces/${WS}/files`;

async function menuFor(user: ReturnType<typeof renderApp>['user'], name: string) {
  const item = await screen.findByRole('treeitem', { name });
  const button = within(item).queryByRole('button', { name: `Actions for ${name}` });
  if (!button) {
    return [];
  }
  await user.click(button);
  return within(screen.getByRole('menu')).getAllByRole('menuitem').map((m) => m.textContent);
}

describe('file permissions in the web app', () => {
  beforeEach(() => server.use(...fileHandlers));

  it('a viewer can browse and read, but sees no change actions and a read-only editor', async () => {
    seedTree(['src/', { path: 'src/a.ts', text: 'read me' }], 'viewer');
    const { user } = renderApp({ route });

    expect(await screen.findByText('Your role: viewer')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New file' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New folder' })).not.toBeInTheDocument();
    expect(await menuFor(user, 'src')).toEqual([]);

    await user.click(screen.getByText('src'));
    await user.click(await screen.findByText('a.ts'));
    const editor = await screen.findByTestId('code-editor');
    expect(editor).toHaveValue('read me');
    expect(editor).toHaveAttribute('readonly');
    expect(screen.getByTestId('save-status')).toHaveTextContent('Read only');
    expect(saves()).toHaveLength(0);
  });

  it('an editor can create, but is not offered delete', async () => {
    seedTree(['src/', 'src/a.ts'], 'editor');
    const { user } = renderApp({ route });

    expect(await screen.findByRole('button', { name: 'New file' })).toBeInTheDocument();
    const actions = await menuFor(user, 'src');
    expect(actions).toContain('New file');
    expect(actions).not.toContain('Delete');
  });

  it('the owner is offered every action', async () => {
    seedTree(['src/'], 'owner');
    const { user } = renderApp({ route });

    expect(await menuFor(user, 'src')).toEqual(expect.arrayContaining(['New file', 'New folder', 'Delete']));
  });

  describe('when the role changes while editing', () => {
    beforeEach(() => vi.useFakeTimers({ shouldAdvanceTime: true }));
    afterEach(() => vi.useRealTimers());

    it('a refused save makes the editor read-only and keeps the text copyable', async () => {
      seedTree([{ path: 'a.ts', text: 'v1' }], 'editor');
      const { user, store } = renderApp({ route });
      await user.click(await screen.findByText('a.ts'));
      const editor = (await screen.findByTestId('code-editor')) as HTMLTextAreaElement;

      setRole('viewer');
      fireEvent.change(editor, { target: { value: 'my unsaved work' } });
      await act(() => vi.advanceTimersByTimeAsync(1000));

      expect(await screen.findByTestId('conflict-bar')).toHaveTextContent('You no longer have permission to edit');
      expect(within(screen.getByTestId('conflict-bar')).getByRole('button', { name: 'Copy mine' })).toBeInTheDocument();
      expect(editor).toHaveAttribute('readonly');
      expect(editor).toHaveValue('my unsaved work');
      expect(contentOf(idOf('a.ts'))?.text).toBe('v1');
      // The role is fetched again, so the page now shows what the server says.
      await waitFor(() => expect(screen.getByText('Your role: viewer')).toBeInTheDocument());
      expect(store.getState().ui.toast).toBe("You don't have permission to do that.");
    });
  });

  it('a removed member sees "Workspace not found"', async () => {
    seedTree(['a.ts']);
    server.use(detailsHandler(() => problem(404, 'not_found', 'Workspace not found.')));
    renderApp({ route });

    expect(await screen.findByRole('heading', { name: 'Workspace not found' })).toBeInTheDocument();
  });
});
