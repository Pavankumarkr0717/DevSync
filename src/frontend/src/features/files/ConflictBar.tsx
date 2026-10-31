import type { FileEditState } from './editorSlice';
import { FAILURE_TEXT } from './fileMessages';

interface Props {
  state: FileEditState;
  onReload: () => void;
  onOverwrite: () => void;
  onCopy: () => void;
}

/**
 * Shown when a save could not be applied: someone saved a newer version (the person chooses), or the
 * file is gone or no longer editable (the person can still copy their text). Spec 003 FR-023.
 */
export function ConflictBar({ state, onReload, onOverwrite, onCopy }: Props) {
  if (state.status !== 'failed' || !state.reason || !['conflict', 'deleted', 'forbidden'].includes(state.reason)) {
    return null;
  }

  return (
    <div className="notice conflict-bar" data-testid="conflict-bar" role="alert">
      <span>{FAILURE_TEXT[state.reason]}.</span>
      {state.reason === 'conflict' && (
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
  );
}
