import type { Draft } from '../../shared/editing/drafts';

/** Offers back unsaved text from an earlier visit (spec 003 FR-036). */
export function DraftBar({ draft, onRestore, onDiscard }: { draft: Draft; onRestore: () => void; onDiscard: () => void }) {
  return (
    <div className="notice draft-bar" data-testid="draft-bar" role="region" aria-label="Unsaved draft">
      <span>You have unsaved changes from {new Date(draft.savedAt).toLocaleString()}.</span>
      <button type="button" onClick={onRestore}>
        Restore
      </button>
      <button type="button" onClick={onDiscard}>
        Discard
      </button>
    </div>
  );
}
