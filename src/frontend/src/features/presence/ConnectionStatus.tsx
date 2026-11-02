import { useEffect, useState } from 'react';
import { useAppSelector, useAppStore } from '../../app/hooks';
import { getPresenceClient } from './presenceClient';
import { selectPresence } from './presenceSlice';

const RESTORED_MS = 4_000;

/** The live connection's state, quiet while all is well (spec 005 FR-031; contracts/ui-presence.md). */
export function ConnectionStatus() {
  const store = useAppStore();
  const { status, restored } = useAppSelector(selectPresence);
  // "Connection restored." shows for each recovery until its timer hides it.
  const [hiddenUpTo, setHiddenUpTo] = useState(0);
  const showRestored = restored > hiddenUpTo;

  useEffect(() => {
    if (restored === 0) {
      return;
    }
    const timer = window.setTimeout(() => setHiddenUpTo(restored), RESTORED_MS);
    return () => window.clearTimeout(timer);
  }, [restored]);

  let content = null;
  if (status === 'reconnecting') {
    content = (
      <>
        <span className="spinner" aria-hidden="true" />
        Reconnecting to DevSync…
      </>
    );
  } else if (status === 'failed') {
    content = (
      <>
        Live presence unavailable
        <button type="button" className="btn-ghost" onClick={() => getPresenceClient(store).retry()}>
          Retry
        </button>
      </>
    );
  } else if (status === 'connected' && showRestored) {
    content = 'Connection restored.';
  }

  return (
    <span role="status" aria-live="polite" className={`connection-status${status === 'failed' ? ' failed' : ''}`}>
      {content}
    </span>
  );
}
