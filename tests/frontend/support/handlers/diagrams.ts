import { http, HttpResponse } from 'msw';
import { COMPONENT_TYPES, LIMITS } from '@/features/diagrams/componentTypes';
import type { DiagramDocument, DiagramProblem, DiagramSummary, WorkspaceRole } from '@/shared/api/types';
import { problem, workspaceDetails } from './workspaces';

// An in-memory implementation of specs/004-system-design-canvas/contracts/openapi.yaml for component
// tests: revisions and If-Match, the name rules, the structural document rules and role checks.

export const WS = '0193aaaa-0000-7000-8000-000000000001';

type Failure = number | 'network';
type Route = 'list' | 'get' | 'create' | 'rename' | 'delete' | 'save';

interface StoredDiagram {
  id: string;
  name: string;
  revision: number;
  document: DiagramDocument | null;
  unsupportedSchemaVersion: number | null;
  updatedAt: string;
}

interface DiagramStore {
  role: WorkspaceRole;
  diagrams: StoredDiagram[];
  failures: Partial<Record<Route, Failure[]>>;
  /** Problems the next document PUT is refused with (400 invalid_diagram). */
  refuseNext: DiagramProblem[] | null;
  /** Every document PUT that reached the server. */
  saves: Array<{ diagramId: string; revision: number; document: DiagramDocument }>;
}

let store: DiagramStore = emptyStore();
// Never reset: a request still in flight from an earlier test must not match a new test's diagrams.
let nextId = 1;

function emptyStore(): DiagramStore {
  return { role: 'owner', diagrams: [], failures: {}, refuseNext: null, saves: [] };
}

const now = '2026-10-31T10:00:00Z';

export function newDiagramId(): string {
  return `0195dddd-0000-7000-8000-${String(nextId++).padStart(12, '0')}`;
}

export const emptyDocument = (): DiagramDocument => ({ schemaVersion: 1, nodes: [], edges: [], viewport: null });

/** Resets the store with diagrams (empty documents unless given). Returns their ids in order. */
export function seedDiagrams(
  entries: Array<string | { name: string; document?: DiagramDocument | null; unsupportedSchemaVersion?: number }>,
  role: WorkspaceRole = 'owner',
): string[] {
  store = emptyStore();
  store.role = role;
  return entries.map((entry) => {
    const { name, document = emptyDocument(), unsupportedSchemaVersion = null } = typeof entry === 'string' ? { name: entry } : entry;
    const id = newDiagramId();
    store.diagrams.push({
      id,
      name,
      revision: 1,
      document,
      unsupportedSchemaVersion: document ? null : unsupportedSchemaVersion,
      updatedAt: now,
    });
    return id;
  });
}

export function setRole(role: WorkspaceRole) {
  store.role = role;
}

export function failNext(route: Route, ...failures: Failure[]) {
  store.failures[route] = [...(store.failures[route] ?? []), ...failures];
}

export function clearFailures() {
  store.failures = {};
}

/** Makes the next document save fail validation with these problems. */
export function refuseNextSave(problems: DiagramProblem[]) {
  store.refuseNext = problems;
}

export function diagramById(id: string) {
  return store.diagrams.find((d) => d.id === id);
}

export function diagrams() {
  return store.diagrams;
}

export function saves() {
  return store.saves;
}

/** Simulates someone else saving the diagram. */
export function saveDirectly(id: string, document: DiagramDocument) {
  const d = diagramById(id)!;
  d.document = document;
  d.revision++;
}

export function deleteDirectly(id: string) {
  store.diagrams = store.diagrams.filter((d) => d.id !== id);
}

function summary(d: StoredDiagram): DiagramSummary {
  return {
    id: d.id,
    name: d.name,
    revision: d.revision,
    nodeCount: d.document?.nodes.length ?? 0,
    edgeCount: d.document?.edges.length ?? 0,
    createdAt: now,
    updatedAt: d.updatedAt,
  };
}

function takeFailure(route: Route): Response | undefined {
  const failure = store.failures[route]?.shift();
  if (failure === undefined) {
    return undefined;
  }
  if (failure === 'network') {
    return HttpResponse.error();
  }
  const codes: Record<number, Parameters<typeof problem>[1]> = {
    403: 'forbidden',
    404: 'diagram_not_found',
    413: 'content_too_large',
    500: 'internal_error',
    503: 'service_unavailable',
  };
  return problem(failure, codes[failure] ?? 'internal_error', 'Failed.');
}

const canEdit = () => store.role === 'owner' || store.role === 'editor';
const forbidden = () => problem(403, 'forbidden', "You don't have permission to do that.");
const notFound = () => problem(404, 'diagram_not_found', 'Diagram not found.');

function nameRefused(name: string, exceptId?: string): Response | null {
  const trimmed = name.trim();
  if (trimmed.length === 0 || [...trimmed].length > 100) {
    return problem(400, 'validation_failed', 'The name is not allowed.', { name: ['Name must be 1-100 characters.'] });
  }
  if (store.diagrams.some((d) => d.id !== exceptId && d.name.toUpperCase() === trimmed.toUpperCase())) {
    return problem(409, 'diagram_name_taken', 'A diagram with this name already exists.');
  }
  return null;
}

const TYPES = new Set<string>(COMPONENT_TYPES.map((t) => t.type));

