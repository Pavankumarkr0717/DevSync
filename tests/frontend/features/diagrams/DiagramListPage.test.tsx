import { screen, waitFor, within } from '@testing-library/react';
import { beforeAll, beforeEach, describe, expect, it } from 'vitest';
import { preloadCanvas } from '../../support/diagramCanvas';
import { diagramHandlers, diagrams, seedDiagrams, WS } from '../../support/handlers/diagrams';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

// Spec 004 US1: the diagram list, its empty state and creating a diagram (contracts/ui-routes.md).
const route = `/workspaces/${WS}/diagrams`;

describe('diagram list', () => {
  beforeAll(preloadCanvas, 60_000);
  beforeEach(() => server.use(...diagramHandlers));

  it('shows the empty state with New diagram', async () => {
    seedDiagrams([]);
    renderApp({ route });

    expect(await screen.findByText('No diagrams yet')).toBeInTheDocument();
    expect(screen.getByText(/Diagrams show how your system.s components connect/)).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'New diagram' }).length).toBeGreaterThan(0);
  });

  it('lists cards sorted by name ignoring case, with their counts', async () => {
    seedDiagrams([
      'payments',
      {
        name: 'Checkout',
        document: {
          schemaVersion: 1,
          nodes: [
            {
              id: 'a',
              type: 'client',
              title: 'Web',
              description: '',
              metadata: [],
              position: { x: 0, y: 0 },
              size: { width: 160, height: 100 },
            },
            {
              id: 'b',
              type: 'database',
              title: 'DB',
              description: '',
              metadata: [],
              position: { x: 200, y: 0 },
              size: { width: 160, height: 100 },
            },
          ],
          edges: [{ id: 'e', source: 'a', target: 'b', label: '' }],
          viewport: null,
        },
      },
      'API',
    ]);
    renderApp({ route });

    const list = await screen.findByTestId('diagram-list');
    const names = within(list)
      .getAllByRole('link')
      .filter((l) => l.className === 'card-title')
      .map((l) => l.textContent);
    expect(names).toEqual(['API', 'Checkout', 'payments']);
    expect(within(list).getByText('2 components · 1 connection')).toBeInTheDocument();
  });

  it('creates a diagram and opens it', async () => {
    seedDiagrams([]);
    const { user } = renderApp({ route });

    await user.click((await screen.findAllByRole('button', { name: 'New diagram' }))[0]);
    await user.type(screen.getByRole('textbox', { name: 'New diagram name' }), 'Checkout architecture');
    await user.click(screen.getByRole('button', { name: 'Create' }));

    expect(await screen.findByRole('heading', { name: /Checkout architecture/ })).toBeInTheDocument();
    expect(await screen.findByLabelText('Diagram canvas')).toBeInTheDocument();
    expect(diagrams().map((d) => d.name)).toEqual(['Checkout architecture']);
  });

  it('shows name errors from the form and the server', async () => {
    seedDiagrams(['Checkout']);
    const { user } = renderApp({ route });

    await user.click(await screen.findByRole('button', { name: 'New diagram' }));
    await user.click(screen.getByRole('button', { name: 'Create' }));
    expect(screen.getByRole('alert')).toHaveTextContent('Name is required.');

    await user.type(screen.getByRole('textbox', { name: 'New diagram name' }), 'x'.repeat(101));
    await user.click(screen.getByRole('button', { name: 'Create' }));
    expect(screen.getByRole('alert')).toHaveTextContent('Name must be at most 100 characters.');

    await user.clear(screen.getByRole('textbox', { name: 'New diagram name' }));
    await user.type(screen.getByRole('textbox', { name: 'New diagram name' }), 'CHECKOUT');
    await user.click(screen.getByRole('button', { name: 'Create' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('A diagram with this name already exists.');
    expect(diagrams()).toHaveLength(1);
  });

  it('opening a card navigates to the canvas', async () => {
    seedDiagrams(['Checkout']);
    const { user } = renderApp({ route });

    await user.click(await screen.findByRole('link', { name: 'Checkout' }));
    await waitFor(() => expect(screen.getByLabelText('Diagram canvas')).toBeInTheDocument());
    expect(screen.getByText('Drag a component from the palette to start.')).toBeInTheDocument();
  });

  it('the workspace overview links to the diagrams', async () => {
    seedDiagrams([]);
    const { user } = renderApp({ route: `/workspaces/${WS}` });

    await user.click(await screen.findByRole('link', { name: 'Open diagrams' }));
    expect(await screen.findByRole('heading', { name: 'Diagrams' })).toBeInTheDocument();
  });
});
