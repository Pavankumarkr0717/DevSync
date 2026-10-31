import { act, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { diagramHandlers, saves, seedDiagrams, setRole, WS } from '../../support/handlers/diagrams';
import { documentOf, edgeDoc, flowNode, nodeDoc, openCanvas, preloadCanvas, selectNode } from '../../support/diagramCanvas';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

// Spec 004 US5: the UI offers each role only what the server allows (FR-030).

const listRoute = `/workspaces/${WS}/diagrams`;
const seeded = (role: 'owner' | 'editor' | 'viewer') =>
  seedDiagrams(
    [
      {
        name: 'Checkout',
        document: documentOf([nodeDoc('a', 'client', 'Web'), nodeDoc('b', 'database', 'DB', 300)], [edgeDoc('e1', 'a', 'b')]),
      },
    ],
    role,
  );

describe('diagram permissions in the web app', { timeout: 20_000 }, () => {
  beforeAll(preloadCanvas, 60_000);
  beforeEach(() => server.use(...diagramHandlers));

  it('a viewer can open and select, but sees no palette, handles, rename or delete, and a read-only panel', async () => {
    const [id] = seeded('viewer');
    await openCanvas(id);

    expect(screen.getByTestId('save-status')).toHaveTextContent('Read only');
    expect(screen.queryByTestId('palette')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Rename diagram' })).not.toBeInTheDocument();
    expect(flowNode('a').querySelectorAll('.react-flow__handle.connectable')).toHaveLength(0);

    selectNode('a');
    const panel = screen.getByTestId('properties-panel');
    expect(await within(panel).findByRole('textbox', { name: /Title/ })).toHaveAttribute('readonly');
    expect(within(panel).getByRole('combobox', { name: 'Type' })).toBeDisabled();
    expect(within(panel).queryByRole('button', { name: 'Delete component' })).not.toBeInTheDocument();
    expect(within(panel).queryByRole('button', { name: 'Add entry' })).not.toBeInTheDocument();
    expect(within(panel).queryByRole('combobox', { name: 'Connect to…' })).not.toBeInTheDocument();
    expect(saves()).toHaveLength(0);
  });

  it('a viewer sees the list without New diagram, rename or delete', async () => {
    seeded('viewer');
    renderApp({ route: listRoute });

    expect(await screen.findByRole('link', { name: 'Checkout' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New diagram' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Rename Checkout' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Delete Checkout' })).not.toBeInTheDocument();
  });

  it('an editor can create and rename, but is not offered delete', async () => {
    seeded('editor');
    renderApp({ route: listRoute });

    expect(await screen.findByRole('button', { name: 'Rename Checkout' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'New diagram' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Delete Checkout' })).not.toBeInTheDocument();
  });

  it('an editor can change the canvas', async () => {
    const [id] = seeded('editor');
    await openCanvas(id);

    expect(screen.getByTestId('palette')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Rename diagram' })).toBeInTheDocument();
    expect(flowNode('a').querySelectorAll('.react-flow__handle.connectable').length).toBeGreaterThan(0);
  });

  it('the owner is offered every action', async () => {
    seeded('owner');
    renderApp({ route: listRoute });

    expect(await screen.findByRole('button', { name: 'Rename Checkout' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'New diagram' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Delete Checkout' })).toBeInTheDocument();
  });

  describe('when the role changes while editing', () => {
    beforeEach(() => vi.useFakeTimers({ shouldAdvanceTime: true }));
    afterEach(() => vi.useRealTimers());

    it('a downgrade to viewer makes the canvas read only on the next save', async () => {
      const [id] = seeded('editor');
      const { user } = await openCanvas(id);
      setRole('viewer');

      await user.click(screen.getByRole('button', { name: 'Add Cache' }));
      await act(() => vi.advanceTimersByTimeAsync(1000));

      await waitFor(() => expect(screen.queryByTestId('palette')).not.toBeInTheDocument());
      expect(screen.getByTestId('save-status')).toHaveTextContent('You no longer have permission to edit');
      expect(flowNode('a').querySelectorAll('.react-flow__handle.connectable')).toHaveLength(0);
    });
  });
});
