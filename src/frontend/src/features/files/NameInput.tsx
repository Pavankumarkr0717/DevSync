import { useId, useState, type KeyboardEvent } from 'react';
import type { ItemKind } from '../../shared/api/types';
import { validateName } from './fileRules';

interface Props {
  label: string;
  kind: ItemKind;
  initialValue?: string;
  siblings: ReadonlyArray<{ id: string; name: string }>;
  selfId?: string;
  /** Resolves with an error message to show, or null when done. */
  onSubmit: (name: string) => Promise<string | null>;
  onCancel: () => void;
}

/** Inline name entry for create and rename: validates as you type, Enter submits, Escape cancels. */
export function NameInput({ label, kind, initialValue = '', siblings, selfId, onSubmit, onCancel }: Props) {
  const [value, setValue] = useState(initialValue);
  const [serverError, setServerError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const errorId = useId();

  const clientError = value.length > 0 ? validateName(value, kind, siblings, selfId) : null;
  const error = clientError ?? serverError;

  async function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    event.stopPropagation();
    if (event.key === 'Escape') {
      onCancel();
      return;
    }
    if (event.key !== 'Enter' || busy) {
      return;
    }
    event.preventDefault();
    const problem = validateName(value, kind, siblings, selfId);
    if (problem) {
      setServerError(problem);
      return;
    }
    setBusy(true);
    const result = await onSubmit(value);
    setBusy(false);
    setServerError(result);
  }

  return (
    <div className="name-input">
      <input
        aria-label={label}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        value={value}
        disabled={busy}
        autoFocus
        onChange={(event) => {
          setValue(event.target.value);
          setServerError(null);
        }}
        onKeyDown={(event) => void handleKeyDown(event)}
      />
      {error && (
        <p id={errorId} role="alert" className="field-error">
          {error}
        </p>
      )}
    </div>
  );
}
