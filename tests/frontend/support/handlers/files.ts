import { http, HttpResponse } from 'msw';
import { validateName } from '@/features/files/fileRules';
import type { Item, ItemKind, Tree, WorkspaceRole } from '@/shared/api/types';
import { problem, workspaceDetails } from './workspaces';

// An in-memory implementation of specs/003-file-explorer-editor/contracts/openapi.yaml for component
// tests: revisions and If-Match, name rules, the 1 MiB limit and role checks behave like the API.

export const WS = '0193aaaa-0000-7000-8000-000000000001';
export const MAX_CONTENT_BYTES = 1024 * 1024;

export const LIMITS: Tree['limits'] = {
  maxNameLength: 255,
  maxPathLength: 1024,
  maxDepth: 20,
  maxItems: 5000,
  maxContentBytes: MAX_CONTENT_BYTES,
};

type Failure = number | 'network';
type Route = 'tree' | 'content' | 'save' | 'create' | 'rename' | 'move' | 'delete';

interface FileStore {
  role: WorkspaceRole;
  items: Item[];
  contents: Map<string, { text: string; revision: number }>;
  failures: Partial<Record<Route, Failure[]>>;
  /** Every content PUT that reached the server: [itemId, If-Match revision, text]. */
  saves: Array<{ itemId: string; revision: number; text: string }>;
}

let store: FileStore = emptyStore();
// Never reset: a request still in flight from an earlier test must not match a new test's items.
let nextId = 1;

function emptyStore(): FileStore {
  return { role: 'owner', items: [], contents: new Map(), failures: {}, saves: [] };
}

const now = '2026-10-31T10:00:00Z';

function newId(): string {
  return `0194ffff-0000-7000-8000-${String(nextId++).padStart(12, '0')}`;
}

/** Resets the store and creates items from paths: "src/" is a folder, anything else a file. */
export function seedTree(entries: Array<string | { path: string; text?: string }>, role: WorkspaceRole = 'owner') {
  store = emptyStore();
  store.role = role;
  for (const entry of entries) {
    const { path, text = '' } = typeof entry === 'string' ? { path: entry } : entry;
    const isFolder = path.endsWith('/');
    const parts = path.replace(/\/$/, '').split('/');
    let parentId: string | null = null;
    parts.forEach((name, index) => {
      const last = index === parts.length - 1;
      const kind: ItemKind = last && !isFolder ? 'file' : 'folder';
      const existing = store.items.find((i) => i.parentId === parentId && i.name === name);
      if (existing) {
        parentId = existing.id;
        return;
      }
      const created: Item = { id: newId(), parentId, kind, name, createdAt: now, updatedAt: now };
      store.items.push(created);
      if (kind === 'file') {
        store.contents.set(created.id, { text, revision: 1 });
      }
      parentId = created.id;
    });
  }
}

export function setRole(role: WorkspaceRole) {
  store.role = role;
}

/** Makes the next request(s) to a route fail with a status (Problem Details) or a network error. */
export function failNext(route: Route, ...failures: Failure[]) {
  store.failures[route] = [...(store.failures[route] ?? []), ...failures];
}

/** Simulates someone else saving the file. */
export function bumpRevision(itemId: string, text = 'their change') {
  const current = store.contents.get(itemId)!;
  store.contents.set(itemId, { text, revision: current.revision + 1 });
}

/** Simulates someone else changing the tree. */
export function renameDirectly(itemId: string, name: string) {
  store.items.find((i) => i.id === itemId)!.name = name;
}

export function deleteDirectly(itemId: string) {
  removeSubtree(itemId);
}

export function idOf(path: string): string {
  let parentId: string | null = null;
  let found: Item | undefined;
  for (const name of path.replace(/\/$/, '').split('/')) {
    found = store.items.find((i) => i.parentId === parentId && i.name === name);
    if (!found) {
      throw new Error(`No item at ${path}`);
    }
    parentId = found.id;
  }
  return found!.id;
}

export function contentOf(itemId: string) {
  return store.contents.get(itemId);
}

export function items() {
  return store.items;
}

export function saves() {
  return store.saves;
}

function removeSubtree(itemId: string) {
  for (const child of store.items.filter((i) => i.parentId === itemId)) {
    removeSubtree(child.id);
  }
  store.items = store.items.filter((i) => i.id !== itemId);
  store.contents.delete(itemId);
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
    400: 'invalid_content',
    403: 'forbidden',
    404: 'item_not_found',
    413: 'content_too_large',
    500: 'internal_error',
    503: 'service_unavailable',
  };
  return problem(failure, codes[failure] ?? 'internal_error', 'Failed.');
}

const canEdit = () => store.role === 'owner' || store.role === 'editor';

function siblings(parentId: string | null) {
  return store.items.filter((i) => i.parentId === parentId);
}

function isDescendant(ancestorId: string, itemId: string): boolean {
  for (let current = store.items.find((i) => i.id === itemId); current?.parentId; ) {
    if (current.parentId === ancestorId) {
      return true;
    }
    current = store.items.find((i) => i.id === current!.parentId);
  }
  return false;
}

