import type { DiagramEdgeDoc } from '../../shared/api/types';
import { LIMITS } from './componentTypes';

export const SELF_CONNECTION_TEXT = "A component can't connect to itself.";
export const DUPLICATE_CONNECTION_TEXT = 'These components are already connected in this direction.';
export const TOO_MANY_CONNECTIONS_TEXT = `A diagram can have at most ${LIMITS.maxEdges} connections.`;

/**
 * Why a new connection from `source` to `target` is refused, or null when it is allowed (spec 004
 * FR-016, FR-034). Structural rules only: any component type may connect to any other.
 */
export function connectionProblem(source: string, target: string, edges: readonly DiagramEdgeDoc[]): string | null {
  if (source === target) {
    return SELF_CONNECTION_TEXT;
  }
  if (edges.some((e) => e.source === source && e.target === target)) {
    return DUPLICATE_CONNECTION_TEXT;
  }
  if (edges.length >= LIMITS.maxEdges) {
    return TOO_MANY_CONNECTIONS_TEXT;
  }
  return null;
}
