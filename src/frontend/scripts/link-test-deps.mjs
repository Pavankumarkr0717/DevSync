// Frontend tests live in <repo>/tests/frontend, outside this npm package. Node, Vite and
// TypeScript resolve packages by walking up from each file, so we link
// tests/frontend/node_modules -> src/frontend/node_modules. Runs as npm "postinstall".
// Skipped when the tests folder is absent (for example inside the Docker build context).
import { existsSync, lstatSync, rmSync, symlinkSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const frontendRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const testsRoot = resolve(frontendRoot, '../../tests/frontend');
const target = join(frontendRoot, 'node_modules');
const link = join(testsRoot, 'node_modules');

if (!existsSync(testsRoot)) {
  process.exit(0);
}

try {
  if (lstatSync(link, { throwIfNoEntry: false })) {
    rmSync(link, { recursive: false, force: true });
  }
} catch {
  // Nothing to remove.
}

// 'junction' needs no admin rights on Windows and is ignored (plain dir symlink) elsewhere.
symlinkSync(target, link, 'junction');
console.log(`Linked ${link} -> ${target}`);
