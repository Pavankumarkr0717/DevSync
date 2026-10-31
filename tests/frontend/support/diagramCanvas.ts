import { createEvent, fireEvent, screen } from '@testing-library/react';
import type { ComponentType, DiagramDocument, DiagramEdgeDoc, DiagramNodeDoc, Viewport } from '@/shared/api/types';
import { WS } from './handlers/diagrams';
import { renderApp } from './renderWithProviders';

/** Loads the lazy canvas chunk once per file, so the first test isn't racing the module transform. */
export const preloadCanvas = () => import('@/features/diagrams/DiagramEditor');

export function nodeDoc(id: string, type: ComponentType = 'microservice', title = id, x = 0, y = 0): DiagramNodeDoc {
  return { id, type, title, description: '', metadata: [], position: { x, y }, size: { width: 160, height: 100 } };
}

export function edgeDoc(id: string, source: string, target: string, label = ''): DiagramEdgeDoc {
  return { id, source, target, label };
}

export function documentOf(nodes: DiagramNodeDoc[], edges: DiagramEdgeDoc[] = [], viewport: Viewport | null = null): DiagramDocument {
  return { schemaVersion: 1, nodes, edges, viewport };
}

/** Opens a diagram's canvas page and waits for the canvas. */
export async function openCanvas(diagramId: string, options: Parameters<typeof renderApp>[0] = {}) {
  const view = renderApp({ route: `/workspaces/${WS}/diagrams/${diagramId}`, ...options });
  await screen.findByLabelText('Diagram canvas', {}, { timeout: 5000 });
  return view;
}

/** The rendered React Flow node for a component id. */
export function flowNode(id: string): HTMLElement {
  const el = document.querySelector<HTMLElement>(`.react-flow__node[data-id="${id}"]`);
  if (!el) {
    throw new Error(`No node ${id}`);
  }
  return el;
}

export function flowNodeIds(): string[] {
  return [...document.querySelectorAll<HTMLElement>('.react-flow__node')].map((n) => n.dataset.id!);
}

export function flowEdgeIds(): string[] {
  return [...document.querySelectorAll<SVGElement>('.react-flow__edge')].map((e) => e.getAttribute('data-id') ?? e.id);
}

/**
 * Selects a node (shift adds to the selection). React Flow selects on click; user-event's mousedown
 * has no `view`, which d3-drag needs, so this fires the click alone.
 */
export function selectNode(id: string, shift = false) {
  // React Flow tracks modifier keys with its own key listeners, not the click's shiftKey.
  if (shift) {
    fireEvent.keyDown(document, { key: 'Shift', code: 'ShiftLeft' });
  }
  fireEvent.click(flowNode(id), { shiftKey: shift });
  if (shift) {
    fireEvent.keyUp(document, { key: 'Shift', code: 'ShiftLeft' });
  }
}

export function selectEdge(id: string) {
  const edge = document.querySelector(`.react-flow__edge[data-id="${id}"]`);
  if (!edge) {
    throw new Error(`No edge ${id}`);
  }
  fireEvent.click(edge);
}

/** A mouse event whose `view` is the window, as d3-drag expects (jsdom refuses it in the event init). */
function mouse(kind: 'mouseDown' | 'mouseMove' | 'mouseUp', target: Element | Window, init: MouseEventInit) {
  const event = createEvent[kind](target, { button: 0, ...init });
  Object.defineProperty(event, 'view', { value: window });
  fireEvent(target, event);
}

/** Drags a node by (dx, dy) screen pixels: down on the node, then moves and up on the window. */
export function dragNode(id: string, dx: number, dy: number) {
  mouse('mouseDown', flowNode(id), { clientX: 10, clientY: 10 });
  for (let step = 1; step <= 4; step++) {
    mouse('mouseMove', window, { buttons: 1, clientX: 10 + (dx * step) / 4, clientY: 10 + (dy * step) / 4 });
  }
  mouse('mouseUp', window, { clientX: 10 + dx, clientY: 10 + dy });
}
