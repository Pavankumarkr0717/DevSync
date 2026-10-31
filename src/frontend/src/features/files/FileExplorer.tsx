import { useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { useAppDispatch, useAppSelector } from '../../app/hooks';
import type { Item, ItemKind, WorkspaceRole } from '../../shared/api/types';
import { DeleteDialog } from './DeleteDialog';
import { folderToggled, selectWorkspaceEditor } from './editorSlice';
import { structureErrorMessage } from './fileMessages';
import { childrenByParent, descendantIds } from './fileTree';
import { useCreateItemMutation, useDeleteItemMutation, useMoveItemMutation, useRenameItemMutation } from './filesApi';
import type { MenuAction } from './ItemMenu';
import { MoveDialog } from './MoveDialog';
import { NameInput } from './NameInput';
import { TreeNode, type TreeContext } from './TreeNode';

interface Props {
  workspaceId: string;
  items: Item[];
  role: WorkspaceRole;
  onOpen: (itemId: string) => void;
  /** Called with every removed id after a delete, so open tabs can close. */
  onDeleted: (itemIds: string[]) => void;
}

interface Creating {
  parentId: string | null;
  kind: ItemKind;
}

/** The workspace's folder and file tree (spec 003 FR-002, FR-012, FR-030; contracts/ui-routes.md). */
export function FileExplorer({ workspaceId, items, role, onOpen, onDeleted }: Props) {
  const dispatch = useAppDispatch();
  const { expanded, activeId } = useAppSelector((state) => selectWorkspaceEditor(state, workspaceId));
  const [createItem] = useCreateItemMutation();
  const [renameItem] = useRenameItemMutation();
  const [moveItem, moveState] = useMoveItemMutation();
  const [deleteItem, deleteState] = useDeleteItemMutation();
  const [creating, setCreating] = useState<Creating | null>(null);
  const [renamingId, setRenamingId] = useState<string | null>(null);
  const [moving, setMoving] = useState<{ item: Item; error: string | null } | null>(null);
  const [deleting, setDeleting] = useState<{ item: Item; error: string | null } | null>(null);
  const [focusedId, setFocusedId] = useState<string | null>(null);
  const treeRef = useRef<HTMLUListElement>(null);

  const canEdit = role === 'owner' || role === 'editor';
  const canDelete = role === 'owner';
  const children = useMemo(() => childrenByParent(items), [items]);
  const childrenOf = (parentId: string | null) => children.get(parentId ?? '') ?? [];
  const topLevel = childrenOf(null);

  const toggle = (folderId: string, value?: boolean) => dispatch(folderToggled({ workspaceId, folderId, expanded: value }));

  function startCreate(parentId: string | null, kind: ItemKind) {
    if (parentId) {
      toggle(parentId, true);
    }
    setCreating({ parentId, kind });
  }

  async function submitCreate(name: string): Promise<string | null> {
    if (!creating) {
      return null;
    }
    const result = await createItem({ workspaceId, parentId: creating.parentId, kind: creating.kind, name });
    if ('error' in result) {
      return structureErrorMessage(result.error);
    }
    setCreating(null);
    if (result.data.kind === 'file') {
      onOpen(result.data.id);
    }
    return null;
  }

  function createInput(parentId: string | null) {
    if (!creating || creating.parentId !== parentId) {
      return null;
    }
    return (
      <li role="none" key="new-item">
        <NameInput
          label={creating.kind === 'file' ? 'New file name' : 'New folder name'}
          kind={creating.kind}
          siblings={childrenOf(parentId)}
          onSubmit={submitCreate}
          onCancel={() => setCreating(null)}
        />
      </li>
    );
  }

  function renameInput(item: Item) {
    if (renamingId !== item.id) {
      return null;
    }
    return (
      <NameInput
        label={`Rename ${item.name}`}
        kind={item.kind}
        initialValue={item.name}
        siblings={childrenOf(item.parentId)}
        selfId={item.id}
        onSubmit={async (name) => {
          if (name === item.name) {
            setRenamingId(null);
            return null;
          }
          const result = await renameItem({ workspaceId, itemId: item.id, name });
          if ('error' in result) {
            return structureErrorMessage(result.error);
          }
          setRenamingId(null);
          return null;
        }}
        onCancel={() => setRenamingId(null)}
      />
    );
  }

  async function confirmMove(parentId: string | null) {
    if (!moving) {
      return;
    }
    const result = await moveItem({ workspaceId, itemId: moving.item.id, parentId });
    if ('error' in result) {
      setMoving({ item: moving.item, error: structureErrorMessage(result.error) });
      return;
    }
    if (parentId) {
      toggle(parentId, true);
    }
    setMoving(null);
  }

  async function confirmDelete() {
    if (!deleting) {
      return;
    }
    const removed = [deleting.item.id, ...descendantIds(items, deleting.item.id)];
    const result = await deleteItem({ workspaceId, itemId: deleting.item.id });
    if ('error' in result) {
      setDeleting({ item: deleting.item, error: structureErrorMessage(result.error) });
      return;
    }
    setDeleting(null);
    onDeleted(removed);
  }

  function actionsFor(item: Item): MenuAction[] {
    const actions: MenuAction[] = [];
    if (canEdit && item.kind === 'folder') {
      actions.push(
        { label: 'New file', onSelect: () => startCreate(item.id, 'file') },
        { label: 'New folder', onSelect: () => startCreate(item.id, 'folder') },
      );
    }
    if (canEdit) {
      actions.push(
        { label: 'Rename', onSelect: () => setRenamingId(item.id) },
        { label: 'Move to…', onSelect: () => setMoving({ item, error: null }) },
      );
    }
    if (canDelete) {
      actions.push({ label: 'Delete', onSelect: () => setDeleting({ item, error: null }) });
    }
    return actions;
  }

  const ctx: TreeContext = {
    childrenOf,
    isExpanded: (id) => Boolean(expanded[id]),
    activeId,
    focusedId: focusedId ?? topLevel[0]?.id ?? null,
    onToggle: (id) => toggle(id),
    onOpen,
    onFocusItem: setFocusedId,
    actionsFor,
    inputFor: (item, place) => {
      if (!item) {
        return null;
      }
      return place === 'children' ? createInput(item.id) : renameInput(item);
    },
  };

  // Tree keyboard navigation (WAI-ARIA tree pattern) over the visible items.
  function handleKeyDown(event: KeyboardEvent<HTMLUListElement>) {
    const current = event.target as HTMLElement;
    if (current.getAttribute('role') !== 'treeitem' || !treeRef.current) {
      return;
    }
    const visible = Array.from(treeRef.current.querySelectorAll<HTMLElement>('[role="treeitem"]'));
    const index = visible.indexOf(current);
    const id = current.dataset.id!;
    const isFolder = current.dataset.kind === 'folder';
    const isOpen = current.getAttribute('aria-expanded') === 'true';

    const handlers: Record<string, () => void> = {
      ArrowDown: () => visible[index + 1]?.focus(),
      ArrowUp: () => visible[index - 1]?.focus(),
      Home: () => visible[0]?.focus(),
      End: () => visible[visible.length - 1]?.focus(),
      ArrowRight: () => {
        if (isFolder && !isOpen) {
          toggle(id, true);
        } else if (isFolder) {
          visible[index + 1]?.focus();
        }
      },
      ArrowLeft: () => {
        if (isFolder && isOpen) {
          toggle(id, false);
        } else {
          current.parentElement?.closest<HTMLElement>('[role="treeitem"]')?.focus();
        }
      },
      Enter: () => (isFolder ? toggle(id) : onOpen(id)),
      F2: () => {
        if (canEdit) {
          setRenamingId(id);
        }
      },
    };

    const handler = handlers[event.key];
    if (handler) {
      event.preventDefault();
      handler();
    }
  }

  return (
    <div className="explorer">
      <div className="explorer-toolbar">
        <span className="explorer-title">Explorer</span>
        {canEdit && (
          <span>
            <button type="button" onClick={() => startCreate(null, 'file')}>
              New file
            </button>
            <button type="button" onClick={() => startCreate(null, 'folder')}>
              New folder
            </button>
          </span>
        )}
      </div>
      {items.length === 0 && !creating && <p className="meta">No files yet</p>}
      <ul role="tree" aria-label="Files" className="tree" ref={treeRef} onKeyDown={handleKeyDown} data-testid="file-tree">
        {createInput(null)}
        {topLevel.map((item) => (
          <TreeNode key={item.id} item={item} level={1} ctx={ctx} />
        ))}
      </ul>
      {moving && (
        <MoveDialog
          item={moving.item}
          items={items}
          error={moving.error}
          busy={moveState.isLoading}
          onMove={(parentId) => void confirmMove(parentId)}
          onCancel={() => setMoving(null)}
        />
      )}
      {deleting && (
        <DeleteDialog
          item={deleting.item}
          inside={descendantIds(items, deleting.item.id).length}
          error={deleting.error}
          busy={deleteState.isLoading}
          onConfirm={() => void confirmDelete()}
          onCancel={() => setDeleting(null)}
        />
      )}
    </div>
  );
}
