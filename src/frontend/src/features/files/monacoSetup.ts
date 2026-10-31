import { loader } from '@monaco-editor/react';
import * as monaco from 'monaco-editor/editor/editor.api';
import 'monaco-editor/features/register.all';
import 'monaco-editor/languages/definitions/csharp/register';
import 'monaco-editor/languages/definitions/css/register';
import 'monaco-editor/languages/definitions/html/register';
import 'monaco-editor/languages/definitions/java/register';
import 'monaco-editor/languages/definitions/javascript/register';
import 'monaco-editor/languages/definitions/markdown/register';
import 'monaco-editor/languages/definitions/python/register';
import 'monaco-editor/languages/definitions/typescript/register';
import EditorWorker from 'monaco-editor/editor/editor.worker?worker';

// Monaco for spec 003 (research R8): bundled locally (never loaded from a CDN), with the editor
// core and syntax tokenizers only. The TypeScript/JSON/CSS/HTML language services are left out on
// purpose: they are language servers (out of scope) and would flag every cross-file import as an error.

self.MonacoEnvironment = {
  getWorker: () => new EditorWorker(),
};

// Monaco ships no tokenizer-only JSON definition (its JSON support is a language service), so
// register a minimal one for highlighting.
monaco.languages.register({ id: 'json', extensions: ['.json'], aliases: ['JSON'] });
monaco.languages.setMonarchTokensProvider('json', {
  tokenizer: {
    root: [
      [/"(?:[^"\\]|\\.)*"(?=\s*:)/, 'type'],
      [/"(?:[^"\\]|\\.)*"/, 'string'],
      [/-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?/, 'number'],
      [/\b(?:true|false|null)\b/, 'keyword'],
      [/[{}[\],:]/, 'delimiter'],
    ],
  },
});
monaco.languages.setLanguageConfiguration('json', {
  brackets: [
    ['{', '}'],
    ['[', ']'],
  ],
  autoClosingPairs: [
    { open: '{', close: '}' },
    { open: '[', close: ']' },
    { open: '"', close: '"' },
  ],
});

loader.config({ monaco: monaco as unknown as Parameters<typeof loader.config>[0]['monaco'] });

export { monaco };
