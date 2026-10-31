import { useState } from 'react';
import { useAppDispatch, useAppSelector } from '../../app/hooks';
import type { ComponentType, DiagramEdgeDoc, DiagramNodeDoc, MetadataEntry } from '../../shared/api/types';
import { PlusIcon, TrashIcon, XIcon } from '../../shared/ui/icons';
import { toastShown } from '../../shared/ui/uiSlice';
import { COMPONENT_TYPES, COMPONENT_TYPE_INFO, LIMITS } from './componentTypes';
import { connectionProblem } from './connectionRules';
import { edgeAdded, edgeUpdated, elementsDeleted, nodeUpdated, selectDiagramEdges, selectDiagramNodes } from './diagramEditorSlice';
import { newId } from './document';

export interface Selection {
  nodeIds: string[];
  edgeIds: string[];
}

const length = (s: string) => [...s].length;
// eslint-disable-next-line no-control-regex
const hasControl = (s: string, allowLineBreaks = false) => (allowLineBreaks ? /[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f]/ : /[\u0000-\u001f\u007f]/).test(s);

/** Why metadata can't be committed as typed, or null (mirrors the server rules, FR-012, FR-034). */
function metadataProblem(entries: MetadataEntry[]): string | null {
  const keys = new Set<string>();
  for (const entry of entries) {
    if (entry.key.trim().length === 0) {
      return 'Every metadata entry needs a key.';
    }
    if (hasControl(entry.key) || hasControl(entry.value)) {
      return 'Metadata can’t contain control characters.';
    }
    const k = entry.key.toUpperCase();
    if (keys.has(k)) {
      return `The key “${entry.key}” is used twice.`;
    }
    keys.add(k);
  }
  return null;
}

function Counter({ value, max }: { value: string; max: number }) {
  return (
    <span className="counter" aria-hidden="true">
      {length(value)}/{max}
    </span>
  );
}

/** The configuration panel for the selection (spec 004 FR-012, FR-013, FR-015; contracts/ui-routes.md). */
export function PropertiesPanel({ selection, readOnly, onDeleted }: { selection: Selection; readOnly: boolean; onDeleted: () => void }) {
  const nodes = useAppSelector(selectDiagramNodes);
  const edges = useAppSelector(selectDiagramEdges);
  const dispatch = useAppDispatch();
  const selectedNodes = nodes.filter((n) => selection.nodeIds.includes(n.id));
  const selectedEdges = edges.filter((e) => selection.edgeIds.includes(e.id));
  const count = selectedNodes.length + selectedEdges.length;

  const remove = () => {
    dispatch(elementsDeleted({ nodeIds: selectedNodes.map((n) => n.id), edgeIds: selectedEdges.map((e) => e.id) }));
    onDeleted();
  };

  return (
    <aside className="floating-panel properties" aria-label="Properties" data-testid="properties-panel">
      <p className="panel-title">Properties</p>
      {count === 0 && (
        <p className="meta">
          {nodes.length} components · {edges.length} connections. Select something to see its details.
        </p>
      )}
      {count === 1 && selectedNodes.length === 1 && (
        <NodeForm key={selectedNodes[0].id} node={selectedNodes[0]} nodes={nodes} edges={edges} readOnly={readOnly} onDelete={remove} />
      )}
      {count === 1 && selectedEdges.length === 1 && (
        <EdgeForm key={selectedEdges[0].id} edge={selectedEdges[0]} nodes={nodes} readOnly={readOnly} onDelete={remove} />
      )}
      {count > 1 && (
        <>
          <p>{count} items selected</p>
          {!readOnly && (
            <button type="button" className="danger" onClick={remove}>
              <TrashIcon />
              Delete selected
            </button>
          )}
        </>
      )}
    </aside>
  );
}