function nameProblem(message: string) {
  if (message.startsWith('An item')) {
    return problem(409, 'name_taken', message);
  }
  if (message.startsWith('Supported')) {
    return problem(400, 'unsupported_extension', message);
  }
  return HttpResponse.json(
    { title: 'The name is not allowed.', detail: message, status: 400, code: 'invalid_name', correlationId: 'corr-400' },
    { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

const forbidden = () => problem(403, 'forbidden', "You don't have permission to do that.");
const itemNotFound = () => problem(404, 'item_not_found', 'File or folder not found.');

export const fileHandlers = [
  http.get('*/api/workspaces/:ws', ({ params }) =>
    HttpResponse.json(workspaceDetails({ id: String(params.ws), name: 'Demo', myRole: store.role })),
  ),

  http.get('*/api/workspaces/:ws/tree', () => takeFailure('tree') ?? HttpResponse.json({ items: store.items, limits: LIMITS })),

  http.post('*/api/workspaces/:ws/items', async ({ request }) => {
    const failure = takeFailure('create');
    if (failure) return failure;
    if (!canEdit()) return forbidden();
    const body = (await request.json()) as { parentId: string | null; kind: ItemKind; name: string };
    if (body.parentId && !store.items.some((i) => i.id === body.parentId && i.kind === 'folder')) {
      return problem(404, 'parent_not_found', 'The target folder was not found.');
    }
    const message = validateName(body.name, body.kind, siblings(body.parentId));
    if (message) return nameProblem(message);
    const created: Item = { id: newId(), parentId: body.parentId, kind: body.kind, name: body.name, createdAt: now, updatedAt: now };
    store.items.push(created);
    if (created.kind === 'file') {
      store.contents.set(created.id, { text: '', revision: 1 });
    }
    return HttpResponse.json(created, { status: 201 });
  }),

  http.post('*/api/workspaces/:ws/items/:id/rename', async ({ params, request }) => {
    const failure = takeFailure('rename');
    if (failure) return failure;
    if (!canEdit()) return forbidden();
    const target = store.items.find((i) => i.id === params.id);
    if (!target) return itemNotFound();
    const { name } = (await request.json()) as { name: string };
    const message = validateName(name, target.kind, siblings(target.parentId), target.id);
    if (message) return nameProblem(message);
    target.name = name;
    return HttpResponse.json(target);
  }),

  http.post('*/api/workspaces/:ws/items/:id/move', async ({ params, request }) => {
    const failure = takeFailure('move');
    if (failure) return failure;
    if (!canEdit()) return forbidden();
    const target = store.items.find((i) => i.id === params.id);
    if (!target) return itemNotFound();
    const { parentId } = (await request.json()) as { parentId: string | null };
    if (parentId && !store.items.some((i) => i.id === parentId && i.kind === 'folder')) {
      return problem(404, 'parent_not_found', 'The target folder was not found.');
    }
    if (parentId === target.id || (parentId && isDescendant(target.id, parentId))) {
      return problem(400, 'invalid_move', 'A folder cannot be moved into itself or one of its folders.');
    }
    if (validateName(target.name, target.kind, siblings(parentId), target.id) !== null) {
      return problem(409, 'name_taken', 'An item with this name already exists here.');
    }
    target.parentId = parentId;
    return HttpResponse.json(target);
  }),

  http.delete('*/api/workspaces/:ws/items/:id', ({ params }) => {
    const failure = takeFailure('delete');
    if (failure) return failure;
    if (store.role !== 'owner') return forbidden();
    if (!store.items.some((i) => i.id === params.id)) return itemNotFound();
    removeSubtree(String(params.id));
    return new HttpResponse(null, { status: 204 });
  }),

  http.get('*/api/workspaces/:ws/files/:id/content', ({ params }) => {
    const failure = takeFailure('content');
    if (failure) return failure;
    const content = store.contents.get(String(params.id));
    if (!content) return itemNotFound();
    return new HttpResponse(content.text, {
      headers: { 'Content-Type': 'text/plain; charset=utf-8', ETag: `"${content.revision}"`, 'Cache-Control': 'no-store' },
    });
  }),

  http.put('*/api/workspaces/:ws/files/:id/content', async ({ params, request }) => {
    const itemId = String(params.id);
    const text = await request.text();
    const revision = Number(request.headers.get('If-Match')?.replace(/"/g, ''));
    store.saves.push({ itemId, revision, text });
    const failure = takeFailure('save');
    if (failure) return failure;
    if (!canEdit()) return forbidden();
    const content = store.contents.get(itemId);
    if (!content) return itemNotFound();
    if (!request.headers.get('If-Match')) return problem(428, 'revision_required', 'State the revision.');
    if (new TextEncoder().encode(text).length > MAX_CONTENT_BYTES) {
      return problem(413, 'content_too_large', 'The file is larger than 1 MiB.');
    }
    if (revision !== content.revision) {
      return HttpResponse.json(
        { title: 'Someone else saved a newer version.', status: 412, code: 'revision_conflict', correlationId: 'corr-412', currentRevision: content.revision },
        { status: 412, headers: { 'Content-Type': 'application/problem+json', ETag: `"${content.revision}"` } },
      );
    }
    const next = { text, revision: content.revision + 1 };
    store.contents.set(itemId, next);
    return HttpResponse.json({ revision: next.revision, sizeBytes: new TextEncoder().encode(text).length, updatedAt: now });
  }),
];
