/** Display details for the supported file types (spec 003 FR-010): the explorer badge and the language name. */
export interface FileTypeInfo {
  /** Short text on the explorer badge. */
  badge: string;
  /** Language name for the editor status bar. */
  language: string;
  /** design-system pastel for the badge */
  pastel: 'blue' | 'purple' | 'green' | 'yellow' | 'pink' | 'orange';
}

const TYPES: Record<string, FileTypeInfo> = {
  cs: { badge: 'C#', language: 'C#', pastel: 'purple' },
  ts: { badge: 'TS', language: 'TypeScript', pastel: 'blue' },
  tsx: { badge: 'TSX', language: 'TypeScript React', pastel: 'blue' },
  js: { badge: 'JS', language: 'JavaScript', pastel: 'yellow' },
  jsx: { badge: 'JSX', language: 'JavaScript React', pastel: 'yellow' },
  json: { badge: '{ }', language: 'JSON', pastel: 'orange' },
  html: { badge: '<>', language: 'HTML', pastel: 'orange' },
  css: { badge: '#', language: 'CSS', pastel: 'blue' },
  py: { badge: 'PY', language: 'Python', pastel: 'green' },
  java: { badge: 'J', language: 'Java', pastel: 'pink' },
  md: { badge: 'MD', language: 'Markdown', pastel: 'purple' },
};

const PLAIN: FileTypeInfo = { badge: '·', language: 'Plain text', pastel: 'blue' };

export function fileTypeFor(name: string): FileTypeInfo {
  const dot = name.lastIndexOf('.');
  const ext = dot > 0 ? name.slice(dot + 1).toLowerCase() : '';
  return TYPES[ext] ?? PLAIN;
}
