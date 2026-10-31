import type { Item } from '../../shared/api/types';

/** Folders first, then files; each by name ignoring case (spec 003 FR-002). */
export function compareItems(a: Item, b: Item): number {
  if (a.kind !== b.kind) {
    return a.kind === 'folder' ? -1 : 1;
  }
  return a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }) || a.name.localeCompare(b.name);
}

/** Sorted children for every parent id ('' = top level). */
export function childrenByParent(items: readonly Item[]): Map<string, Item[]> {
  const map = new Map<string, Item[]>();
  for (const item of items) {
    const key = item.parentId ?? '';
    map.set(key, [...(map.get(key) ?? []), item]);
  }
  for (const list of map.values()) {
    list.sort(compareItems);
  }
  return map;
}

/** The item's folder names from the top level, joined with "/" (empty for top-level items). */
export function folderPath(items: readonly Item[], itemId: string): string {
  const byId = new Map(items.map((i) => [i.id, i]));
  const names: string[] = [];
  for (let parent = byId.get(itemId)?.parentId; parent; parent = byId.get(parent)?.parentId ?? null) {
    names.unshift(byId.get(parent)?.name ?? '');
  }
  return names.join('/');
}

/** Ids of every item below <paramref>itemId</paramref>. */
export function descendantIds(items: readonly Item[], itemId: string): string[] {
  const children = childrenByParent(items);
  const result: string[] = [];
  const walk = (id: string) => {
    for (const child of children.get(id) ?? []) {
      result.push(child.id);
      walk(child.id);
    }
  };
  walk(itemId);
  return result;
}
