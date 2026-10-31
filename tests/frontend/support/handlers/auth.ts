import { http, HttpResponse } from 'msw';
import type { ReceivedInvitation } from '@/shared/api/types';
import { problem } from './workspaces';

export const refreshHandler = (respond: (request: Request) => Response | Promise<Response>) =>
  http.post('*/api/auth/refresh', ({ request }) => respond(request));

export const loginHandler = (respond: (body: unknown, request: Request) => Response | Promise<Response>) =>
  http.post('*/api/auth/login', async ({ request }) => respond(await request.json(), request));

export const registerHandler = (respond: (body: unknown, request: Request) => Response | Promise<Response>) =>
  http.post('*/api/auth/register', async ({ request }) => respond(await request.json(), request));

export const myInvitationsHandler = (invitations: ReceivedInvitation[]) =>
  http.get('*/api/invitations', () => HttpResponse.json(invitations));

/** Defaults every test gets: no session to restore, nothing in the invitations inbox, sign-out works. */
export const defaultAuthHandlers = [
  refreshHandler(() => problem(401, 'session_ended', 'Your session has ended. Sign in again.')),
  myInvitationsHandler([]),
  http.post('*/api/auth/logout', () => new HttpResponse(null, { status: 204 })),
  http.post('*/api/auth/logout-all', () => new HttpResponse(null, { status: 204 })),
];
