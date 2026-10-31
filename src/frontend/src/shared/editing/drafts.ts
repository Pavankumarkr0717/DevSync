import { useEffect, useRef } from 'react';

/**
 * Unsaved-edit drafts in this browser (spec 003 FR-035–FR-037, research R11). A draft is a
 * recovery aid, never authoritative: it is offered back only when it differs from the server copy,
 * and restoring it still goes through the revision check. Stored per user and file; cleared on
 * save, discard, explicit sign-out, or after 7 days. Storage failures are ignored.
 */

export interface Draft {
  text: string;
  baseRevision: number;
  savedAt: string;
}

const PREFIX = 'devsync:draft:';
const MAX_AGE_MS = 7 * 24 * 60 * 60 * 1000;
export const KEEPALIVE_LIMIT_BYTES = 64 * 1024;

const key = (userId: string, itemId: string) => `${PREFIX}${userId}:${itemId}`;

function safely<T>(action: () => T, fallback: T): T {
  try {
    return action();
  } catch {
    return fallback;
  }
}

function draftKeys(): string[] {
  return safely(
    () => Array.from({ length: localStorage.length }, (_, i) => localStorage.key(i)).filter((k): k is string => !!k?.startsWith(PREFIX)),
    [],
  );
}

export function writeDraft(userId: string, itemId: string, draft: Omit<Draft, 'savedAt'>) {
  safely(() => localStorage.setItem(key(userId, itemId), JSON.stringify({ ...draft, savedAt: new Date().toISOString() })), undefined);
}

export function readDraft(userId: string, itemId: string): Draft | null {
  return safely(() => {
    const raw = localStorage.getItem(key(userId, itemId));
    const draft = raw ? (JSON.parse(raw) as Draft) : null;
    return draft && typeof draft.text === 'string' && typeof draft.baseRevision === 'number' ? draft : null;
  }, null);
}

export function removeDraft(userId: string, itemId: string) {
  safely(() => localStorage.removeItem(key(userId, itemId)), undefined);
}

export function clearUserDrafts(userId: string) {
  for (const k of draftKeys().filter((k) => k.startsWith(`${PREFIX}${userId}:`))) {
    safely(() => localStorage.removeItem(k), undefined);
  }
}

export function purgeOldDrafts(now = Date.now()) {
  for (const k of draftKeys()) {
    const savedAt = safely(() => Date.parse((JSON.parse(localStorage.getItem(k) ?? '') as Draft).savedAt), Number.NaN);
    if (!(now - savedAt < MAX_AGE_MS)) {
      safely(() => localStorage.removeItem(k), undefined);
    }
  }
}

export interface DraftWriter {
  /** Records the latest unsaved text; written at most every <paramref>throttleMs</paramref>. */
  write: (text: string, baseRevision: number) => void;
  /** Writes any pending text now (synchronous, safe in pagehide). */
  flush: () => void;
  cancel: () => void;
}

export function createDraftWriter(userId: string, itemId: string, throttleMs = 500): DraftWriter {
  let pending: Omit<Draft, 'savedAt'> | null = null;
  let timer: ReturnType<typeof setTimeout> | undefined;

  const flush = () => {
    clearTimeout(timer);
    timer = undefined;
    if (pending) {
      writeDraft(userId, itemId, pending);
      pending = null;
    }
  };

  return {
    write(text, baseRevision) {
      pending = { text, baseRevision };
      timer ??= setTimeout(flush, throttleMs);
    },
    flush,
    cancel() {
      clearTimeout(timer);
      timer = undefined;
      pending = null;
    },
  };
}

/**
 * Watches the page going away. <paramref>onHidden</paramref> runs when the tab is hidden (switching
 * tabs or apps, and also just before an unload): the page may come back, so save normally.
 * <paramref>onPageHide</paramref> runs when the page is being unloaded (close, reload, navigate away).
 */
export function usePageLeave(onHidden: () => void, onPageHide: () => void) {
  const callbacks = useRef({ onHidden, onPageHide });
  useEffect(() => {
    callbacks.current = { onHidden, onPageHide };
  });

  useEffect(() => {
    const handlePageHide = () => callbacks.current.onPageHide();
    const handleVisibility = () => {
      if (document.visibilityState === 'hidden') {
        callbacks.current.onHidden();
      }
    };
    window.addEventListener('pagehide', handlePageHide);
    document.addEventListener('visibilitychange', handleVisibility);
    return () => {
      window.removeEventListener('pagehide', handlePageHide);
      document.removeEventListener('visibilitychange', handleVisibility);
    };
  }, []);
}

/**
 * A last-moment save while the page unloads. Browsers refuse keepalive bodies over 64 KiB, so larger
 * edits rely on the draft instead. Returns whether a request was sent; its result is not awaited.
 */
export function sendKeepaliveSave(args: {
  workspaceId: string;
  itemId: string;
  revision: number;
  text: string;
  accessToken: string | null;
}): boolean {
  return sendKeepalive({
    path: `/api/workspaces/${encodeURIComponent(args.workspaceId)}/files/${encodeURIComponent(args.itemId)}/content`,
    contentType: 'text/plain; charset=utf-8',
    revision: args.revision,
    body: args.text,
    accessToken: args.accessToken,
  });
}

/** A last-moment PUT with If-Match while the page unloads; refused by browsers above 64 KiB. */
export function sendKeepalive(args: { path: string; contentType: string; revision: number; body: string; accessToken: string | null }): boolean {
  if (!args.accessToken || new TextEncoder().encode(args.body).length > KEEPALIVE_LIMIT_BYTES) {
    return false;
  }
  void fetch(`${window.location.origin}${args.path}`, {
    method: 'PUT',
    keepalive: true,
    body: args.body,
    headers: {
      Authorization: `Bearer ${args.accessToken}`,
      'If-Match': `"${args.revision}"`,
      'Content-Type': args.contentType,
    },
  }).catch(() => undefined);
  return true;
}