function NodeForm({ node, nodes: all, edges: allEdges, readOnly: ro, onDelete }: {
  node: DiagramNodeDoc; nodes: DiagramNodeDoc[]; edges: DiagramEdgeDoc[]; readOnly: boolean; onDelete: () => void;
}) {
  const dispatch = useAppDispatch();
  const [title, setTitle] = useState(node.title);
  const [description, setDescription] = useState(node.description);
  const [metadata, setMetadata] = useState<MetadataEntry[]>(node.metadata);
  const [connectTo, setConnectTo] = useState('');
  const titleError = title.trim().length === 0 ? 'Title is required.' : hasControl(title) ? 'Title can’t contain control characters.' : null;
  const metaError = metadataProblem(metadata);
  const others = all.filter((n) => n.id !== node.id);

  const commit = (changes: Partial<Omit<DiagramNodeDoc, 'id'>>) => dispatch(nodeUpdated({ id: node.id, changes }));

  const changeMetadata = (next: MetadataEntry[]) => {
    setMetadata(next);
    if (!metadataProblem(next)) {
      commit({ metadata: next });
    }
  };

  const connect = (target: string) => {
    const problem = connectionProblem(node.id, target, allEdges);
    if (problem) {
      dispatch(toastShown(problem));
      return;
    }
    dispatch(edgeAdded({ id: newId(), source: node.id, target, label: '' }));
    setConnectTo('');
  };

  return (
    <form onSubmit={(e) => e.preventDefault()} aria-label={`Component ${node.title}`}>
      <div className="field">
        <label htmlFor="node-type">Type</label>
        <select
          id="node-type"
          value={node.type}
          disabled={ro}
          onChange={(e) => commit({ type: e.target.value as ComponentType })}
        >
          {COMPONENT_TYPES.map((t) => (
            <option key={t.type} value={t.type}>
              {t.label}
            </option>
          ))}
        </select>
      </div>
      <div className="field">
        <label htmlFor="node-title">
          Title <Counter value={title} max={LIMITS.maxTitle} />
        </label>
        <input
          id="node-title"
          value={title}
          maxLength={LIMITS.maxTitle}
          readOnly={ro}
          aria-invalid={titleError ? true : undefined}
          onChange={(e) => {
            setTitle(e.target.value);
            if (e.target.value.trim() && !hasControl(e.target.value)) {
              commit({ title: e.target.value });
            }
          }}
        />
        {titleError && <span className="field-error">{titleError}</span>}
      </div>
      <div className="field">
        <label htmlFor="node-description">
          Description <Counter value={description} max={LIMITS.maxDescription} />
        </label>
        <textarea
          id="node-description"
          rows={3}
          value={description}
          maxLength={LIMITS.maxDescription}
          readOnly={ro}
          onChange={(e) => {
            setDescription(e.target.value);
            if (!hasControl(e.target.value, true)) {
              commit({ description: e.target.value });
            }
          }}
        />
      </div>
      <fieldset className="field" style={{ border: 0, padding: 0, margin: 0 }}>
        <legend>Metadata</legend>
        {metadata.map((entry, i) => (
          <div className="metadata-row" key={i}>
            <input
              aria-label={`Metadata key ${i + 1}`}
              placeholder="key"
              value={entry.key}
              maxLength={LIMITS.maxMetadataKey}
              readOnly={ro}
              onChange={(e) => changeMetadata(metadata.map((m, j) => (j === i ? { ...m, key: e.target.value } : m)))}
            />
            <input
              aria-label={`Metadata value ${i + 1}`}
              placeholder="value"
              value={entry.value}
              maxLength={LIMITS.maxMetadataValue}
              readOnly={ro}
              onChange={(e) => changeMetadata(metadata.map((m, j) => (j === i ? { ...m, value: e.target.value } : m)))}
            />
            {!ro && (
              <button
                type="button"
                className="icon-button"
                aria-label={`Remove metadata ${entry.key || i + 1}`}
                onClick={() => changeMetadata(metadata.filter((_, j) => j !== i))}
              >
                <XIcon size={16} />
              </button>
            )}
          </div>
        ))}
        {metaError && (
          <p className="field-error" role="alert">
            {metaError}
          </p>
        )}
        {!ro && (
          <button
            type="button"
            disabled={metadata.length >= LIMITS.maxMetadata}
            onClick={() => setMetadata([...metadata, { key: '', value: '' }])}
          >
            <PlusIcon size={16} />
            Add entry
          </button>
        )}
      </fieldset>
      {!ro && others.length > 0 && (
        <div className="field">
          <label htmlFor="node-connect">Connect to…</label>
          <select
            id="node-connect"
            value={connectTo}
            onChange={(e) => {
              setConnectTo(e.target.value);
              if (e.target.value) {
                connect(e.target.value);
              }
            }}
          >
            <option value="">Choose a component</option>
            {others.map((o) => (
              <option key={o.id} value={o.id}>
                {o.title} ({COMPONENT_TYPE_INFO[o.type]?.label})
              </option>
            ))}
          </select>
        </div>
      )}
      {!ro && (
        <button type="button" className="danger" onClick={onDelete}>
          <TrashIcon />
          Delete component
        </button>
      )}
    </form>
  );
}

function EdgeForm({ edge, nodes: all, readOnly: ro, onDelete }: { edge: DiagramEdgeDoc; nodes: DiagramNodeDoc[]; readOnly: boolean; onDelete: () => void }) {
  const dispatch = useAppDispatch();
  const [label, setLabel] = useState(edge.label);
  const name = (id: string) => all.find((n) => n.id === id)?.title ?? id;

  return (
    <form onSubmit={(e) => e.preventDefault()} aria-label="Connection">
      <p>
        {name(edge.source)} → {name(edge.target)}
      </p>
      <div className="field">
        <label htmlFor="edge-label">
          Label <Counter value={label} max={LIMITS.maxLabel} />
        </label>
        <input
          id="edge-label"
          value={label}
          maxLength={LIMITS.maxLabel}
          readOnly={ro}
          onChange={(e) => {
            setLabel(e.target.value);
            if (!hasControl(e.target.value)) {
              dispatch(edgeUpdated({ id: edge.id, label: e.target.value }));
            }
          }}
        />
      </div>
      {!ro && (
        <button type="button" className="danger" onClick={onDelete}>
          <TrashIcon />
          Delete connection
        </button>
      )}
    </form>
  );
}
