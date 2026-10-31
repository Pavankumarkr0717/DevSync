import { useEffect, useMemo, useRef } from 'react';
import { useAppDispatch, useAppSelector, useAppStore } from '../../app/hooks';
import type { Item } from '../../shared/api/types';
import { usePageLeave } from '../../shared/editing/drafts';
import type { FileSessions } from './EditorPane';
import { saveStateChanged } from './editorSlice';
import { FileSession } from './fileSession';
import { filesApi } from './filesApi';
import { toSaveOutcome } from '../../shared/editing/saveOutcome';

/**
 * Keeps one {@link FileSession} per open file for the files page. Saves of the file being left are
 * flushed on tab switch; everything pending is saved (and kept as a draft) when the page is hidden,
 * unloaded or left (spec 003 FR-021, FR-035).
 */
export function useFileSessions(
  workspaceId: string,
  activeId: string | null,
): FileSessions & { close: (itemId: string) => void; discard: (itemId: string) => void } {
  const dispatch = useAppDispatch();
  const store = useAppStore();
  const userId = useAppSelector((state) => state.auth.user?.id ?? '');
  const sessions = useRef(new Map<string, FileSession>());

  const registry = useMemo(
    () => ({
      get: (itemId: string) => sessions.current.get(itemId),
      open: (item: Item, text: string, revision: number) => {
        const existing = sessions.current.get(item.id);
        if (existing) {
          return existing;
        }
        const session = new FileSession({
          workspaceId,
          itemId: item.id,
          userId,
          text,
          revision,
          save: async (body, baseRevision) => {
            try {
              const result = await dispatch(
                filesApi.endpoints.saveFileContent.initiate({ workspaceId, itemId: item.id, revision: baseRevision, text: body }),
              ).unwrap();
              return { ok: true, revision: result.revision };
            } catch (error) {
              return toSaveOutcome(error);
            }
          },
          onStatus: (update) => dispatch(saveStateChanged({ itemId: item.id, ...update })),
        });
        sessions.current.set(item.id, session);
        return session;
      },
      close: (itemId: string) => {
        sessions.current.get(itemId)?.close();
        sessions.current.delete(itemId);
      },
      /** The file was deleted by this person: stop without saving. */
      discard: (itemId: string) => {
        sessions.current.get(itemId)?.controller.dispose();
        sessions.current.delete(itemId);
      },
    }),
    [dispatch, userId, workspaceId],
  );

  // Leaving a file saves its pending edits now instead of waiting for the delay.
  const previousActive = useRef(activeId);
  useEffect(() => {
    if (previousActive.current && previousActive.current !== activeId) {
      sessions.current.get(previousActive.current)?.flush();
    }
    previousActive.current = activeId;
  }, [activeId]);

  usePageLeave(
    () => sessions.current.forEach((session) => session.hide()),
    () => {
      const token = store.getState().auth.accessToken;
      sessions.current.forEach((session) => session.leave(token));
    },
  );

  // Leaving the files page (or the workspace) saves everything pending.
  useEffect(() => {
    const open = sessions.current;
    return () => {
      open.forEach((session) => session.close());
      open.clear();
    };
  }, [workspaceId]);

  return registry;
}
