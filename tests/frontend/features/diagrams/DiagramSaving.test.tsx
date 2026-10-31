import { act, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { selectDiagramNodes } from '@/features/diagrams/diagramEditorSlice';
import { serialize } from '@/features/diagrams/document';
import { readDraft, writeDraft } from '@/shared/editing/drafts';
import {
  clearFailures,
  deleteDirectly,
  diagramById,
  diagramHandlers,
  failNext,
  refuseNextSave,
  saveDirectly,
  saves,
  seedDiagrams,
  setRole,
} from '../../support/handlers/diagrams';
import { USER_A } from '../../support/handlers/users';
import { documentOf, dragNode, flowNodeIds, nodeDoc, openCanvas, preloadCanvas } from '../../support/diagramCanvas';
import { server } from '../../support/server';

// Spec 004 US3: autosave on commit points, statuses, conflicts, failures, drafts and unsupported formats.

const status = () => screen.getByTestId('save-status');

async function elapse(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

const oneNode = () =>
  seedDiagrams([{ name: 'Checkout', document: documentOf([nodeDoc('a', 'client', 'Web')], [], { x: 0, y: 0, zoom: 1 }) }]);

describe('saving a diagram', { timeout: 20_000 }, () => {
  beforeAll(preloadCanvas, 60_000);
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    server.use(...diagramHandlers);
  });
  afterEach(() => vi.useRealTimers());

  it('one drag is one save after the pause, with Unsaved → Saving → Saved, and survives a reload', async () => {
    const [id] = oneNode();
    const first = await openCanvas(id);

    await act(async () => dragNode('a', 100, 60));
    expect(status()).toHaveTextContent('Unsaved changes');
    expect(saves()).toHaveLength(0);

    await elapse(1000);
    await waitFor(() => expect(status()).toHaveTextContent('Saved'));
    expect(saves()).toHaveLength(1);
    expect(saves()[0].revision).toBe(1);
    const moved = saves()[0].document.nodes[0].position;
    expect(moved).not.toEqual({ x: 0, y: 0 });
    expect(diagramById(id)!.revision).toBe(2);

    first.unmount();
    const second = await openCanvas(id);
    expect(selectDiagramNodes(second.store.getState())[0].position).toEqual(moved);
  });

  it('shows Saving… while the request is in flight', async () => {
    const [id] = oneNode();
    let release!: () => void;
    const gate = new Promise<void>((resolve) => (release = resolve));
    server.use(
      ...[
        (await import('msw')).http.put('*/api/workspaces/:ws/diagrams/:id/document', async () => {
          await gate;
          return undefined;
        }),
      ],
      ...diagramHandlers,
    );
    const { user } = await openCanvas(id);

    await user.click(screen.getByRole('button', { name: 'Add Database' }));
    await elapse(1000);
    await waitFor(() => expect(status()).toHaveTextContent('Saving…'));
    release();
    await waitFor(() => expect(status()).toHaveTextContent('Saved'));
  });

  it('panning and zooming alone never saves', async () => {
    const [id] = oneNode();
    const { user } = await openCanvas(id);

    await user.click(screen.getByRole('button', { name: 'Zoom in' }));
    await user.click(screen.getByRole('button', { name: 'Fit' }));
    await elapse(15_000);

    expect(saves()).toHaveLength(0);
    expect(status()).toHaveTextContent('Saved');
  });

  it('a conflict pauses saving, and Reload latest shows their version', async () => {
    const [id] = oneNode();
    const { user, store } = await openCanvas(id);
    saveDirectly(id, documentOf([nodeDoc('a', 'client', 'Their title')]));

    await user.click(screen.getByRole('button', { name: 'Add Cache' }));
    await elapse(1000);

    const bar = await screen.findByTestId('conflict-bar');
    expect(bar).toHaveTextContent('Someone else saved a newer version');
    expect(status()).toHaveTextContent('Save failed: Someone else saved a newer version');
    await elapse(15_000);
    expect(saves()).toHaveLength(1);

    await user.click(within(bar).getByRole('button', { name: 'Reload latest' }));
    await waitFor(() => expect(screen.queryByTestId('conflict-bar')).not.toBeInTheDocument());
    expect(selectDiagramNodes(store.getState()).map((n) => n.title)).toEqual(['Their title']);
    expect(status()).toHaveTextContent('Saved');
  });

  it('Overwrite with mine saves over their version', async () => {
    const [id] = oneNode();
    const { user } = await openCanvas(id);
    saveDirectly(id, documentOf([nodeDoc('a', 'client', 'Their title')]));

    await user.click(screen.getByRole('button', { name: 'Add Cache' }));
    await elapse(1000);
    await user.click(within(await screen.findByTestId('conflict-bar')).getByRole('button', { name: 'Overwrite with mine' }));

    await waitFor(() => expect(status()).toHaveTextContent('Saved'));
    expect(
      diagramById(id)!
        .document!.nodes.map((n) => n.type)
        .sort(),
    ).toEqual(['cache', 'client']);
    expect(diagramById(id)!.document!.nodes.find((n) => n.id === 'a')!.title).toBe('Web');
  });

  it('a deleted diagram becomes read only', async () => {
    const [id] = oneNode();
    const { user } = await openCanvas(id);
    deleteDirectly(id);

    await user.click(screen.getByRole('button', { name: 'Add Cache' }));
    await elapse(1000);

    expect(await screen.findByTestId('conflict-bar')).toHaveTextContent('This diagram was deleted');
    expect(screen.queryByTestId('palette')).not.toBeInTheDocument();
  });

  it('losing edit permission makes the canvas read only', async () => {
    const [id] = oneNode();
    const { user } = await openCanvas(id);
    setRole('viewer');

    await user.click(screen.getByRole('button', { name: 'Add Cache' }));
    await elapse(1000);

    expect(await screen.findByTestId('conflict-bar')).toHaveTextContent('You no longer have permission to edit');
    expect(screen.queryByTestId('palette')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Delete component' })).not.toBeInTheDocument();
  });

  it('a refused save shows the first problem and lists the rest under Details', async () => {
    const [id] = oneNode();
    const { user } = await openCanvas(id);
    refuseNextSave([
      { code: 'unknown_type', path: 'nodes[0].type', message: 'Unknown component type "printer".' },
      { code: 'dangling_edge', path: 'edges[0]', message: 'A connection refers to a missing component.' },
    ]);

    await user.click(screen.getByRole('button', { name: 'Add Cache' }));
    await elapse(1000);

    const problem = await screen.findByTestId('save-problem');
    expect(problem).toHaveTextContent('The diagram couldn\'t be saved: Unknown component type "printer".');
    await user.click(within(problem).getByRole('button', { name: 'Details' }));
    expect(within(problem).getByText('A connection refers to a missing component.')).toBeInTheDocument();
    expect(within(problem).getByText('edges[0]')).toBeInTheDocument();

    await user.click(within(problem).getByRole('button', { name: 'Retry' }));
    await waitFor(() => expect(status()).toHaveTextContent('Saved'));
  });

  it('retries by itself when the server cannot be reached', async () => {
    const [id] = oneNode();
    const { user } = await openCanvas(id);
    failNext('save', 'network');

    await user.click(screen.getByRole('button', { name: 'Add Cache' }));
    await elapse(1000);
    expect(await screen.findByTestId('save-problem')).toHaveTextContent("Couldn't reach the server — retrying");

    await elapse(2500);
    await waitFor(() => expect(status()).toHaveTextContent('Saved'));
    expect(diagramById(id)!.document!.nodes).toHaveLength(2);
  });

  it('keeps a draft when the page is hidden, and offers and restores it on return', async () => {
    const [id] = oneNode();
    // The server stays unavailable until the person comes back, so leaving can't save either.
    failNext('save', 503, 503, 503, 503, 503, 503);
    const first = await openCanvas(id);

    await first.user.click(screen.getByRole('button', { name: 'Add Cache' }));
    await act(async () => {
      Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'hidden' });
      document.dispatchEvent(new Event('visibilitychange'));
    });
    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' });
    expect(readDraft(USER_A, `diagram-${id}`)?.text).toContain('"cache"');

    first.unmount();
    const second = await openCanvas(id);
    const bar = await screen.findByTestId('draft-bar');
    clearFailures();
    expect(bar).toHaveTextContent('You have unsaved changes from');
    await second.user.click(within(bar).getByRole('button', { name: 'Restore' }));

    await waitFor(() => expect(flowNodeIds()).toHaveLength(2));
    await elapse(1000);
    await waitFor(() => expect(diagramById(id)!.document!.nodes).toHaveLength(2));
    expect(readDraft(USER_A, `diagram-${id}`)).toBeNull();
  });

  it('restoring a stale draft leads to a conflict, not an overwrite', async () => {
    const [id] = oneNode();
    writeDraft(USER_A, `diagram-${id}`, { text: serialize(documentOf([nodeDoc('a', 'client', 'Draft title')])), baseRevision: 1 });
    saveDirectly(id, documentOf([nodeDoc('a', 'client', 'Their title')]));
    const { user } = await openCanvas(id);

    await user.click(within(await screen.findByTestId('draft-bar')).getByRole('button', { name: 'Restore' }));
    await elapse(1000);

    expect(await screen.findByTestId('conflict-bar')).toHaveTextContent('Someone else saved a newer version');
    expect(diagramById(id)!.document!.nodes[0].title).toBe('Their title');
  });

  it('a newer stored format shows the banner, and Start over replaces it with an empty diagram', async () => {
    const [id] = seedDiagrams([{ name: 'Future', document: null, unsupportedSchemaVersion: 3 }]);
    const { user } = await import('../../support/renderWithProviders').then(({ renderApp }) =>
      renderApp({ route: `/workspaces/0193aaaa-0000-7000-8000-000000000001/diagrams/${id}` }),
    );

    const banner = await screen.findByTestId('unsupported-banner');
    expect(banner).toHaveTextContent("This diagram was saved by a newer version of DevSync (format 3) and can't be opened here.");
    await user.click(within(banner).getByRole('button', { name: 'Start over' }));
    await user.click(within(banner).getByRole('button', { name: 'Replace with empty diagram' }));

    expect(await screen.findByLabelText('Diagram canvas', {}, { timeout: 5000 })).toBeInTheDocument();
    expect(diagramById(id)!.document).toEqual({ schemaVersion: 1, nodes: [], edges: [], viewport: null });
  });

  it('a viewer sees the unsupported banner without Start over', async () => {
    const [id] = seedDiagrams([{ name: 'Future', document: null, unsupportedSchemaVersion: 3 }], 'viewer');
    const { renderApp } = await import('../../support/renderWithProviders');
    renderApp({ route: `/workspaces/0193aaaa-0000-7000-8000-000000000001/diagrams/${id}` });

    const banner = await screen.findByTestId('unsupported-banner');
    expect(within(banner).queryByRole('button', { name: 'Start over' })).not.toBeInTheDocument();
  });
});
