import { setupListeners } from '@reduxjs/toolkit/query';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { signedOut } from '@/features/auth/authSlice';
import { contentOf, deleteDirectly, failNext, fileHandlers, idOf, renameDirectly, saves, seedTree, WS } from '../../support/handlers/files';
import { USER_A } from '../../support/handlers/users';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

// Spec 003 US6: several files open at once, restored after a refresh (FR-031–FR-033).
const route = `/workspaces/${WS}/files`;

function tabs() {
  return within(screen.getByTestId('editor-tabs'))
    .getAllByRole('tab')
    .map((t) => t.textContent?.replace(/\s*[●!]$/, ''));
}

async function openInTree(user: ReturnType<typeof renderApp>['user'], name: string) {
  await user.click(within(await screen.findByRole('tree', { name: 'Files' })).getByText(name));
  return (await screen.findByTestId('code-editor')) as HTMLTextAreaElement;
}

describe('editor tabs', () => {
  beforeEach(() => server.use(...fileHandlers));

  it('opening an open file focuses its tab instead of adding another', async () => {
    seedTree(['a.ts', 'b.ts']);
    const { user } = renderApp({ route });

    await openInTree(user, 'a.ts');
    await openInTree(user, 'b.ts');
    await openInTree(user, 'a.ts');

    expect(tabs()).toEqual(['a.ts', 'b.ts']);
    expect(screen.getByRole('tab', { name: 'a.ts' })).toHaveAttribute('aria-selected', 'true');
  });

  it('allows 20 tabs and asks to close one before a 21st', async () => {
    seedTree(Array.from({ length: 21 }, (_, i) => `f${String(i).padStart(2, '0')}.ts`));
    const { user, store } = renderApp({ route });

    for (let i = 0; i < 21; i++) {
      await user.click(await screen.findByText(`f${String(i).padStart(2, '0')}.ts`, { selector: '.tree-name' }));
    }

    expect(tabs()).toHaveLength(20);
    expect(store.getState().ui.toast).toBe('Close a tab to open another file');
  }, 20_000); // 21 files opened one by one

  it('shows the folder for files with the same name', async () => {
    seedTree(['src/web/index.ts', 'src/api/index.ts', 'src/']);
    const { user } = renderApp({ route });
    await user.click(await screen.findByText('src'));
    await user.click(await screen.findByText('web'));
    await user.click(await screen.findByText('api'));

    const [api, web] = within(screen.getByRole('tree')).getAllByText('index.ts');
    await user.click(api);
    await user.click(web);

    expect(tabs()).toEqual(['index.ts — src/api', 'index.ts — src/web']);
  });

  it('closing a tab activates its neighbour', async () => {
    seedTree(['a.ts', 'b.ts', 'c.ts']);
    const { user } = renderApp({ route });
    await openInTree(user, 'a.ts');
    await openInTree(user, 'b.ts');
    await openInTree(user, 'c.ts');

    await user.click(screen.getByRole('tab', { name: 'b.ts' }));
    await user.click(screen.getByRole('button', { name: 'Close b.ts' }));

    expect(tabs()).toEqual(['a.ts', 'c.ts']);
    expect(screen.getByRole('tab', { name: 'c.ts' })).toHaveAttribute('aria-selected', 'true');
  });

  it('reopens the same tabs, in order, with the same active tab after a refresh', async () => {
    seedTree(['a.ts', 'b.ts', 'c.ts']);
    const first = renderApp({ route });
    await openInTree(first.user, 'c.ts');
    await openInTree(first.user, 'a.ts');
    await openInTree(first.user, 'b.ts');
    await first.user.click(screen.getByRole('tab', { name: 'a.ts' }));
    first.unmount();

    renderApp({ route });

    await waitFor(() => expect(screen.queryByTestId('editor-tabs')).toBeInTheDocument());
    expect(tabs()).toEqual(['c.ts', 'a.ts', 'b.ts']);
    expect(screen.getByRole('tab', { name: 'a.ts' })).toHaveAttribute('aria-selected', 'true');
  });

  it('silently drops a tab whose file was deleted before the refresh', async () => {
    seedTree(['a.ts', 'b.ts']);
    const first = renderApp({ route });
    await openInTree(first.user, 'a.ts');
    await openInTree(first.user, 'b.ts');
    first.unmount();
    deleteDirectly(idOf('a.ts'));

    renderApp({ route });

    await waitFor(() => expect(tabs()).toEqual(['b.ts']));
  });

  it('sign-out forgets the open tabs', async () => {
    seedTree(['a.ts']);
    const { user, store } = renderApp({ route });
    await openInTree(user, 'a.ts');
    expect(localStorage.getItem(`devsync:tabs:${USER_A}:${WS}`)).not.toBeNull();

    act(() => {
      store.dispatch(signedOut());
    });

    expect(localStorage.getItem(`devsync:tabs:${USER_A}:${WS}`)).toBeNull();
  });

  it('a rename by someone else shows in the tab after the window regains focus', async () => {
    seedTree(['a.ts']);
    const { user, store } = renderApp({ route });
    const unsubscribe = setupListeners(store.dispatch);
    await openInTree(user, 'a.ts');

    renameDirectly(idOf('a.ts'), 'renamed.ts');
    act(() => {
      window.dispatchEvent(new Event('focus'));
    });

    expect(await screen.findByRole('tab', { name: 'renamed.ts' })).toBeInTheDocument();
    unsubscribe();
  });

  describe('with unsaved edits', () => {
    beforeEach(() => vi.useFakeTimers({ shouldAdvanceTime: true }));
    afterEach(() => vi.useRealTimers());

    it('each tab keeps its own text and status, and switching saves the tab being left', async () => {
      seedTree([{ path: 'a.ts', text: 'A' }, { path: 'b.ts', text: 'B' }]);
      const { user } = renderApp({ route });
      const a = await openInTree(user, 'a.ts');
      fireEvent.change(a, { target: { value: 'A edited' } });
      expect(screen.getByRole('tab', { name: 'a.ts, unsaved changes' })).toBeInTheDocument();

      const b = await openInTree(user, 'b.ts');
      expect(b).toHaveValue('B');
      await waitFor(() => expect(contentOf(idOf('a.ts'))?.text).toBe('A edited'));
      expect(saves()).toHaveLength(1);

      fireEvent.change(b, { target: { value: 'B edited' } });
      await user.click(screen.getByRole('tab', { name: /a\.ts/ }));
      expect(await screen.findByDisplayValue('A edited')).toBeInTheDocument();
    });

    it('closing a dirty tab saves first; if the save fails the tab stays open', async () => {
      seedTree(['a.ts', 'b.ts']);
      const { user } = renderApp({ route });
      await openInTree(user, 'b.ts');
      const a = await openInTree(user, 'a.ts');

      fireEvent.change(a, { target: { value: 'saved on close' } });
      await user.click(screen.getByRole('button', { name: /Close a\.ts/ }));
      await waitFor(() => expect(tabs()).toEqual(['b.ts']));
      expect(contentOf(idOf('a.ts'))?.text).toBe('saved on close');

      const again = await openInTree(user, 'a.ts');
      failNext('save', 'network');
      fireEvent.change(again, { target: { value: 'cannot save' } });
      await user.click(screen.getByRole('button', { name: /Close a\.ts/ }));

      expect(await screen.findByText("Save failed: Couldn't reach the server — retrying")).toBeInTheDocument();
      expect(tabs()).toEqual(['b.ts', 'a.ts']);
    });
  });
});
