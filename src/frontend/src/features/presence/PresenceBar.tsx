import { useEffect, useId, useRef, useState } from 'react';
import { useAppSelector } from '../../app/hooks';
import { Avatar } from '../../shared/ui/Avatar';
import { selectCurrentUser } from '../auth/authSlice';
import { ConnectionStatus } from './ConnectionStatus';
import { selectPresence } from './presenceSlice';
import type { PresenceUser } from './presenceTypes';

const MAX_AVATARS = 4;

/** Self first, then everyone else by name. */
function ordered(users: PresenceUser[], selfId: string | undefined) {
  return [...users].sort((a, b) =>
    a.userId === selfId ? -1 : b.userId === selfId ? 1 : a.displayName.localeCompare(b.displayName),
  );
}

/**
 * Who is online in this workspace (spec 005 FR-028, FR-029; contracts/ui-presence.md): avatars with
 * online dots, a "+N" overflow, the count, and a summary listing every name.
 */
export function PresenceBar() {
  const presence = useAppSelector(selectPresence);
  const self = useAppSelector(selectCurrentUser);
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const popoverId = useId();

  useEffect(() => {
    if (!open) {
      return;
    }
    const onPointer = (e: PointerEvent) => {
      if (!rootRef.current?.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setOpen(false);
        buttonRef.current?.focus();
      }
    };
    document.addEventListener('pointerdown', onPointer);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('pointerdown', onPointer);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  if (!presence.joined || presence.revoked) {
    return (
      <div className="presence">
        <ConnectionStatus />
      </div>
    );
  }

  const users = ordered(presence.users, self?.id);
  const shown = users.slice(0, MAX_AVATARS);
  const extra = users.length - shown.length;
  const stale = presence.status !== 'connected';
  const count = `${users.length} online`;

  return (
    <div className="presence" ref={rootRef}>
      <ConnectionStatus />
      <button
        ref={buttonRef}
        type="button"
        className={`presence-button${stale ? ' stale' : ''}`}
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-controls={open ? popoverId : undefined}
        aria-label={`${count} — show who${stale ? ', may be out of date' : ''}`}
        onClick={() => setOpen((o) => !o)}
      >
        <span className="presence-avatars" aria-hidden="true">
          {shown.map((u) => (
            <span key={u.userId} className="presence-avatar" data-testid="presence-avatar">
              <Avatar id={u.userId} name={u.displayName} size="sm" />
              <span className="presence-dot" />
            </span>
          ))}
          {extra > 0 && <span className="presence-more">+{extra}</span>}
        </span>
        <span className="presence-count" aria-hidden="true">
          {count}
        </span>
      </button>
      {open && (
        <div
          id={popoverId}
          role="dialog"
          aria-label="Online now"
          className="presence-popover"
        >
          <h2 className="panel-title">Online now</h2>
          <ul>
            {users.map((u) => (
              <li key={u.userId}>
                <Avatar id={u.userId} name={u.displayName} size="sm" />
                <span>
                  {u.displayName}
                  {u.userId === self?.id && ' (you)'}
                </span>
              </li>
            ))}
          </ul>
          <button type="button" className="visually-hidden" onClick={() => setOpen(false)}>
            Close
          </button>
        </div>
      )}
    </div>
  );
}
