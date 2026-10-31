import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeAll, beforeEach, describe, expect, it } from 'vitest';
import { COMPONENT_TYPES, LIMITS } from '@/features/diagrams/componentTypes';
import { selectDiagramEdges, selectDiagramNodes } from '@/features/diagrams/diagramEditorSlice';
import { diagramHandlers, seedDiagrams } from '../../support/handlers/diagrams';
import { documentOf, edgeDoc, flowNode, flowNodeIds, nodeDoc, openCanvas, preloadCanvas, selectNode } from '../../support/diagramCanvas';
import { server } from '../../support/server';

// Spec 004 US1: an empty canvas, adding each component type from the palette, rendering and deleting.

describe('diagram canvas', { timeout: 20_000 }, () => {
  beforeAll(preloadCanvas, 60_000);
  beforeEach(() => server.use(...diagramHandlers));

  it('an empty diagram shows the palette hint', async () => {
    const [id] = seedDiagrams(['Checkout']);
    await openCanvas(id);

    expect(screen.getByText('Drag a component from the palette to start.')).toBeInTheDocument();
    expect(within(screen.getByTestId('properties-panel')).getByText(/0 components · 0 connections/)).toBeInTheDocument();
  });

  it('the palette adds each of the 8 types with its default title and size, snapped to the grid', async () => {
    const [id] = seedDiagrams(['Checkout']);
    const { user, store } = await openCanvas(id);

    for (const t of COMPONENT_TYPES) {
      await user.click(screen.getByRole('button', { name: `Add ${t.label}` }));
    }

    const nodes = selectDiagramNodes(store.getState());
    expect(nodes).toHaveLength(8);
    for (const t of COMPONENT_TYPES) {
      const added = nodes.find((n) => n.type === t.type)!;
      expect(added.title).toBe(t.defaultTitle);
      expect(added.size).toEqual(t.defaultSize);
      expect(added.position.x % LIMITS.grid).toBe(0);
      expect(added.position.y % LIMITS.grid).toBe(0);
    }
    // Placed one after another, none stacked exactly on top of another.
    expect(new Set(nodes.map((n) => `${n.position.x},${n.position.y}`)).size).toBe(8);
    await waitFor(() => expect(flowNodeIds()).toHaveLength(8));
    expect(screen.queryByText('Drag a component from the palette to start.')).not.toBeInTheDocument();
  });

  it('dropping a palette item adds that type', async () => {
    const [id] = seedDiagrams(['Checkout']);
    const { store } = await openCanvas(id);

    const data = new Map<string, string>([['application/x-devsync-component', 'cache']]);
    fireEvent.drop(screen.getByTestId('diagram-canvas'), {
      clientX: 300,
      clientY: 200,
      dataTransfer: { getData: (type: string) => data.get(type) ?? '' },
    });

    expect(selectDiagramNodes(store.getState()).map((n) => n.type)).toEqual(['cache']);
  });

  it('renders each node with its type icon and title as text', async () => {
    const [id] = seedDiagrams([
      { name: 'Checkout', document: documentOf([nodeDoc('a', 'database', 'Orders DB'), nodeDoc('b', 'client', '<b>Web</b>', 300)]) },
    ]);
    await openCanvas(id);

    await waitFor(() => expect(flowNodeIds().sort()).toEqual(['a', 'b']));
    expect(within(flowNode('a')).getByText('Orders DB')).toBeInTheDocument();
    expect(flowNode('a').querySelector('.component-node')).toHaveAttribute('data-type', 'database');
    expect(flowNode('a').querySelector('.component-icon svg')).not.toBeNull();
    expect(within(flowNode('b')).getByText('<b>Web</b>')).toBeInTheDocument();
    expect(flowNode('b').querySelector('b')).toBeNull();
  });

  it('deleting a selected node removes it and its connections', async () => {
    const [id] = seedDiagrams([
      {
        name: 'Checkout',
        document: documentOf([nodeDoc('a', 'client', 'Web'), nodeDoc('b', 'api_gateway', 'Gateway', 300)], [edgeDoc('e1', 'a', 'b')]),
      },
    ]);
    const { user, store } = await openCanvas(id);

    await waitFor(() => expect(flowNodeIds()).toHaveLength(2));
    selectNode('b');
    const panel = screen.getByTestId('properties-panel');
    await user.click(await within(panel).findByRole('button', { name: 'Delete component' }));

    expect(selectDiagramNodes(store.getState()).map((n) => n.id)).toEqual(['a']);
    expect(selectDiagramEdges(store.getState())).toEqual([]);
    await waitFor(() => expect(flowNodeIds()).toEqual(['a']));
  });
});
