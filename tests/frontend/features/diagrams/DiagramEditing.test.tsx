import { screen, waitFor, within } from '@testing-library/react';
import { beforeAll, beforeEach, describe, expect, it } from 'vitest';
import { DUPLICATE_CONNECTION_TEXT } from '@/features/diagrams/connectionRules';
import { LIMITS } from '@/features/diagrams/componentTypes';
import { selectDiagramEdges, selectDiagramNodes } from '@/features/diagrams/diagramEditorSlice';
import { diagramHandlers, seedDiagrams } from '../../support/handlers/diagrams';
import { documentOf, edgeDoc, flowNode, nodeDoc, openCanvas, preloadCanvas, selectEdge, selectNode } from '../../support/diagramCanvas';
import { server } from '../../support/server';

// Spec 004 US2: connecting components, labels and the properties panel (FR-012, FR-015–FR-018, FR-034, FR-036).

const twoNodes = () =>
  seedDiagrams([{ name: 'Checkout', document: documentOf([nodeDoc('a', 'client', 'Web'), nodeDoc('b', 'api_gateway', 'Gateway', 300)]) }]);

const panel = () => screen.getByTestId('properties-panel');

describe('editing a diagram', { timeout: 20_000 }, () => {
  beforeAll(preloadCanvas, 60_000);
  beforeEach(() => server.use(...diagramHandlers));

  it('connecting two components adds a directed edge', async () => {
    const [id] = twoNodes();
    const { user, store } = await openCanvas(id);

    selectNode('a');
    await user.selectOptions(await within(panel()).findByRole('combobox', { name: 'Connect to…' }), 'b');

    const edges = selectDiagramEdges(store.getState());
    expect(edges).toHaveLength(1);
    expect(edges[0]).toMatchObject({ source: 'a', target: 'b', label: '' });
    await waitFor(() => expect(document.querySelectorAll('.react-flow__edge')).toHaveLength(1));
  });

  it('a duplicate connection is refused with a message', async () => {
    const [id] = seedDiagrams([
      {
        name: 'Checkout',
        document: documentOf([nodeDoc('a', 'client', 'Web'), nodeDoc('b', 'database', 'DB', 300)], [edgeDoc('e1', 'a', 'b')]),
      },
    ]);
    const { user, store } = await openCanvas(id);

    selectNode('a');
    await user.selectOptions(await within(panel()).findByRole('combobox', { name: 'Connect to…' }), 'b');

    expect(await screen.findByText(DUPLICATE_CONNECTION_TEXT)).toBeInTheDocument();
    expect(selectDiagramEdges(store.getState())).toHaveLength(1);
  });

  it('the panel never offers a component a connection to itself', async () => {
    const [id] = twoNodes();
    await openCanvas(id);

    selectNode('a');
    const options = within(await within(panel()).findByRole('combobox', { name: 'Connect to…' })).getAllByRole('option');
    expect(options.map((o) => o.getAttribute('value'))).toEqual(['', 'b']);
  });

  it('edits an edge label in the panel', async () => {
    const [id] = seedDiagrams([
      {
        name: 'Checkout',
        document: documentOf([nodeDoc('a', 'client', 'Web'), nodeDoc('b', 'database', 'DB', 300)], [edgeDoc('e1', 'a', 'b')]),
      },
    ]);
    const { user, store } = await openCanvas(id);

    await waitFor(() => expect(document.querySelector('.react-flow__edge')).not.toBeNull());
    selectEdge('e1');
    expect(await within(panel()).findByText('Web → DB')).toBeInTheDocument();
    await user.type(within(panel()).getByRole('textbox', { name: /Label/ }), 'SQL');

    expect(selectDiagramEdges(store.getState())[0].label).toBe('SQL');
    expect(await screen.findByText('SQL', { selector: '.edge-label' })).toBeInTheDocument();
  });

  it('edits the title, description and type of a component', async () => {
    const [id] = twoNodes();
    const { user, store } = await openCanvas(id);

    selectNode('a');
    const title = await within(panel()).findByRole('textbox', { name: /Title/ });
    await user.clear(title);
    expect(within(panel()).getByText('Title is required.')).toBeInTheDocument();
    // An empty title is never committed.
    expect(selectDiagramNodes(store.getState()).find((n) => n.id === 'a')!.title).toBe('Web');

    await user.type(title, 'Mobile app');
    await user.type(within(panel()).getByRole('textbox', { name: /Description/ }), 'iOS and Android');
    await user.selectOptions(within(panel()).getByRole('combobox', { name: 'Type' }), 'external_service');

    const node = selectDiagramNodes(store.getState()).find((n) => n.id === 'a')!;
    expect(node).toMatchObject({ title: 'Mobile app', description: 'iOS and Android', type: 'external_service' });
    expect(within(flowNode('a')).getByText('Mobile app')).toBeInTheDocument();
  });

  it('enforces text limits as the person types', async () => {
    const [id] = twoNodes();
    const { user } = await openCanvas(id);

    selectNode('a');
    const title = await within(panel()).findByRole('textbox', { name: /Title/ });
    expect(title).toHaveAttribute('maxLength', String(LIMITS.maxTitle));
    expect(within(panel()).getByRole('textbox', { name: /Description/ })).toHaveAttribute('maxLength', String(LIMITS.maxDescription));
    await user.type(title, '!');
    expect(within(panel()).getByText(`4/${LIMITS.maxTitle}`)).toBeInTheDocument();
  });

  it('edits metadata, refusing a duplicate key and stopping at 20 entries', async () => {
    const [id] = twoNodes();
    const { user, store } = await openCanvas(id);

    selectNode('a');
    await user.click(await within(panel()).findByRole('button', { name: 'Add entry' }));
    await user.type(within(panel()).getByRole('textbox', { name: 'Metadata key 1' }), 'owner');
    await user.type(within(panel()).getByRole('textbox', { name: 'Metadata value 1' }), 'payments');
    expect(selectDiagramNodes(store.getState()).find((n) => n.id === 'a')!.metadata).toEqual([{ key: 'owner', value: 'payments' }]);

    await user.click(within(panel()).getByRole('button', { name: 'Add entry' }));
    await user.type(within(panel()).getByRole('textbox', { name: 'Metadata key 2' }), 'OWNER');
    expect(within(panel()).getByRole('alert')).toHaveTextContent('The key “OWNER” is used twice.');
    // The duplicate is never committed.
    const keys = selectDiagramNodes(store.getState())
      .find((n) => n.id === 'a')!
      .metadata.map((m) => m.key.toUpperCase());
    expect(new Set(keys).size).toBe(keys.length);

    await user.click(within(panel()).getByRole('button', { name: 'Remove metadata OWNER' }));
    for (let i = 2; i <= LIMITS.maxMetadata; i++) {
      await user.click(within(panel()).getByRole('button', { name: 'Add entry' }));
    }
    expect(within(panel()).getAllByRole('textbox', { name: /Metadata key/ })).toHaveLength(LIMITS.maxMetadata);
    expect(within(panel()).getByRole('button', { name: 'Add entry' })).toBeDisabled();
  });

  it('deleting a component also deletes its connections', async () => {
    const [id] = seedDiagrams([
      {
        name: 'Checkout',
        document: documentOf(
          [nodeDoc('a', 'client', 'Web'), nodeDoc('b', 'api_gateway', 'Gateway', 300), nodeDoc('c', 'database', 'DB', 600)],
          [edgeDoc('e1', 'a', 'b'), edgeDoc('e2', 'b', 'c'), edgeDoc('e3', 'a', 'c')],
        ),
      },
    ]);
    const { user, store } = await openCanvas(id);

    selectNode('b');
    await user.click(await within(panel()).findByRole('button', { name: 'Delete component' }));

    expect(selectDiagramEdges(store.getState()).map((e) => e.id)).toEqual(['e3']);
  });

  it('several selected items can be deleted together', async () => {
    const [id] = twoNodes();
    const { user, store } = await openCanvas(id);

    selectNode('a');
    selectNode('b', true);
    expect(await within(panel()).findByText('2 items selected')).toBeInTheDocument();
    await user.click(within(panel()).getByRole('button', { name: 'Delete selected' }));

    expect(selectDiagramNodes(store.getState())).toEqual([]);
  });

  it('script-like and prototype text is shown literally', async () => {
    const [id] = seedDiagrams([
      {
        name: 'Checkout',
        document: documentOf([
          {
            ...nodeDoc('a', 'client', '<script>alert(1)</script>'),
            metadata: [{ key: '__proto__', value: '<img src=x onerror=alert(1)>' }],
          },
        ]),
      },
    ]);
    await openCanvas(id);

    expect(within(flowNode('a')).getByText('<script>alert(1)</script>')).toBeInTheDocument();
    expect(document.querySelector('.react-flow script')).toBeNull();
    selectNode('a');
    expect(await within(panel()).findByDisplayValue('__proto__')).toBeInTheDocument();
    expect(within(panel()).getByDisplayValue('<img src=x onerror=alert(1)>')).toBeInTheDocument();
    expect(document.querySelector('img')).toBeNull();
  });
});
