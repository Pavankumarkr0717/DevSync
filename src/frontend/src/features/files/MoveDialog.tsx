import { useId, useState } from 'react';
import type { Item } from '../../shared/api/types';
import { compareItems, descendantIds, folderPath } from './fileTree';

interface Props {
  item: Item;
  items: Item[];
  error: string | null;
  busy: boolean;
  onMove: (parentId: string | null) => void;
  onCancel: () => void;
}

const TOP = '';

/** "Move to…": pick a folder or the top level. The item and its descendants cannot be chosen (spec 003 FR-014, FR-034). */
export function MoveDialog({ item, items, error, busy, onMove, onCancel }: Props) {
  const titleId = useId();
  const [target, setTarget] = useState<string>(item.parentId ?? TOP);
  const blocked = new Set([item.id, ...descendantIds(items, item.id)]);
  const folders = items
    .filter((i) => i.kind === 'folder')
    .map((folder) => ({ folder, path: [folderPath(items, folder.id), folder.name].filter(Boolean).join('/') }))
    .sort((a, b) => a.path.localeCompare(b.path, undefined, { sensitivity: 'base' }) || compareItems(a.folder, b.folder));

  return (
    <div className="dialog-backdrop">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="dialog"
        onKeyDown={(event) => {
          if (event.key === 'Escape') {
            onCancel();
          }
        }}
      >
        <h2 id={titleId}>Move {item.name}</h2>
        <fieldset className="folder-picker">
          <legend>Destination</legend>
          <label>
            <input type="radio" name="target" checked={target === TOP} onChange={() => setTarget(TOP)} />
            Top level
          </label>
          {folders.map(({ folder, path }) => (
            <label key={folder.id}>
              <input
                type="radio"
                name="target"
                aria-label={path}
                disabled={blocked.has(folder.id)}
                checked={target === folder.id}
                onChange={() => setTarget(folder.id)}
              />
              {path}
            </label>
          ))}
        </fieldset>
        {error && (
          <p role="alert" className="field-error">
            {error}
          </p>
        )}
        <div className="dialog-actions">
          <button type="button" onClick={onCancel}>
            Cancel
          </button>
          <button type="button" disabled={busy} onClick={() => onMove(target === TOP ? null : target)}>
            Move
          </button>
        </div>
      </div>
    </div>
  );
}
