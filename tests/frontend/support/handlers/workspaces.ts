import { http, HttpResponse } from 'msw';
import type {
  Problem,
  WorkspaceDetails,
  WorkspaceInvitation,
  WorkspacePage,
  WorkspaceSummary,
} from '@/shared/api/types';
import { USERS } from './users';

export function problem(status: number, code: Problem['code'], title: string, errors?: Problem['errors']) {
  const body: Problem = { title, status, code, correlationId: `corr-${status}`, ...(errors ? { errors } : {}) };
  return HttpResponse.json(body, { status, headers: { 'Content-Type': 'application/problem+json' } });
}

export function workspaceDetails(overrides: Partial<WorkspaceDetails> = {}): WorkspaceDetails {
  const owner = USERS[0];
  return {
    id: '0193aaaa-0000-7000-8000-000000000001',
    name: 'Interview Prep',
    description: 'Mock interviews',
    owner,
    createdAt: '2026-10-29T10:00:00Z',
    updatedAt: '2026-10-29T10:00:00Z',
    members: [{ userId: owner.id, displayName: owner.displayName, role: 'owner', joinedAt: '2026-10-29T10:00:00Z' }],
    myRole: 'owner',
    ...overrides,
  };
}

/** A workspace owned by Ana with Ben as editor and Cy as viewer (emails present only for the owner view). */
export function teamWorkspace(myRole: WorkspaceDetails['myRole'] = 'owner'): WorkspaceDetails {
  const withEmail = myRole === 'owner';
  const member = (i: number, role: WorkspaceDetails['myRole']) => ({
    userId: USERS[i].id,
    displayName: USERS[i].displayName,
    role,
    joinedAt: '2026-10-29T10:00:00Z',
    ...(withEmail ? { email: `${USERS[i].displayName.toLowerCase()}@example.com` } : {}),
  });
  return workspaceDetails({ myRole, members: [member(0, 'owner'), member(1, 'editor'), member(2, 'viewer')] });
}

export function invitation(overrides: Partial<WorkspaceInvitation> = {}): WorkspaceInvitation {
  return {
    id: '0193cccc-0000-7000-8000-000000000001',
    email: 'dee@example.com',
    role: 'editor',
    invitedBy: USERS[0],
    createdAt: '2026-10-30T08:00:00Z',
    expiresAt: '2026-11-06T08:00:00Z',
    ...overrides,
  };
}

export function summaries(count: number, prefix = 'Workspace'): WorkspaceSummary[] {
  return Array.from({ length: count }, (_, i) => ({
    id: `0193bbbb-0000-7000-8000-${String(i + 1).padStart(12, '0')}`,
    name: `${prefix} ${i + 1}`,
    role: 'owner',
    createdAt: '2026-10-29T10:00:00Z',
  }));
}

export function pageOf(items: WorkspaceSummary[], page = 1, pageSize = 20, totalCount = items.length): WorkspacePage {
  return { items, page, pageSize, totalCount };
}

export const listHandler = (respond: (request: Request) => Response | Promise<Response>) =>
  http.get('*/api/workspaces', ({ request }) => respond(request));

export const detailsHandler = (respond: (id: string, request: Request) => Response | Promise<Response>) =>
  http.get('*/api/workspaces/:id', ({ params, request }) => respond(String(params.id), request));

export const createHandler = (respond: (body: unknown, request: Request) => Response | Promise<Response>) =>
  http.post('*/api/workspaces', async ({ request }) => respond(await request.json(), request));

export const updateHandler = (respond: (body: unknown, request: Request) => Response | Promise<Response>) =>
  http.put('*/api/workspaces/:id', async ({ request }) => respond(await request.json(), request));

export const workspaceInvitationsHandler = (invitations: WorkspaceInvitation[]) =>
  http.get('*/api/workspaces/:id/invitations', () => HttpResponse.json(invitations));
