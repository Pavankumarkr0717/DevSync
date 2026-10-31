import { screen, within } from '@testing-library/react';
import { http } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import { failNext, fileHandlers, idOf, items, seedTree, WS } from '../../support/handlers/files';
import { problem } from '../../support/handlers/workspaces';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

const route = `/workspaces/${WS}/files`;

function tree() {
  return screen.findByRole('tree', { name: 'Files' });
}

function names(container: HTMLElement) {
  return within(container)
    .getAllByRole('treeitem')
    .map((item) => item.getAttribute('data-name'));
}

describe('FileExplorer', () => {
  beforeEach(() => server.use(...fileHandlers));

  it('lists folders first, then files, each by name ignoring case', async () => {
    seedTree(['b.ts', 'A.md', 'zeta/', 'Alpha/', 'c.py']);
    renderApp({ route });

    expect(names(await tree())).toEqual(['Alpha', 'zeta', 'A.md', 'b.ts', 'c.py']);
  });

  it('expands and collapses folders by click and with the keyboard', async () => {
    seedTree(['src/api/server.ts', 'README.md']);
    const { user } = renderApp({ route });
    const files = await tree();
    const src = within(files).getByRole('treeitem', { name: 'src' });

    expect(src).toHaveAttribute('aria-expanded', 'false');
    expect(within(files).queryByRole('treeitem', { name: 'api' })).not.toBeInTheDocument();

    await user.click(within(src).getByText('src'));
    expect(src).toHaveAttribute('aria-expanded', 'true');
    const api = within(files).getByRole('treeitem', { name: 'api' });

    api.focus();
    await user.keyboard('{ArrowRight}');
    expect(api).toHaveAttribute('aria-expanded', 'true');
    expect(within(files).getByRole('treeitem', { name: 'server.ts' })).toBeInTheDocument();

    await user.keyboard('{ArrowLeft}');
    expect(api).toHaveAttribute('aria-expanded', 'false');
    await user.keyboard('{ArrowUp}');
    expect(src).toHaveFocus();
    await user.keyboard('{Enter}');
    expect(src).toHaveAttribute('aria-expanded', 'false');
  });

  it('shows an empty state with create actions', async () => {
    seedTree([]);
    renderApp({ route });

    expect(await screen.findByText('No files yet')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'New file' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'New folder' })).toBeInTheDocument();
  });

  it('creates nested folders and a file, which opens', async () => {
    seedTree([]);
    const { user } = renderApp({ route });

    await user.click(await screen.findByRole('button', { name: 'New folder' }));
    await user.type(screen.getByRole('textbox', { name: 'New folder name' }), 'src{Enter}');
    const src = await screen.findByRole('treeitem', { name: 'src' });

    await user.click(within(src).getByRole('button', { name: 'Actions for src' }));
    await user.click(screen.getByRole('menuitem', { name: 'New folder' }));
    await user.type(screen.getByRole('textbox', { name: 'New folder name' }), 'api{Enter}');
    const api = await screen.findByRole('treeitem', { name: 'api' });

    await user.click(within(api).getByRole('button', { name: 'Actions for api' }));
    await user.click(screen.getByRole('menuitem', { name: 'New file' }));
    await user.type(screen.getByRole('textbox', { name: 'New file name' }), 'server.ts{Enter}');

    expect(await screen.findByRole('treeitem', { name: 'server.ts' })).toBeInTheDocument();
    expect(items().find((i) => i.name === 'server.ts')?.parentId).toBe(idOf('src/api'));
    expect(await screen.findByRole('tab', { name: /server\.ts/ })).toHaveAttribute('aria-selected', 'true');
  });

  it('validates names before submitting', async () => {
    seedTree(['Main.cs']);
    const { user } = renderApp({ route });

    await user.click(await screen.findByRole('button', { name: 'New file' }));
    const input = screen.getByRole('textbox', { name: 'New file name' });
    await user.type(input, 'notes.txt');
    expect(screen.getByRole('alert')).toHaveTextContent('Supported: .cs .js .ts');

    await user.clear(input);
    await user.type(input, 'main.CS');
    expect(screen.getByRole('alert')).toHaveTextContent('An item with this name already exists here.');

    await user.clear(input);
    await user.type(input, '../x.ts{Enter}');
    expect(screen.getByRole('alert')).toHaveTextContent('Name cannot contain "/"');
    expect(items()).toHaveLength(1);

    await user.keyboard('{Escape}');
    expect(screen.queryByRole('textbox', { name: 'New file name' })).not.toBeInTheDocument();
  });

  it.each([
    [409, 'too_many_items', 'This workspace has reached 5,000 files and folders'],
    [400, 'too_deep', 'Folders can be nested at most 20 levels'],
    [400, 'path_too_long', 'The full path would be longer than 1,024 characters'],
    [409, 'name_taken', 'An item with this name already exists here'],
  ] as const)('shows the message for a server %s %s', async (status, code, message) => {
    seedTree([]);
    server.use(http.post('*/api/workspaces/:ws/items', () => problem(status, code, message)));
    const { user } = renderApp({ route });

    await user.click(await screen.findByRole('button', { name: 'New folder' }));
    await user.type(screen.getByRole('textbox', { name: 'New folder name' }), 'ok{Enter}');

    expect(await screen.findByRole('alert')).toHaveTextContent(message);
  });

  it('renders names as text, never markup', async () => {
    seedTree(['<img src=x onerror=alert(1)>.md']);
    renderApp({ route });

    expect(await screen.findByText('<img src=x onerror=alert(1)>.md')).toBeInTheDocument();
    expect(document.querySelector('img')).toBeNull();
  });

  it('shows a retryable error when the tree cannot load', async () => {
    seedTree(['a.ts']);
    failNext('tree', 500);
    const { user } = renderApp({ route });

    await user.click(await screen.findByRole('button', { name: /retry/i }));
    expect(names(await tree())).toEqual(['a.ts']);
  });
});
