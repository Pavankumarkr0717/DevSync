import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The browser always calls same-origin /api and /health; in development Vite forwards them to the API.
const apiTarget = process.env.DEVSYNC_API_URL ?? 'http://localhost:5080';

// Component tests live outside this package, in <repo>/tests/frontend.
const testsDir = fileURLToPath(new URL('../../tests/frontend', import.meta.url));

export default defineConfig({
  plugins: [react()],
  resolve: {
    // Lets the external tests import app code as '@/...'.
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    port: 5173,
    fs: { allow: ['.', testsDir] },
    proxy: {
      '/api': apiTarget,
      '/health': apiTarget,
    },
  },
  test: {
    dir: testsDir,
    environment: 'jsdom',
    setupFiles: ['../../tests/frontend/support/setup.ts'],
    restoreMocks: true,
  },
});
