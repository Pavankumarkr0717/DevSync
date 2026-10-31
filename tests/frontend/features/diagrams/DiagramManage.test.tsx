import { act, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { diagramById, diagramHandlers, diagrams, seedDiagrams, WS } from '../../support/handlers/diagrams';
import { documentOf, nodeDoc, openCanvas, preloadCanvas } from '../../support/diagramCanvas';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

// Spec 004 US6: renaming, deleting and keeping diagrams apart (FR-004–FR-006).

const listRoute = `/workspaces/${WS}/diagrams`;

describe('managing diagrams', { timeout: 20_000 }, () => {
  beforeAll(preloadCanvas, 60_000);
  beforeEach(() => server.use(...diagramHandlers));

  it('renames a diagram inline from its card', async () => {
    seedDiagrams(['Checkout', 'Payments']);
    const { user } = renderApp({ route: listRoute });

    await user.click(await screen.findByRole('button', { name: 'Rename Checkout' }, { timeout: 5000 }));
    const input = screen.getByRole('textbox', { name: 'New name for Checkout' });
    await user.clear(input);
    await user.click(screen.getByRole('button', { name: 'Rename' }));
    expect(screen.getByRole('alert')).toHaveTextContent('Name is required.');

    await user.type(input, 'payments');
    await user.click(screen.getByRole('button', { name: 'Rename' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('A diagram with this name already exists.');

    await user.clear(input);
    await user.type(input, 'Checkout v2');
    await user.click(screen.getByRole('button', { name: 'Rename' }));
    expect(await screen.findByRole('link', { name: 'Checkout v2' })).toBeInTheDocument();
    expect(diagrams().map((d) => d.name)).toEqual(['Checkout v2', 'Payments']);
  });

  it('Escape cancels a rename', async () => {
    seedDiagrams(['Checkout']);
    const { user } = renderApp({ route: listRoute });

    await user.click(await screen.findByRole('button', { name: 'Rename Checkout' }, { timeout: 5000 }));
    await user.type(screen.getByRole('textbox', { name: 'New name for Checkout' }), ' x{Escape}');
    expect(screen.getByRole('link', { name: 'Checkout' })).toBeInTheDocument();
    expect(diagrams()[0].name).toBe('Checkout');
  });

  it('renames from the canvas header', async () => {
    const [id] = seedDiagrams(['Checkout']);
    const { user } = await openCanvas(id);

    await user.click(screen.getByRole('button', { name: 'Rename diagram' }));
    const input = screen.getByRole('textbox', { name: 'Diagram name' });
    await user.clear(input);
    await user.type(input, 'Checkout flow{Enter}');

    expect(await screen.findByRole('heading', { name: /Checkout flow/ })).toBeInTheDocument();
    expect(diagramById(id)!.name).toBe('Checkout flow');
  });

  it('delete asks first, naming the diagram and its component count', async () => {
    seedDiagrams([{ name: 'Checkout', document: documentOf([nodeDoc('a'), nodeDoc('b'), nodeDoc('c')]) }, 'Payments']);
    const { user } = renderApp({ route: listRoute });

    await user.click(await screen.findByRole('button', { name: 'Delete Checkout' }, { timeout: 5000 }));
    const dialog = screen.getByRole('alertdialog', { name: 'Delete Checkout' });
    expect(dialog).toHaveTextContent("Delete Checkout and its 3 components? This can't be undone.");

    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    expect(diagrams()).toHaveLength(2);

    await user.click(screen.getByRole('button', { name: 'Delete Checkout' }));
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(screen.queryByRole('link', { name: 'Checkout' })).not.toBeInTheDocument());
    expect(diagrams().map((d) => d.name)).toEqual(['Payments']);
  });

  it('says when the workspace has reached 50 diagrams', async () => {
    seedDiagrams(Array.from({ length: 50 }, (_, i) => `Diagram ${i + 1}`));
    const { user } = renderApp({ route: listRoute });

    await user.click(await screen.findByRole('button', { name: 'New diagram' }));
    await user.type(screen.getByRole('textbox', { name: 'New diagram name' }), 'One more');
    await user.click(screen.getByRole('button', { name: 'Create' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('This workspace has reached 50 diagrams.');
  });

  it('a missing diagram says so and links back to the list', async () => {
    seedDiagrams([]);
    renderApp({ route: `${listRoute}/0195dddd-0000-7000-8000-999999999999` });

    expect(await screen.findByRole('heading', { name: 'Diagram not found' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to diagrams' })).toHaveAttribute('href', listRoute);
  });

  describe('with fake timers', () => {
    beforeEach(() => vi.useFakeTimers({ shouldAdvanceTime: true }));
    afterEach(() => vi.useRealTimers());

    it('editing one diagram leaves another untouched', async () => {
      const [a, b] = seedDiagrams([
        { name: 'A', document: documentOf([nodeDoc('n1')]) },
        { name: 'B', document: documentOf([nodeDoc('n1', 'database', 'Same id, other diagram')]) },
      ]);
      const before = structuredClone(diagramById(b));
      const { user } = await openCanvas(a);

      await user.click(screen.getByRole('button', { name: 'Add Cache' }));
      await act(() => vi.advanceTimersByTimeAsync(1000));
      await waitFor(() => expect(diagramById(a)!.revision).toBe(2));

      expect(diagramById(b)).toEqual(before);
    });
  });
});
