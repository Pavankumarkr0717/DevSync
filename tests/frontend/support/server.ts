import { setupServer } from 'msw/node';
import { defaultAuthHandlers } from './handlers/auth';

/** Mock HTTP server for component tests; tests add handlers with server.use(). */
export const server = setupServer(...defaultAuthHandlers);
