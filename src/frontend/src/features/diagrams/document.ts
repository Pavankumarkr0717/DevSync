import type { DiagramDocument, DiagramEdgeDoc, DiagramNodeDoc, Viewport } from '../../shared/api/types';

/**
 * The canonical document form (spec 004 research R9): nodes and edges sorted by id, fixed property
 * order, numbers rounded to 2 decimals. Two equal diagrams serialize identically, so "nothing changed
 * since the last save" is a string comparison, as for files.
 */

const round = (n: number) => Math.round(n * 100) / 100;
const byId = (a: { id: string }, b: { id: string }) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0);

function canonicalNode(n: DiagramNodeDoc): DiagramNodeDoc {
  return {
    id: n.id,
    type: n.type,
    title: n.title,
    description: n.description,
    metadata: n.metadata.map((m) => ({ key: m.key, value: m.value })),
    position: { x: round(n.position.x), y: round(n.position.y) },
    size: { width: round(n.size.width), height: round(n.size.height) },
  };
}

function canonicalEdge(e: DiagramEdgeDoc): DiagramEdgeDoc {
  return { id: e.id, source: e.source, target: e.target, label: e.label };
}

export function toDocument(nodes: readonly DiagramNodeDoc[], edges: readonly DiagramEdgeDoc[], viewport: Viewport | null): DiagramDocument {
  return {
    schemaVersion: 1,
    nodes: [...nodes].sort(byId).map(canonicalNode),
    edges: [...edges].sort(byId).map(canonicalEdge),
    viewport: viewport ? { x: round(viewport.x), y: round(viewport.y), zoom: Math.round(viewport.zoom * 1000) / 1000 } : null,
  };
}

export function serialize(document: DiagramDocument): string {
  return JSON.stringify(toDocument(document.nodes, document.edges, document.viewport));
}

/** Parses a draft or saved text back into a document; null if it isn't one. */
export function parseDocument(json: string): DiagramDocument | null {
  try {
    const value = JSON.parse(json) as DiagramDocument;
    return value && value.schemaVersion === 1 && Array.isArray(value.nodes) && Array.isArray(value.edges) ? value : null;
  } catch {
    return null;
  }
}

export const EMPTY_DOCUMENT: DiagramDocument = { schemaVersion: 1, nodes: [], edges: [], viewport: null };

export function newId(): string {
  return crypto.randomUUID();
}
