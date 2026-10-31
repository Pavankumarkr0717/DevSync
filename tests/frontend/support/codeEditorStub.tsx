import { useState } from 'react';
import type { CodeEditorProps } from '@/features/files/CodeEditor';
import { editorModels } from './editorModels';

// Monaco cannot run in jsdom. This stand-in keeps one value per file, as Monaco keeps one model per
// file, and honours the same props, so component tests exercise the real page logic around it.

const models = editorModels;

export default function CodeEditorStub({ workspaceId, itemId, language, value, valueVersion, readOnly, onChange }: CodeEditorProps) {
  const key = `${workspaceId}/${itemId}`;
  if (!models.has(key)) {
    models.set(key, value);
  }
  const [text, setText] = useState(models.get(key)!);
  const [appliedVersion, setAppliedVersion] = useState(valueVersion);

  // Like Monaco's setValue: a new valueVersion replaces the text.
  if (valueVersion !== appliedVersion) {
    setAppliedVersion(valueVersion);
    models.set(key, value);
    setText(value);
  }

  return (
    <textarea
      data-testid="code-editor"
      aria-label="Code editor"
      data-language={language}
      readOnly={readOnly}
      value={text}
      onChange={(event) => {
        models.set(key, event.target.value);
        setText(event.target.value);
        onChange(event.target.value);
      }}
    />
  );
}
