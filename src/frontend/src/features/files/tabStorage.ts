/**
 * Open tabs per user and workspace in this browser, so a refresh reopens them (spec 003 FR-033).
 * Not shared with the server or other devices. Storage failures are ignored.
 */

export interface StoredTabs {
  itemIds: string[];
  activeId: string | null;
}

const PREFIX = 'devsync:tabs:';
const key = (userId: string, workspaceId: string) => `${PREFIX}${userId}:${workspaceId}`;

export function saveTabs(userId: string, workspaceId: string, tabs: StoredTabs) {
  try {
    localStorage.setItem(key(userId, workspaceId), JSON.stringify(tabs));
  } catch {
    // Not essential: the tabs simply won't come back after a refresh.
  }
}

/** The stored tabs that still exist; null when nothing usable is stored. */
export function loadTabs(userId: string, workspaceId: string, existing: ReadonlySet<string>): StoredTabs | null {
  try {
    const raw = localStorage.getItem(key(userId, workspaceId));
    const stored = raw ? (JSON.parse(raw) as StoredTabs) : null;
    if (!stored || !Array.isArray(stored.itemIds)) {
      return null;
    }
    const itemIds = stored.itemIds.filter((id) => typeof id === 'string' && existing.has(id));
    const activeId = stored.activeId && itemIds.includes(stored.activeId) ? stored.activeId : (itemIds[0] ?? null);
    return { itemIds, activeId };
  } catch {
    return null;
  }
}

export function clearUserTabs(userId: string) {
  try {
    const prefix = `${PREFIX}${userId}:`;
    const keys = Array.from({ length: localStorage.length }, (_, i) => localStorage.key(i)).filter((k) => k?.startsWith(prefix));
    keys.forEach((k) => localStorage.removeItem(k!));
  } catch {
    // Nothing to clean up if storage is unavailable.
  }
}
