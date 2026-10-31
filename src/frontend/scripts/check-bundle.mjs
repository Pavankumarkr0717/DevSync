// Checks the production bundle (run after `npm run build`), spec 003 research R8 and spec 004 research R2:
// 1. Monaco and React Flow are in lazy chunks, not in the entry chunk that every page loads.
// 2. Nothing is loaded from a CDN. The only CDN text allowed is @monaco-editor/loader's unused
//    default path, which is never fetched because monacoSetup passes the bundled instance.
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

const root = new URL('..', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');
const dist = join(root, 'dist');
const assets = join(dist, 'assets');
const failures = [];

const entry = /src="\/assets\/([^"]+\.js)"/.exec(readFileSync(join(dist, 'index.html'), 'utf8'))?.[1];
if (!entry) {
  failures.push('Could not find the entry script in dist/index.html.');
}

// A command id that survives minification and only exists in Monaco's editor code.
const monacoMarker = 'editor.action.';
const loaderDefault = /paths:\{vs:`https:\/\/cdn\.jsdelivr\.net\/npm\/monaco-editor@[\d.]+\/min\/vs`\}/g;
// A class name that survives minification and only exists in React Flow's renderer.
const reactFlowMarker = 'react-flow__renderer';
const cdnHosts = /cdn\.jsdelivr\.net|unpkg\.com|cdnjs\.cloudflare\.com|esm\.sh|skypack\.dev/;
let monacoChunk = null;
let reactFlowChunk = null;

for (const file of readdirSync(assets).filter((f) => f.endsWith('.js'))) {
  const code = readFileSync(join(assets, file), 'utf8');
  if (code.includes(monacoMarker)) {
    if (file === entry) {
      failures.push(`Monaco is in the entry chunk ${file}; it must be lazy-loaded.`);
    } else {
      monacoChunk = file;
    }
  }
  if (code.includes(reactFlowMarker)) {
    if (file === entry) {
      failures.push(`React Flow is in the entry chunk ${file}; it must be lazy-loaded.`);
    } else {
      reactFlowChunk = file;
    }
  }
  if (cdnHosts.test(code.replace(loaderDefault, ''))) {
    failures.push(`${file} references a CDN.`);
  }
}

if (!monacoChunk) {
  failures.push('No lazy chunk contains Monaco.');
}
if (!reactFlowChunk) {
  failures.push('No lazy chunk contains React Flow.');
}

const setup = readFileSync(join(root, 'src', 'features', 'files', 'monacoSetup.ts'), 'utf8');
if (!/loader\.config\(\{\s*monaco\b/.test(setup)) {
  failures.push('monacoSetup.ts must call loader.config({ monaco }) so the CDN default is never used.');
}

if (failures.length > 0) {
  console.error(failures.map((f) => `✗ ${f}`).join('\n'));
  process.exit(1);
}
console.log(`✓ Monaco (${monacoChunk}) and React Flow (${reactFlowChunk}) are lazy-loaded; entry ${entry}; no CDN references.`);
