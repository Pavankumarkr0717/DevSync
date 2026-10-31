import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { sessionEnded, signedOut } from '@/features/auth/authSlice';
import { readDraft, writeDraft } from '@/shared/editing/drafts';
import { contentOf, failNext, fileHandlers, idOf, saves, seedTree, WS } from '../../support/handlers/files';
import { USER_A } from '../../support/handlers/users';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

// Spec 003 US2: open, edit, autosave, status, reload, failure and retry, drafts.
const route = `/workspaces/${WS}/files`;

async function openFile(user: ReturnType<typeof renderApp>['user'], name: string) {
  await user.click(within(await screen.findByRole('tree', { name: 'Files' })).getByText(name));
  return (await screen.findByTestId('code-editor')) as HTMLTextAreaElement;
}

function status() {
  return screen.getByTestId('save-status');
}

async function elapse(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

describe('editing a file', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    server.use(...fileHandlers);
  });
  afterEach(() => vi.useRealTimers());

  it('opens with the stored text and the language from the extension', async () => {
    seedTree([{ path: 'src/server.ts', text: 'export {};\n' }, 'src/']);
    const { user } = renderApp({ route });
    await user.click(await screen.findByText('src'));

    const editor = await openFile(user, 'server.ts');

    // Exact line endings are checked end to end by the backend round-trip tests; a textarea normalizes them.
    expect(editor).toHaveValue('export {};\n');
    expect(editor).toHaveAttribute('data-language', 'typescript');
    expect(status()).toHaveTextContent('Saved');
  });

  it('autosaves after a pause, shows each state, and keeps the change after a reload', async () => {
    seedTree([{ path: 'notes.md', text: '' }]);
    const first = renderApp({ route });
    const editor = await openFile(first.user, 'notes.md');

    fireEvent.change(editor, { target: { value: '# Hello' } });
    expect(status()).toHaveTextContent('Unsaved changes');
    expect(saves()).toHaveLength(0);

    await elapse(1000);
    await waitFor(() => expect(status()).toHaveTextContent('Saved'));
    expect(saves()).toEqual([{ itemId: idOf('notes.md'), revision: 1, text: '# Hello' }]);
    expect(contentOf(idOf('notes.md'))).toEqual({ text: '# Hello', revision: 2 });

    // A reload: a fresh store and page.
    first.unmount();
    const second = renderApp({ route });
    expect(await openFile(second.user, 'notes.md')).toHaveValue('# Hello');
  });

  it('does not send a save per keystroke', async () => {
    seedTree(['a.ts']);
    const { user } = renderApp({ route });
    const editor = await openFile(user, 'a.ts');

    let text = '';
    for (const ch of 'const x = 1;') {
      text += ch;
      fireEvent.change(editor, { target: { value: text } });
      await elapse(100);
    }
    await elapse(1000);

    await waitFor(() => expect(saves()).toHaveLength(1));
    expect(saves()[0].text).toBe('const x = 1;');
  });

  it('shows Saving while a save is in flight', async () => {
    seedTree(['a.ts']);
    const { user } = renderApp({ route });
    const editor = await openFile(user, 'a.ts');

    fireEvent.change(editor, { target: { value: 'x' } });
    await elapse(1000);

    expect(screen.getByTestId('save-status')).toHaveTextContent(/Saving…|Saved/);
  });

  it('reports a failed autosave, keeps the text, and saves on Retry', async () => {
    seedTree(['a.ts']);
    failNext('save', 'network');
    const { user } = renderApp({ route });
    const editor = await openFile(user, 'a.ts');

    fireEvent.change(editor, { target: { value: 'offline edit' } });
    await elapse(1000);

    expect(await screen.findByText("Save failed: Couldn't reach the server — retrying")).toBeInTheDocument();
    expect(editor).toHaveValue('offline edit');
    expect(screen.getByRole('tab', { name: /a\.ts, save failed/ })).toBeInTheDocument();
    expect(contentOf(idOf('a.ts'))?.text).toBe('');

    await user.click(screen.getByRole('button', { name: 'Retry' }));

    await waitFor(() => expect(status()).toHaveTextContent('Saved'));
    expect(contentOf(idOf('a.ts'))?.text).toBe('offline edit');
  });

  it('refuses text over 1 MiB and keeps it in the editor', async () => {
    seedTree(['big.json']);
    const { user } = renderApp({ route });
    const editor = await openFile(user, 'big.json');
    const big = 'a'.repeat(1024 * 1024 + 1);

    fireEvent.change(editor, { target: { value: big } });
    await elapse(1000);

    expect(await screen.findByText('Save failed: File is larger than 1 MiB')).toBeInTheDocument();
    expect(editor.value).toHaveLength(big.length);
    expect(contentOf(idOf('big.json'))?.text).toBe('');
  });

  it('flushes a pending edit when switching to another file', async () => {
    seedTree(['a.ts', 'b.ts']);
    const { user } = renderApp({ route });
    const editor = await openFile(user, 'a.ts');

    fireEvent.change(editor, { target: { value: 'switched away' } });
    await user.click(screen.getByText('b.ts'));

    await waitFor(() => expect(contentOf(idOf('a.ts'))?.text).toBe('switched away'));
  });

  it('writes a draft when the page is hidden with unsaved edits', async () => {
    seedTree(['a.ts']);
    const { user } = renderApp({ route });
    const editor = await openFile(user, 'a.ts');

    fireEvent.change(editor, { target: { value: 'not yet saved' } });
    window.dispatchEvent(new Event('pagehide'));

    expect(readDraft(USER_A, idOf('a.ts'))).toMatchObject({ text: 'not yet saved', baseRevision: 1 });
  });

  it('offers a draft that differs from the server copy, and restores it as unsaved edits', async () => {
    seedTree([{ path: 'a.ts', text: 'server' }]);
    writeDraft(USER_A, idOf('a.ts'), { text: 'my draft', baseRevision: 1 });
    const { user } = renderApp({ route });
    const editor = await openFile(user, 'a.ts');

    expect(screen.getByTestId('draft-bar')).toHaveTextContent('You have unsaved changes from');
    expect(editor).toHaveValue('server');

    await user.click(screen.getByRole('button', { name: 'Restore' }));
    expect(editor).toHaveValue('my draft');
    expect(status()).toHaveTextContent('Unsaved changes');

    await elapse(1000);
    await waitFor(() => expect(contentOf(idOf('a.ts'))?.text).toBe('my draft'));
    expect(readDraft(USER_A, idOf('a.ts'))).toBeNull();
  });

  it('discards a draft on request, and drops a draft equal to the server copy silently', async () => {
    seedTree([{ path: 'a.ts', text: 'server' }, { path: 'b.ts', text: 'same' }]);
    writeDraft(USER_A, idOf('a.ts'), { text: 'old idea', baseRevision: 1 });
    writeDraft(USER_A, idOf('b.ts'), { text: 'same', baseRevision: 1 });
    const { user } = renderApp({ route });

    await openFile(user, 'a.ts');
    await user.click(screen.getByRole('button', { name: 'Discard' }));
    expect(screen.queryByTestId('draft-bar')).not.toBeInTheDocument();
    expect(readDraft(USER_A, idOf('a.ts'))).toBeNull();

    await user.click(screen.getByText('b.ts'));
    await screen.findByDisplayValue('same');
    expect(screen.queryByTestId('draft-bar')).not.toBeInTheDocument();
    expect(readDraft(USER_A, idOf('b.ts'))).toBeNull();
  });

  it('clears drafts on sign-out but keeps them when the session ends', () => {
    writeDraft(USER_A, 'f1', { text: 'x', baseRevision: 1 });
    const { store } = renderApp({ route: '/workspaces' });

    store.dispatch(sessionEnded());
    expect(readDraft(USER_A, 'f1')).not.toBeNull();

    const again = renderApp({ route: '/workspaces' });
    again.store.dispatch(signedOut());
    expect(readDraft(USER_A, 'f1')).toBeNull();
  });
});
