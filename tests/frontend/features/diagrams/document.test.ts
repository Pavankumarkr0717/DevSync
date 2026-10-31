import { describe, expect, it } from 'vitest';
import {
  connectionProblem,
  DUPLICATE_CONNECTION_TEXT,
  SELF_CONNECTION_TEXT,
  TOO_MANY_CONNECTIONS_TEXT,
} from '@/features/diagrams/connectionRules';
import { LIMITS } from '@/features/diagrams/componentTypes';
import { parseDocument, serialize, toDocument } from '@/features/diagrams/document';
import type { DiagramEdgeDoc, DiagramNodeDoc } from '@/shared/api/types';

// Spec 004 research R9 (canonical form) and FR-016 (structural connection rules).

const node = (id: string, x = 0): DiagramNodeDoc => ({
  id,
  type: 'database',
  title: id,
  description: '',
  metadata: [],
  position: { x, y: 0 },
  size: { width: 160, height: 100 },
});

const edge = (id: string, source: string, target: string): DiagramEdgeDoc => ({ id, source, target, label: '' });

describe('canonical document', () => {
  it('sorts nodes and edges by id, so equal diagrams serialize identically', () => {
    const a = toDocument([node('b'), node('a')], [edge('2', 'a', 'b'), edge('1', 'b', 'a')], null);
    const b = toDocument([node('a'), node('b')], [edge('1', 'b', 'a'), edge('2', 'a', 'b')], null);
    expect(a.nodes.map((n) => n.id)).toEqual(['a', 'b']);
    expect(a.edges.map((e) => e.id)).toEqual(['1', '2']);
    expect(JSON.stringify(a)).toBe(JSON.stringify(b));
  });

  it('rounds coordinates to 2 decimals and zoom to 3', () => {
    const doc = toDocument([node('a', 10.123456)], [], { x: 1.23456, y: -0.005, zoom: 1.23456 });
    expect(doc.nodes[0].position.x).toBe(10.12);
    expect(doc.viewport).toEqual({ x: 1.23, y: -0, zoom: 1.235 });
  });

  it('drops unknown properties', () => {
    const extra = { ...node('a'), selected: true } as DiagramNodeDoc;
    expect(Object.keys(toDocument([extra], [], null).nodes[0])).toEqual([
      'id',
      'type',
      'title',
      'description',
      'metadata',
      'position',
      'size',
    ]);
  });

  it('round-trips through serialize and parse, keeping script-like text as data', () => {
    const n = { ...node('a'), title: '<script>alert(1)</script>', metadata: [{ key: '__proto__', value: 'x' }] };
    const text = serialize(toDocument([n], [], null));
    const back = parseDocument(text)!;
    expect(back.nodes[0].title).toBe('<script>alert(1)</script>');
    expect(back.nodes[0].metadata).toEqual([{ key: '__proto__', value: 'x' }]);
    expect(({} as Record<string, unknown>).x).toBeUndefined();
  });

  it('parse refuses text that is not a version 1 document', () => {
    expect(parseDocument('not json')).toBeNull();
    expect(parseDocument('{"schemaVersion":2,"nodes":[],"edges":[]}')).toBeNull();
    expect(parseDocument('null')).toBeNull();
  });
});

describe('connection rules', () => {
  it('allows any type to connect to any other', () => {
    expect(connectionProblem('a', 'b', [])).toBeNull();
  });

  it('refuses a self-connection', () => {
    expect(connectionProblem('a', 'a', [])).toBe(SELF_CONNECTION_TEXT);
  });

  it('refuses a duplicate in the same direction, but allows the reverse', () => {
    const edges = [edge('1', 'a', 'b')];
    expect(connectionProblem('a', 'b', edges)).toBe(DUPLICATE_CONNECTION_TEXT);
    expect(connectionProblem('b', 'a', edges)).toBeNull();
  });

  it('refuses a connection past the limit', () => {
    const edges = Array.from({ length: LIMITS.maxEdges }, (_, i) => edge(String(i), `s${i}`, `t${i}`));
    expect(connectionProblem('a', 'b', edges)).toBe(TOO_MANY_CONNECTIONS_TEXT);
  });
});
