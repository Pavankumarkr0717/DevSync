import { useId } from 'react';
import type { Item } from '../../shared/api/types';

interface Props {
  item: Item;
  /** Files and folders inside the item. */
  inside: number;
  error: string | null;
  busy: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/** Confirms a permanent delete, saying how much goes with it (spec 003 FR-015). */
export function DeleteDialog({ item, inside, error, busy, onConfirm, onCancel }: Props) {
  const textId = useId();
  const what = inside > 0 ? `${item.name} and ${inside} item${inside === 1 ? '' : 's'} inside` : item.name;

  return (
    <div className="dialog-backdrop">
      <div
        role="alertdialog"
        aria-modal="true"
        aria-describedby={textId}
        aria-label={`Delete ${item.name}`}
        className="dialog"
        onKeyDown={(event) => {
          if (event.key === 'Escape') {
            onCancel();
          }
        }}
      >
        <p id={textId}>Delete {what}? This can&apos;t be undone.</p>
        {error && (
          <p role="alert" className="field-error">
            {error}
          </p>
        )}
        <div className="dialog-actions">
          <button type="button" autoFocus onClick={onCancel}>
            Cancel
          </button>
          <button type="button" className="danger" disabled={busy} onClick={onConfirm}>
            Delete
          </button>
        </div>
      </div>
    </div>
  );
}
