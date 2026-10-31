import { act, screen, waitFor } from '@testing-library/react';
import { beforeAll, beforeEach, describe, expect, it } from 'vitest';
import { selectDiagramNodes } from '@/features/diagrams/diagramEditorSlice';
import { diagramHandlers, saves, seedDiagrams } from '../../support/handlers/diagrams';
import { USER_A } from '../../support/handlers/users';
import { documentOf, dragNode, nodeDoc, openCanvas, preloadCanvas, selectNode } from '../../support/diagramCanvas';
import { server } from '../../support/server';

// Spec 004 US4: zoom, fit, the grid, multi-select and the stored view (FR-019, FR-020).

const zoomLevel = () => screen.getByLabelText('Zoom level').textContent;

describe('navigating the canvas', { timeout: 20_000 }, () => {
  beforeAll(preloadCanvas, 60_000);
  beforeEach(() => server.use(...diagramHandlers));

  it('restores the saved view on open', async () => {
    const [id] = seedDiagrams([{ name: 'Checkout', document: documentOf([nodeDoc('a')], [], { x: 40, y: 20, zoom: 1.5 }) }]);
    await openCanvas(id);

    expect(zoomLevel()).toBe('150%');
    expect(document.querySelector<HTMLElement>('.react-flow__viewport')!.style.transform).toBe('translate(40px,20px) scale(1.5)');
  });

  it('fits the diagram when no view was saved', async () => {
    const [id] = seedDiagrams([{ name: 'Checkout', document: documentOf([nodeDoc('a'), nodeDoc('b', 'database', 'DB', 2000, 1500)]) }]);
    await openCanvas(id);

    await waitFor(() => expect(zoomLevel()).not.toBe('100%'));
  });

  it('zoom buttons step and stop at 10% and 400%', async () => {
    const [id] = seedDiagrams([{ name: 'Checkout', document: documentOf([nodeDoc('a')], [], { x: 0, y: 0, zoom: 1 }) }]);
    const { user } = await openCanvas(id);

    await user.click(screen.getByRole('button', { name: 'Zoom in' }));
    await waitFor(() => expect(zoomLevel()).toBe('120%'));

    const zoomIn = screen.getByRole('button', { name: 'Zoom in' });
    for (let i = 0; i < 20 && !zoomIn.hasAttribute('disabled'); i++) {
      await user.click(zoomIn);
    }
    await waitFor(() => expect(zoomLevel()).toBe('400%'));
    expect(zoomIn).toBeDisabled();

    const zoomOut = screen.getByRole('button', { name: 'Zoom out' });
    for (let i = 0; i < 40 && !zoomOut.hasAttribute('disabled'); i++) {
      await user.click(zoomOut);
    }
    await waitFor(() => expect(zoomLevel()).toBe('10%'));
    expect(zoomOut).toBeDisabled();
  });

  it('Fit changes the view to show everything', async () => {
    const [id] = seedDiagrams([
      { name: 'Checkout', document: documentOf([nodeDoc('a'), nodeDoc('b', 'database', 'DB', 3000, 2000)], [], { x: 0, y: 0, zoom: 4 }) },
    ]);
    const { user } = await openCanvas(id);

    expect(zoomLevel()).toBe('400%');
    await user.click(screen.getByRole('button', { name: 'Fit' }));
    await waitFor(() => expect(zoomLevel()).not.toBe('400%'));
  });

  it('the grid toggle is remembered per person', async () => {
    const [id] = seedDiagrams(['Checkout']);
    const first = await openCanvas(id);

    const toggle = screen.getByRole('button', { name: 'Snap to grid' });
    expect(toggle).toHaveAttribute('aria-pressed', 'true');
    await first.user.click(toggle);
    expect(toggle).toHaveAttribute('aria-pressed', 'false');
    expect(window.localStorage.getItem(`devsync:diagram-grid:${USER_A}`)).toBe('off');

    first.unmount();
    await openCanvas(id);
    expect(screen.getByRole('button', { name: 'Snap to grid' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('several selected components move together as one change and one save', async () => {
    const [id] = seedDiagrams([
      {
        name: 'Checkout',
        document: documentOf([nodeDoc('a', 'client', 'Web'), nodeDoc('b', 'database', 'DB', 300)], [], { x: 0, y: 0, zoom: 1 }),
      },
    ]);
    const { store } = await openCanvas(id);

    selectNode('a');
    selectNode('b', true);
    const before = store.getState().diagramEditor.changeVersion;
    await act(async () => dragNode('a', 100, 60));

    expect(store.getState().diagramEditor.changeVersion).toBe(before + 1);
    const [a, b] = selectDiagramNodes(store.getState());
    expect(a.position).not.toEqual({ x: 0, y: 0 });
    expect(b.position.x - a.position.x).toBe(300);
    expect(b.position.y - a.position.y).toBe(0);

    // Real timers: the save follows the 1 s pause.
    await waitFor(() => expect(saves()).toHaveLength(1), { timeout: 4000 });
  });
});
