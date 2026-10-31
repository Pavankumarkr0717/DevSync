import type { ReactNode } from 'react';
import type { Item } from '../../shared/api/types';
import { ItemMenu, type MenuAction } from './ItemMenu';

export interface TreeContext {
  childrenOf: (parentId: string | null) => Item[];
  isExpanded: (folderId: string) => boolean;
  activeId: string | null;
  focusedId: string | null;
  onToggle: (folderId: string) => void;
  onOpen: (itemId: string) => void;
  onFocusItem: (itemId: string) => void;
  actionsFor: (item: Item) => MenuAction[];
  /** An inline input (create or rename) to show at this position, if any. */
  inputFor: (item: Item | null, place: 'rename' | 'children') => ReactNode;
}

/** One folder or file in the explorer. Names are always rendered as text. */
export function TreeNode({ item, level, ctx }: { item: Item; level: number; ctx: TreeContext }) {
  const isFolder = item.kind === 'folder';
  const expanded = isFolder && ctx.isExpanded(item.id);
  const renaming = ctx.inputFor(item, 'rename');

  return (
    <li
      role="treeitem"
      aria-label={item.name}
      aria-level={level}
      aria-expanded={isFolder ? expanded : undefined}
      aria-selected={item.id === ctx.activeId}
      tabIndex={item.id === ctx.focusedId ? 0 : -1}
      data-id={item.id}
      data-kind={item.kind}
      data-name={item.name}
      onFocus={(event) => {
        if (event.target === event.currentTarget) {
          ctx.onFocusItem(item.id);
        }
      }}
    >
      <div
        className={`tree-row${item.id === ctx.activeId ? ' active' : ''}`}
        style={{ paddingInlineStart: `${(level - 1) * 0.9 + 0.25}rem` }}
        onClick={() => (isFolder ? ctx.onToggle(item.id) : ctx.onOpen(item.id))}
      >
        <span aria-hidden="true" className="tree-icon">
          {isFolder ? (expanded ? '▾' : '▸') : '·'}
        </span>
        {renaming ?? <span className="tree-name">{item.name}</span>}
        <ItemMenu name={item.name} actions={ctx.actionsFor(item)} />
      </div>
      {expanded && (
        <ul role="group">
          {ctx.inputFor(item, 'children')}
          {ctx.childrenOf(item.id).map((child) => (
            <TreeNode key={child.id} item={child} level={level + 1} ctx={ctx} />
          ))}
        </ul>
      )}
    </li>
  );
}
