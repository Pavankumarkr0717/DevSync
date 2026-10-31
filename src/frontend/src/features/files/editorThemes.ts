import type { Monaco } from '@monaco-editor/react';

/**
 * Monaco themes matching DevSync's surfaces (docs/design/design-system.md): the stock vs/vs-dark
 * token colours on the app's own backgrounds, so the editor sits on the navy sheet in dark mode.
 */
export function defineEditorThemes(monaco: Monaco) {
  monaco.editor.defineTheme('devsync-dark', {
    base: 'vs-dark',
    inherit: true,
    rules: [],
    colors: {
      'editor.background': '#121826',
      'editorGutter.background': '#121826',
      'editor.lineHighlightBackground': '#1a2232',
      'editor.lineHighlightBorder': '#00000000',
      'editorLineNumber.foreground': '#4b5872',
      'editorLineNumber.activeForeground': '#c3cede',
      'minimap.background': '#0f1520',
      'editorWidget.background': '#1a2232',
    },
  });
  monaco.editor.defineTheme('devsync-light', {
    base: 'vs',
    inherit: true,
    rules: [],
    colors: {
      'editor.background': '#ffffff',
      'editor.lineHighlightBackground': '#f4f6fa',
      'editor.lineHighlightBorder': '#00000000',
      'editorLineNumber.foreground': '#9aa3b2',
      'minimap.background': '#f9fafc',
    },
  });
}
