import { useAppSelector } from '../../app/hooks';
import type { Item } from '../../shared/api/types';
import type { EditorState } from './editorSlice';
import { folderPath } from './fileTree';

interface Props {
  workspaceId: string;
  items: Item[];
  openTabs: string[];
  activeId: string | null;
  onActivate: (itemId: string) => void;
  onClose: (itemId: string) => void;
}

/** One tab per open file. Same-named files also show their folder (spec 003 FR-031). */
export function EditorTabs({ items, openTabs, activeId, onActivate, onClose }: Props) {
  const files = useAppSelector((state: { editor: EditorState }) => state.editor.files);
  const byId = new Map(items.map((i) => [i.id, i]));
  const nameCounts = new Map<string, number>();
  for (const id of openTabs) {
    const name = byId.get(id)?.name.toUpperCase() ?? '';
    nameCounts.set(name, (nameCounts.get(name) ?? 0) + 1);
  }

  if (openTabs.length === 0) {
    return null;
  }

  return (
    <div role="tablist" aria-label="Open files" className="tabs" data-testid="editor-tabs">
      {openTabs.map((id) => {
        const item = byId.get(id);
        const name = item?.name ?? '…';
        const clash = (nameCounts.get(name.toUpperCase()) ?? 0) > 1;
        const label = clash && item ? `${name} — ${folderPath(items, id) || '/'}` : name;
        const status = files[id]?.status;
        const marker = status === 'failed' ? ' !' : status === 'dirty' || status === 'saving' ? ' ●' : '';
        return (
          <span key={id} className={`tab${id === activeId ? ' active' : ''}`}>
            <button
              type="button"
              role="tab"
              aria-selected={id === activeId}
              aria-label={`${label}${status === 'failed' ? ', save failed' : status === 'dirty' ? ', unsaved changes' : ''}`}
              onClick={() => onActivate(id)}
            >
              {label}
              {marker && <span aria-hidden="true">{marker}</span>}
            </button>
            <button type="button" className="icon-button" aria-label={`Close ${label}`} onClick={() => onClose(id)}>
              ×
            </button>
          </span>
        );
      })}
    </div>
  );
}
