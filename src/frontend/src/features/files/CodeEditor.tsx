import Editor, { type OnMount } from '@monaco-editor/react';
import { useEffect, useRef } from 'react';
import { useTheme } from '../../shared/theme/useTheme';
import { defineEditorThemes } from './editorThemes';
import './monacoSetup';

export interface CodeEditorProps {
  workspaceId: string;
  itemId: string;
  /** Monaco language id (from the file extension). */
  language: string;
  /** The text to show when the file is first opened, or after <see cref="valueVersion"/> changes. */
  value: string;
  /** Bump to replace the editor's text with <paramref>value</paramref> (e.g. "Reload latest"). */
  valueVersion: number;
  readOnly: boolean;
  onChange: (text: string) => void;
}

/**
 * The only module that imports Monaco (lazy-loaded). Each file gets its own model, keyed by a
 * workspace/item URI, so its undo history, cursor and scroll survive tab switches (spec 003 FR-032).
 */
export default function CodeEditor({ workspaceId, itemId, language, value, valueVersion, readOnly, onChange }: CodeEditorProps) {
  const editorRef = useRef<Parameters<OnMount>[0] | null>(null);
  const appliedVersion = useRef(valueVersion);
  const dark = useTheme() === 'dark';

  useEffect(() => {
    if (valueVersion !== appliedVersion.current) {
      appliedVersion.current = valueVersion;
      editorRef.current?.getModel()?.setValue(value);
    }
  }, [value, valueVersion]);

  return (
    <div className="code-editor" data-testid="code-editor">
      <Editor
        path={`devsync://${workspaceId}/${itemId}`}
        defaultLanguage={language}
        language={language}
        defaultValue={value}
        saveViewState
        theme={dark ? 'devsync-dark' : 'devsync-light'}
        beforeMount={defineEditorThemes}
        options={{
          readOnly,
          domReadOnly: readOnly,
          minimap: { enabled: true, renderCharacters: false },
          automaticLayout: true,
          fixedOverflowWidgets: true,
          fontFamily: "'JetBrains Mono', Consolas, 'Courier New', monospace",
          fontSize: 13,
          fontLigatures: true,
        }}
        onMount={(editor) => {
          editorRef.current = editor;
        }}
        onChange={(text) => onChange(text ?? '')}
      />
    </div>
  );
}