/** The structural rules the server checks (a subset of DiagramValidator that matters to the UI). */
export function documentProblems(doc: DiagramDocument): DiagramProblem[] {
  const problems: DiagramProblem[] = [];
  const ids = new Set<string>();
  doc.nodes.forEach((n, i) => {
    if (!TYPES.has(n.type))
      problems.push({ code: 'unknown_type', path: `nodes[${i}].type`, message: `Unknown component type "${n.type}".` });
    if (n.title.trim().length === 0)
      problems.push({ code: 'title_required', path: `nodes[${i}].title`, message: 'A component needs a title.' });
    if (ids.has(n.id)) problems.push({ code: 'duplicate_id', path: `nodes[${i}].id`, message: 'Duplicate id.' });
    ids.add(n.id);
  });
  const pairs = new Set<string>();
  doc.edges.forEach((e, i) => {
    if (!ids.has(e.source) || !ids.has(e.target)) {
      problems.push({ code: 'dangling_edge', path: `edges[${i}]`, message: 'A connection refers to a missing component.' });
    }
    if (e.source === e.target)
      problems.push({ code: 'self_connection', path: `edges[${i}]`, message: "A component can't connect to itself." });
    const key = `${e.source}>${e.target}`;
    if (pairs.has(key)) problems.push({ code: 'duplicate_connection', path: `edges[${i}]`, message: 'Duplicate connection.' });
    pairs.add(key);
  });
  if (doc.nodes.length > LIMITS.maxNodes) problems.push({ code: 'too_many_nodes', path: 'nodes', message: 'Too many components.' });
  return problems;
}

export const diagramHandlers = [
  http.get('*/api/workspaces/:ws', ({ params }) =>
    HttpResponse.json(workspaceDetails({ id: String(params.ws), name: 'Demo', myRole: store.role })),
  ),

  http.get('*/api/workspaces/:ws/diagrams', () => takeFailure('list') ?? HttpResponse.json(store.diagrams.map(summary))),

  http.post('*/api/workspaces/:ws/diagrams', async ({ request }) => {
    const failure = takeFailure('create');
    if (failure) return failure;
    if (!canEdit()) return forbidden();
    const { name } = (await request.json()) as { name: string };
    const refused = nameRefused(name);
    if (refused) return refused;
    if (store.diagrams.length >= 50) return problem(409, 'too_many_diagrams', 'This workspace has reached 50 diagrams.');
    const created: StoredDiagram = {
      id: newDiagramId(),
      name: name.trim(),
      revision: 1,
      document: emptyDocument(),
      unsupportedSchemaVersion: null,
      updatedAt: now,
    };
    store.diagrams.push(created);
    return HttpResponse.json(summary(created), { status: 201 });
  }),

  http.get('*/api/workspaces/:ws/diagrams/:id', ({ params }) => {
    const failure = takeFailure('get');
    if (failure) return failure;
    const d = diagramById(String(params.id));
    if (!d) return notFound();
    return HttpResponse.json(
      { ...summary(d), document: d.document, unsupportedSchemaVersion: d.unsupportedSchemaVersion },
      { headers: { ETag: `"${d.revision}"`, 'Cache-Control': 'no-store' } },
    );
  }),

  http.post('*/api/workspaces/:ws/diagrams/:id/rename', async ({ params, request }) => {
    const failure = takeFailure('rename');
    if (failure) return failure;
    if (!canEdit()) return forbidden();
    const d = diagramById(String(params.id));
    if (!d) return notFound();
    const { name } = (await request.json()) as { name: string };
    const refused = nameRefused(name, d.id);
    if (refused) return refused;
    d.name = name.trim();
    return HttpResponse.json(summary(d));
  }),

  http.delete('*/api/workspaces/:ws/diagrams/:id', ({ params }) => {
    const failure = takeFailure('delete');
    if (failure) return failure;
    if (store.role !== 'owner') return forbidden();
    if (!diagramById(String(params.id))) return notFound();
    deleteDirectly(String(params.id));
    return new HttpResponse(null, { status: 204 });
  }),

  http.put('*/api/workspaces/:ws/diagrams/:id/document', async ({ params, request }) => {
    const diagramId = String(params.id);
    const text = await request.text();
    const revision = Number(request.headers.get('If-Match')?.replace(/"/g, ''));
    const document = JSON.parse(text) as DiagramDocument;
    store.saves.push({ diagramId, revision, document });
    const failure = takeFailure('save');
    if (failure) return failure;
    if (!canEdit()) return forbidden();
    const d = diagramById(diagramId);
    if (!d) return notFound();
    if (!request.headers.get('If-Match')) return problem(428, 'revision_required', 'State the revision.');
    if (new TextEncoder().encode(text).length > LIMITS.maxDocumentBytes) {
      return problem(413, 'content_too_large', 'The diagram is larger than 1 MiB.');
    }
    if (revision !== d.revision) {
      return HttpResponse.json(
        {
          title: 'Someone else saved a newer version.',
          status: 412,
          code: 'revision_conflict',
          correlationId: 'corr-412',
          currentRevision: d.revision,
        },
        { status: 412, headers: { 'Content-Type': 'application/problem+json', ETag: `"${d.revision}"` } },
      );
    }
    if (d.document === null && new URL(request.url).searchParams.get('replaceUnsupported') !== 'true') {
      return problem(409, 'stored_version_unsupported', 'The stored diagram uses a newer format.');
    }
    const problems = store.refuseNext ?? documentProblems(document);
    store.refuseNext = null;
    if (problems.length > 0) {
      return HttpResponse.json(
        { title: "The diagram couldn't be saved.", status: 400, code: 'invalid_diagram', correlationId: 'corr-400', problems },
        { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
      );
    }
    d.document = document;
    d.unsupportedSchemaVersion = null;
    d.revision++;
    return HttpResponse.json({ revision: d.revision, nodeCount: document.nodes.length, edgeCount: document.edges.length, updatedAt: now });
  }),
];
