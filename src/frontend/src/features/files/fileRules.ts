import type { ItemKind } from '../../shared/api/types';

// A client copy of the server's name and extension rules (spec 003 FR-005, FR-010), for instant
// feedback only. The server always re-checks; its messages are the same.

const LANGUAGES: Record<string, string> = {
  cs: 'csharp',
  js: 'javascript',
  jsx: 'javascript',
  ts: 'typescript',
  tsx: 'typescript',
  json: 'json',
  html: 'html',
  css: 'css',
  py: 'python',
  java: 'java',
  md: 'markdown',
};

export const MAX_NAME_LENGTH = 255;
export const UNSUPPORTED_EXTENSION_TEXT = 'Supported: .cs .js .ts .tsx .jsx .json .html .css .py .java .md';
export const NAME_TAKEN_TEXT = 'An item with this name already exists here.';
const FORBIDDEN = '/\\<>:"|?*';

function extension(name: string): string | null {
  const dot = name.lastIndexOf('.');
  return dot >= 0 && dot < name.length - 1 ? name.slice(dot + 1).toLowerCase() : null;
}

/** The editor language for a file name; plain text when the extension is unknown. */
export function languageFor(name: string): string {
  const ext = extension(name);
  return (ext && LANGUAGES[ext]) || 'plaintext';
}

/** The comparison key for sibling names (case-insensitive, like the server). */
export function normalizedName(name: string): string {
  return name.toUpperCase();
}

/** Why <paramref>name</paramref> is not allowed, or null when it is. */
export function validateName(
  name: string,
  kind: ItemKind,
  siblings: ReadonlyArray<{ id: string; name: string }>,
  selfId?: string,
): string | null {
  if (name.length === 0) {
    return 'Name is required.';
  }
  if ([...name].length > MAX_NAME_LENGTH) {
    return `Name must be at most ${MAX_NAME_LENGTH} characters.`;
  }
  if (name === '.' || name === '..') {
    return 'Name cannot be "." or "..".';
  }
  const forbidden = [...name].find((c) => FORBIDDEN.includes(c));
  if (forbidden) {
    return `Name cannot contain "${forbidden}". Not allowed: / \\ < > : " | ? *`;
  }
  // eslint-disable-next-line no-control-regex
  if (/[\u0000-\u001f\u007f]/.test(name)) {
    return 'Name cannot contain control characters.';
  }
  if (name.startsWith(' ') || name.endsWith(' ')) {
    return 'Name cannot start or end with a space.';
  }
  if (name.endsWith('.')) {
    return 'Name cannot end with a dot.';
  }
  if (kind === 'file') {
    const ext = extension(name);
    if (!ext || !(ext in LANGUAGES)) {
      return UNSUPPORTED_EXTENSION_TEXT;
    }
  }
  const key = normalizedName(name);
  if (siblings.some((s) => s.id !== selfId && normalizedName(s.name) === key)) {
    return NAME_TAKEN_TEXT;
  }
  return null;
}
