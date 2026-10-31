import { useState } from 'react';
import type { Draft } from '../../shared/editing/drafts';
import type { DiagramEditorState } from './diagramEditorSlice';
import { FAILURE_TEXT, statusText } from './saveStatus';

interface Props {
  editor: DiagramEditorState;
  draft: Draft | null;
  onRetry: () => void;
  onReload: () => void;
  onOverwrite: () => void;
  onCopy: () => void;
  onRestoreDraft: () => void;
  onDiscardDraft: () => void;
}

/** Retry, conflict, invalid-problem and draft bars under the toolbar (contracts/ui-routes.md). */
export function DiagramStatusBars({ editor, draft, onRetry, onReload, onOverwrite, onCopy, onRestoreDraft, onDiscardDraft }: Props) {
  const [showDetails, setShowDetails] = useState(false);
  const failed = editor.status === 'failed' ? editor.reason : undefined;

  return (
    <>
      {failed && ['conflict', 'deleted', 'forbidden'].includes(failed) && (
        <div className="notice conflict-bar" role="alert" data-testid="conflict-bar">
          <span>{FAILURE_TEXT[failed]}.</span>
          {failed === 'conflict' && (
            <>
              <button type="button" onClick={onReload}>
                Reload latest
              </button>
              <button type="button" onClick={onOverwrite}>
                Overwrite with mine
              </button>
            </>
          )}
          <button type="button" onClick={onCopy}>
            Copy mine
          </button>
        </div>
      )}
      {failed && ['offline', 'tooLarge', 'invalidContent'].includes(failed) && (
        <div className="notice conflict-bar" role="alert" data-testid="save-problem">
          <span>{statusText(editor, false)}</span>
          {editor.problems.length > 1 && (
            <button type="button" aria-expanded={showDetails} onClick={() => setShowDetails((v) => !v)}>
              Details
            </button>
          )}
          <button type="button" onClick={onRetry}>
            Retry
          </button>
          {showDetails && (
            <ul className="problem-list">
              {editor.problems.map((p, i) => (
                <li key={i}>
                  {p.message} <span className="meta">{p.path}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
      {draft && (
        <div className="notice draft-bar" data-testid="draft-bar" role="region" aria-label="Unsaved draft">
          <span>You have unsaved changes from {new Date(draft.savedAt).toLocaleString()}.</span>
          <button type="button" onClick={onRestoreDraft}>
            Restore
          </button>
          <button type="button" onClick={onDiscardDraft}>
            Discard
          </button>
        </div>
      )}
    </>
  );
}
