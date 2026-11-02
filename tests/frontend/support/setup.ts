import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, beforeEach, vi } from 'vitest';
import { resetEditorModels } from './editorModels';
import { resetFakeHub } from './fakeHub';
import './reactFlowEnv';
import { server } from './server';

// Monaco cannot run in jsdom; the files page gets a textarea with the same props instead.
vi.mock('@/features/files/CodeEditor', () => import('./codeEditorStub'));

beforeAll(() => server.listen({ onUnhandledFrame: 'error' }));
// Workspace pages open a real-time connection (spec 005); tests get an in-memory hub instead.
beforeEach(() => {
  resetFakeHub();
});
afterEach(() => {
  cleanup();
  server.resetHandlers();
  window.localStorage.clear();
  resetEditorModels();
});
afterAll(() => server.close());
