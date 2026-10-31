import { useEffect, useRef } from 'react';
import { useAppDispatch, useAppSelector } from '../../app/hooks';
import type { Item } from '../../shared/api/types';
import { selectWorkspaceEditor, tabsRestored } from './editorSlice';
import { loadTabs, saveTabs } from './tabStorage';

/**
 * Reopens the tabs this person had open in this workspace (after a refresh), once the tree is
 * known, and keeps the stored list up to date afterwards (spec 003 FR-033).
 */
export function useTabPersistence(workspaceId: string, items: Item[] | undefined) {
  const dispatch = useAppDispatch();
  const userId = useAppSelector((state) => state.auth.user?.id);
  const { openTabs, activeId } = useAppSelector((state) => selectWorkspaceEditor(state, workspaceId));
  const restored = useRef(false);

  useEffect(() => {
    if (restored.current || !items || !userId) {
      return;
    }
    restored.current = true;
    // Tabs already in memory (navigating back within the app) win over the stored list.
    if (openTabs.length > 0) {
      return;
    }
    const files = new Set(items.filter((i) => i.kind === 'file').map((i) => i.id));
    const stored = loadTabs(userId, workspaceId, files);
    if (stored && stored.itemIds.length > 0) {
      dispatch(tabsRestored({ workspaceId, ...stored }));
    }
  }, [dispatch, items, openTabs.length, userId, workspaceId]);

  useEffect(() => {
    if (restored.current && userId) {
      saveTabs(userId, workspaceId, { itemIds: openTabs, activeId });
    }
  }, [activeId, openTabs, userId, workspaceId]);
}
