import { useId } from 'react';

interface Props {
  title: string;
  message: string;
  confirmLabel: string;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/** Asks before a destructive action. Cancel has focus, and Escape cancels. */
export function ConfirmDialog({ title, message, confirmLabel, busy = false, onConfirm, onCancel }: Props) {
  const textId = useId();

  return (
    <div className="dialog-backdrop">
      <div
        role="alertdialog"
        aria-modal="true"
        aria-label={title}
        aria-describedby={textId}
        className="dialog"
        onKeyDown={(event) => {
          if (event.key === 'Escape') {
            onCancel();
          }
        }}
      >
        <h2>{title}</h2>
        <p id={textId}>{message}</p>
        <div className="dialog-actions">
          <button type="button" autoFocus onClick={onCancel}>
            Cancel
          </button>
          <button type="button" className="danger" disabled={busy} onClick={onConfirm}>
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
