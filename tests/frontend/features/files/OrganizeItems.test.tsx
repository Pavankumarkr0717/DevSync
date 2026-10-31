import { screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import { failNext, fileHandlers, idOf, items, seedTree, WS } from '../../support/handlers/files';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

// Spec 003 US4: rename, move and delete from the explorer (FR-013–FR-015, FR-030, FR-034).
const route = `/workspaces/${WS}/files`;

type User = ReturnType<typeof renderApp>['user'];

async function action(user: User, name: string, label: string) {
  const item = await screen.findByRole('treeitem', { name });
  await user.click(within(item).getByRole('button', { name: `Actions for ${name}` }));
  await user.click(screen.getByRole('menuitem', { name: label }));
}

describe('organizing files', () => {
  beforeEach(() => server.use(...fileHandlers));

  it('renames inline from the menu', async () => {
    seedTree(['server.ts']);
    const { user } = renderApp({ route });

    await action(user, 'server.ts', 'Rename');
    const input = screen.getByRole('textbox', { name: 'Rename server.ts' });
    expect(input).toHaveValue('server.ts');
    await user.clear(input);
    await user.type(input, 'app.ts{Enter}');

    expect(await screen.findByRole('treeitem', { name: 'app.ts' })).toBeInTheDocument();
    expect(items().map((i) => i.name)).toEqual(['app.ts']);
  });

  it('renames with F2 and shows server errors', async () => {
    seedTree(['a.ts', 'b.ts']);
    failNext('rename', 404);
    const { user } = renderApp({ route });

    const item = await screen.findByRole('treeitem', { name: 'a.ts' });
    item.focus();
    await user.keyboard('{F2}');
    const input = screen.getByRole('textbox', { name: 'Rename a.ts' });
    await user.clear(input);
    await user.type(input, 'B.TS');
    expect(screen.getByRole('alert')).toHaveTextContent('An item with this name already exists here.');

    await user.clear(input);
    await user.type(input, 'c.ts{Enter}');
    expect(await screen.findByRole('alert')).toHaveTextContent('That file or folder no longer exists.');
  });

  it('moves an item with the folder picker, where the item and its descendants cannot be chosen', async () => {
    seedTree(['src/api/v1/', 'lib/', 'README.md']);
    const { user } = renderApp({ route });

    await action(user, 'src', 'Move to…');
    const dialog = screen.getByRole('dialog', { name: 'Move src' });
    expect(within(dialog).getByRole('radio', { name: 'Top level' })).toBeInTheDocument();
    expect(within(dialog).getByRole('radio', { name: 'src/api' })).toBeDisabled();
    expect(within(dialog).getByRole('radio', { name: 'src/api/v1' })).toBeDisabled();

    await user.click(within(dialog).getByRole('radio', { name: 'lib' }));
    await user.click(within(dialog).getByRole('button', { name: 'Move' }));

    await waitFor(() => expect(items().find((i) => i.name === 'src')?.parentId).toBe(idOf('lib')));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('moves a file to the top level', async () => {
    seedTree(['src/a.ts']);
    const { user } = renderApp({ route });
    await user.click(await screen.findByText('src'));

    await action(user, 'a.ts', 'Move to…');
    await user.click(screen.getByRole('radio', { name: 'Top level' }));
    await user.click(screen.getByRole('button', { name: 'Move' }));

    await waitFor(() => expect(items().find((i) => i.name === 'a.ts')?.parentId).toBeNull());
  });

  it('shows move errors in the dialog', async () => {
    seedTree(['lib/a.ts', 'a.ts']);
    const { user } = renderApp({ route });

    await action(user, 'a.ts', 'Move to…');
    await user.click(screen.getByRole('radio', { name: 'lib' }));
    await user.click(screen.getByRole('button', { name: 'Move' }));

    expect(await within(screen.getByRole('dialog')).findByRole('alert')).toHaveTextContent('An item with this name already exists here.');
  });

  it('the owner deletes a folder after confirming how many items go with it', async () => {
    seedTree(['src/api/a.ts', 'src/api/b.ts', 'src/web/c.ts', 'src/main.ts', 'README.md']);
    const { user } = renderApp({ route });

    await action(user, 'src', 'Delete');
    const dialog = screen.getByRole('alertdialog');
    expect(dialog).toHaveTextContent('Delete src and 6 items inside? This can\'t be undone.');

    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    expect(items()).toHaveLength(8);

    await action(user, 'src', 'Delete');
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Delete' }));

    await waitFor(() => expect(screen.queryByRole('treeitem', { name: 'src' })).not.toBeInTheDocument());
    expect(items().map((i) => i.name)).toEqual(['README.md']);
  });

  it('deleting an open file closes its tab', async () => {
    seedTree(['a.ts', 'b.ts']);
    const { user } = renderApp({ route });
    await user.click(await screen.findByText('a.ts'));
    await screen.findByRole('tab', { name: /a\.ts/ });

    await action(user, 'a.ts', 'Delete');
    expect(screen.getByRole('alertdialog')).toHaveTextContent("Delete a.ts? This can't be undone.");
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Delete' }));

    await waitFor(() => expect(screen.queryByRole('tab', { name: /a\.ts/ })).not.toBeInTheDocument());
  });

  it('an editor is not offered delete; a vanished item gives a message and a fresh tree', async () => {
    seedTree(['src/'], 'editor');
    failNext('move', 404);
    const { user } = renderApp({ route });

    await user.click(within(await screen.findByRole('treeitem', { name: 'src' })).getByRole('button', { name: 'Actions for src' }));
    expect(screen.queryByRole('menuitem', { name: 'Delete' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('menuitem', { name: 'Move to…' }));
    await user.click(screen.getByRole('radio', { name: 'Top level' }));
    await user.click(screen.getByRole('button', { name: 'Move' }));

    expect(await within(screen.getByRole('dialog')).findByRole('alert')).toHaveTextContent('That file or folder no longer exists.');
  });
});
