import type { FileEditState, SaveFailure } from './editorSlice';
import { FAILURE_TEXT } from './fileMessages';

const RETRYABLE: SaveFailure[] = ['offline', 'tooLarge', 'invalidContent'];

/** The active file's save state (spec 003 FR-022). */
export function SaveStatus({ state, readOnly, onRetry }: { state: FileEditState | undefined; readOnly: boolean; onRetry: () => void }) {
  let text = 'Saved';
  if (readOnly && state?.reason !== 'forbidden') {
    text = 'Read only';
  } else if (state?.status === 'dirty') {
    text = 'Unsaved changes';
  } else if (state?.status === 'saving') {
    text = 'Saving…';
  } else if (state?.status === 'failed' && state.reason) {
    text = `Save failed: ${FAILURE_TEXT[state.reason]}`;
  }

  return (
    <div className="save-status" data-testid="save-status" role="status" aria-live="polite">
      <span className={state?.status === 'failed' ? 'error' : 'meta'}>{text}</span>
      {state?.status === 'failed' && state.reason && RETRYABLE.includes(state.reason) && (
        <button type="button" onClick={onRetry}>
          Retry
        </button>
      )}
    </div>
  );
}
