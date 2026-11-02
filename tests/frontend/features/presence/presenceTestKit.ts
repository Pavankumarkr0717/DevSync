import { act, screen } from '@testing-library/react';
import { diagramHandlers } from '../../support/handlers/diagrams';
import { fileHandlers } from '../../support/handlers/files';
import { workspaceInvitationsHandler } from '../../support/handlers/workspaces';
import { fakeHub } from '../../support/fakeHub';
import { server } from '../../support/server';

export const WS_A = '0193aaaa-0000-7000-8000-000000000001';
export const WS_B = '0193aaaa-0000-7000-8000-0000000000b2';

/** Every workspace page renders: details, tree, diagrams, invitations. */
export function useWorkspaceHandlers() {
  server.use(...diagramHandlers, ...fileHandlers, workspaceInvitationsHandler([]));
}

/** The presence button, once the join has been answered. */
export const presenceButton = () => screen.findByRole('button', { name: /online — show who/ });

/** Server → client event on the tab's current connection, inside act. */
export async function serverSends(event: string, payload: unknown) {
  await act(async () => fakeHub.last.emit(event, payload));
}
