import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { writeDraft } from '@/shared/editing/drafts';
import { bumpRevision, contentOf, deleteDirectly, fileHandlers, idOf, saves, seedTree, WS } from '../../support/handlers/files';
import { USER_A } from '../../support/handlers/users';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

// Spec 003 US3: a newer version on the server is never silently overwritten (FR-019, FR-023, FR-036).
const route = `/workspaces/${WS}/files`;

async function elapse(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** Edits the open file and lets autosave run. */
async function editAndSave(text: string) {
  const editor = (await screen.findByTestId('code-editor')) as HTMLTextAreaElement;
  fireEvent.change(editor, { target: { value: text } });
  await elapse(1000);
  return editor;
}

describe('conflicts', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    server.use(...fileHandlers);
  });
  afterEach(() => vi.useRealTimers());

  it('shows a conflict when someone else saved first, and pauses autosave', async () => {
    seedTree([{ path: 'a.ts', text: 'base' }]);
    const { user } = renderApp({ route });
    await user.click(await screen.findByText('a.ts'));
    await screen.findByTestId('code-editor');
    bumpRevision(idOf('a.ts'), 'their change');

    const editor = await editAndSave('my change');

    const bar = await screen.findByTestId('conflict-bar');
    expect(bar).toHaveTextContent('Someone else saved a newer version');
    expect(screen.getByTestId('save-status')).toHaveTextContent('Save failed: Someone else saved a newer version');

    const before = saves().length;
    fireEvent.change(editor, { target: { value: 'my change, more' } });
    await elapse(15_000);
    expect(saves()).toHaveLength(before);
    expect(contentOf(idOf('a.ts'))?.text).toBe('their change');
  });

  it('Reload latest discards my edits and shows the server copy', async () => {
    seedTree([{ path: 'a.ts', text: 'base' }]);
    const { user } = renderApp({ route });
    await user.click(await screen.findByText('a.ts'));
    await screen.findByTestId('code-editor');
    bumpRevision(idOf('a.ts'), 'their change');
    const editor = await editAndSave('my change');

    await user.click(within(await screen.findByTestId('conflict-bar')).getByRole('button', { name: 'Reload latest' }));

    await waitFor(() => expect(editor).toHaveValue('their change'));
    expect(screen.getByTestId('save-status')).toHaveTextContent('Saved');
    expect(screen.queryByTestId('conflict-bar')).not.toBeInTheDocument();

    fireEvent.change(editor, { target: { value: 'their change + mine' } });
    await elapse(1000);
    await waitFor(() => expect(contentOf(idOf('a.ts'))).toEqual({ text: 'their change + mine', revision: 3 }));
  });

  it('Overwrite with mine saves against the current revision', async () => {
    seedTree([{ path: 'a.ts', text: 'base' }]);
    const { user } = renderApp({ route });
    await user.click(await screen.findByText('a.ts'));
    await screen.findByTestId('code-editor');
    bumpRevision(idOf('a.ts'), 'their change');
    await editAndSave('my change');

    await user.click(within(await screen.findByTestId('conflict-bar')).getByRole('button', { name: 'Overwrite with mine' }));

    await waitFor(() => expect(contentOf(idOf('a.ts'))).toEqual({ text: 'my change', revision: 3 }));
    expect(saves()[saves().length - 1]).toMatchObject({ revision: 2, text: 'my change' });
    expect(screen.getByTestId('save-status')).toHaveTextContent('Saved');
  });

  it('Copy mine puts my text on the clipboard', async () => {
    seedTree([{ path: 'a.ts', text: 'base' }]);
    const { user } = renderApp({ route });
    // user-event installs its own clipboard; watch that one.
    const writeText = vi.spyOn(navigator.clipboard, 'writeText');
    await user.click(await screen.findByText('a.ts'));
    await screen.findByTestId('code-editor');
    bumpRevision(idOf('a.ts'));
    await editAndSave('precious');

    await user.click(within(await screen.findByTestId('conflict-bar')).getByRole('button', { name: 'Copy mine' }));

    expect(writeText).toHaveBeenCalledWith('precious');
  });

  it('a file deleted by someone else stops autosave and keeps my text copyable', async () => {
    seedTree([{ path: 'a.ts', text: 'base' }, 'b.ts']);
    const { user } = renderApp({ route });
    await user.click(await screen.findByText('a.ts'));
    await screen.findByTestId('code-editor');
    deleteDirectly(idOf('a.ts'));
    const editor = await editAndSave('orphaned work');

    const bar = await screen.findByTestId('conflict-bar');
    expect(bar).toHaveTextContent('This file was deleted');
    expect(within(bar).getByRole('button', { name: 'Copy mine' })).toBeInTheDocument();
    expect(within(bar).queryByRole('button', { name: 'Overwrite with mine' })).not.toBeInTheDocument();
    expect(editor).toHaveValue('orphaned work');
  });

  it('restoring a stale draft leads to a conflict, not an overwrite', async () => {
    seedTree([{ path: 'a.ts', text: 'server v1' }]);
    const id = idOf('a.ts');
    bumpRevision(id, 'server v2');
    writeDraft(USER_A, id, { text: 'draft from v1', baseRevision: 1 });
    const { user } = renderApp({ route });
    await user.click(await screen.findByText('a.ts'));
    await screen.findByTestId('draft-bar');

    await user.click(screen.getByRole('button', { name: 'Restore' }));
    await elapse(1000);

    expect(await screen.findByTestId('conflict-bar')).toHaveTextContent('Someone else saved a newer version');
    expect(contentOf(id)).toEqual({ text: 'server v2', revision: 2 });
  });
});
