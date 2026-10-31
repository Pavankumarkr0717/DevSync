import {
  applyEdgeChanges,
  applyNodeChanges,
  Background,
  BackgroundVariant,
  MarkerType,
  MiniMap,
  ReactFlow,
  ReactFlowProvider,
  useReactFlow,
  useViewport,
  type Connection,
  type EdgeChange,
  type NodeChange,
  type OnSelectionChangeParams,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, type DragEvent, type ReactNode } from 'react';
import { useAppDispatch, useAppSelector, useAppStore } from '../../app/hooks';
import type { ComponentType, Diagram, DiagramEdgeDoc, DiagramNodeDoc } from '../../shared/api/types';
import { usePageLeave } from '../../shared/editing/drafts';
import { toSaveOutcome } from '../../shared/editing/saveOutcome';
import { FitIcon, GridIcon, MinusIcon, PlusIcon } from '../../shared/ui/icons';
import { toastShown } from '../../shared/ui/uiSlice';
import { CanvasActionsContext, type CanvasActions } from './canvasContext';
import { ComponentNode, type ComponentFlowNode } from './ComponentNode';
import { COMPONENT_TYPE_INFO, DRAG_TYPE, LIMITS } from './componentTypes';
import { connectionProblem } from './connectionRules';
import {
  diagramLoaded,
  documentReplaced,
  edgeAdded,
  elementsDeleted,
  nodeAdded,
  nodeResized,
  nodesMoved,
  problemsReceived,
  saveStateChanged,
  selectDiagramEdges,
  selectDiagramEditor,
  selectDiagramNodes,
} from './diagramEditorSlice';
import { diagramsApi } from './diagramsApi';
import { DiagramSession } from './diagramSession';
import { DiagramStatusBars } from './DiagramStatusBars';
import { newId, parseDocument, serialize, toDocument } from './document';
import { LabelledEdge, type LabelledFlowEdge } from './LabelledEdge';
import { Palette } from './Palette';
import { PropertiesPanel, type Selection } from './PropertiesPanel';

const nodeTypes = { component: ComponentNode };
const edgeTypes = { labelled: LabelledEdge };
const GRID_KEY = 'devsync:diagram-grid';

const snap = (n: number) => Math.round(n / LIMITS.grid) * LIMITS.grid;
const clamp = (n: number) => Math.max(-LIMITS.maxCoordinate, Math.min(LIMITS.maxCoordinate, Math.round(n)));

function toFlowNodes(docs: DiagramNodeDoc[], editable: boolean, selected: ReadonlySet<string>): ComponentFlowNode[] {
  return docs.map((doc) => ({
    id: doc.id,
    type: 'component',
    position: doc.position,
    width: doc.size.width,
    height: doc.size.height,
    data: { doc },
    selected: selected.has(doc.id),
    draggable: editable,
    connectable: editable,
    deletable: editable,
    ariaLabel: `${COMPONENT_TYPE_INFO[doc.type]?.label ?? doc.type}: ${doc.title}`,
  }));
}

function toFlowEdges(docs: DiagramEdgeDoc[], nodes: DiagramNodeDoc[], editable: boolean, selected: ReadonlySet<string>): LabelledFlowEdge[] {
  const title = (id: string) => nodes.find((n) => n.id === id)?.title ?? id;
  return docs.map((doc) => ({
    id: doc.id,
    source: doc.source,
    target: doc.target,
    type: 'labelled',
    data: { label: doc.label },
    markerEnd: { type: MarkerType.ArrowClosed },
    selected: selected.has(doc.id),
    deletable: editable,
    ariaLabel: `${title(doc.source)} → ${title(doc.target)}${doc.label ? `: ${doc.label}` : ''}`,
  }));
}

function readGrid(userId: string): boolean {
  try {
    return localStorage.getItem(`${GRID_KEY}:${userId}`) !== 'off';
  } catch {
    return true;
  }
}

interface Props {
  workspaceId: string;
  diagram: Diagram;
  readOnly: boolean;
  /** Shown at the start of the toolbar row. */
  breadcrumbs?: ReactNode;
}

/** The lazy-loaded canvas page body (spec 004 US1–US4). React Flow only lives in this chunk. */
export default function DiagramEditor(props: Props) {
  return (
    <ReactFlowProvider>
      <Editor {...props} />
    </ReactFlowProvider>
  );
}

function Editor({ workspaceId, diagram, readOnly: roleReadOnly, breadcrumbs }: Props) {
  const dispatch = useAppDispatch();
  const store = useAppStore();
  const flow = useReactFlow();
  const userId = useAppSelector((s) => s.auth.user?.id ?? '');
  const editor = useAppSelector(selectDiagramEditor);
  const docNodes = useAppSelector(selectDiagramNodes);
  const docEdges = useAppSelector(selectDiagramEdges);
  const readOnly = roleReadOnly || editor.reason === 'forbidden' || editor.reason === 'deleted';
  const editable = !readOnly;

  // ---------- load and session (once per diagram: the page keys this editor by diagram id) ----------
  const [initial] = useState(diagram);
  const [loadedAt] = useState(() => store.getState().diagramEditor.loadVersion + 1);
  useLayoutEffect(() => {
    dispatch(diagramLoaded({ diagramId: initial.id, document: initial.document!, revision: initial.revision }));
  }, [dispatch, initial]);
  const loaded = editor.diagramId === initial.id && editor.loadVersion >= loadedAt;

  const [session] = useState(() => {
    const document = initial.document!;
    return new DiagramSession({
      workspaceId,
      diagramId: diagram.id,
      userId,
      json: serialize(document),
      revision: initial.revision,
      save: async (json, baseRevision) => {
        try {
          const result = await dispatch(
            diagramsApi.endpoints.saveDiagramDocument.initiate({ workspaceId, diagramId: diagram.id, revision: baseRevision, json }),
          ).unwrap();
          return { ok: true, revision: result.revision };
        } catch (error) {
          const data = (error as { data?: { code?: string; problems?: DiagramProblems; detail?: string; title?: string } }).data;
          if (data?.code === 'invalid_diagram' && data.problems) {
            dispatch(problemsReceived(data.problems));
          } else if (data?.code === 'stored_version_unsupported' || data?.code === 'unsupported_schema_version') {
            dispatch(problemsReceived([{ code: data.code, path: '', message: data.title ?? 'Unsupported format.' }]));
          }
          return toSaveOutcome(error);
        }
      },
      onStatus: (update) => dispatch(saveStateChanged(update)),
    });
  });
  const [, rerender] = useState(0);

  // Every committed change is serialized (with the live viewport) and handed to autosave.
  const lastChange = useRef(editor.changeVersion);
  useEffect(() => {
    if (editor.changeVersion !== lastChange.current) {
      lastChange.current = editor.changeVersion;
      session.edit(JSON.stringify(toDocument(docNodes, docEdges, flow.getViewport())));
    }
  }, [editor.changeVersion, docNodes, docEdges, flow, session]);

  usePageLeave(
    () => session.hide(),
    () => session.leave(store.getState().auth.accessToken),
  );
  useEffect(() => () => session.close(), [session]);

  // ---------- React Flow state: transient positions and selection are local ----------
  const [selection, setSelection] = useState<Selection>({ nodeIds: [], edgeIds: [] });
  const [flowNodes, setFlowNodes] = useState(() => toFlowNodes(docNodes, editable, new Set()));
  const [flowEdges, setFlowEdges] = useState(() => toFlowEdges(docEdges, docNodes, editable, new Set()));
  const [seeded, setSeeded] = useState({ docNodes, docEdges, editable });
  if (seeded.docNodes !== docNodes || seeded.docEdges !== docEdges || seeded.editable !== editable) {
    // A commit or reload changed the saved data: re-seed React Flow from it, keeping the selection.
    setSeeded({ docNodes, docEdges, editable });
    setFlowNodes(toFlowNodes(docNodes, editable, new Set(selection.nodeIds)));
    setFlowEdges(toFlowEdges(docEdges, docNodes, editable, new Set(selection.edgeIds)));
  }

  const onNodesChange = useCallback((changes: NodeChange<ComponentFlowNode>[]) => setFlowNodes((n) => applyNodeChanges(changes, n)), []);
  const onEdgesChange = useCallback((changes: EdgeChange<LabelledFlowEdge>[]) => setFlowEdges((e) => applyEdgeChanges(changes, e)), []);
  const onSelectionChange = useCallback(({ nodes, edges }: OnSelectionChangeParams) => {
    setSelection({ nodeIds: nodes.map((n) => n.id), edgeIds: edges.map((e) => e.id) });
  }, []);

  // ---------- commit points ----------
  const commitPositions = useCallback(
    (_: unknown, __: unknown, moved: ComponentFlowNode[]) =>
      dispatch(nodesMoved(moved.map((n) => ({ id: n.id, position: { x: clamp(n.position.x), y: clamp(n.position.y) } })))),
    [dispatch],
  );

  const actions = useMemo<CanvasActions>(
    () => ({
      editable,
      resized: (id, box) =>
        dispatch(
          nodeResized({
            id,
            position: { x: clamp(box.x), y: clamp(box.y) },
            size: {
              width: Math.min(LIMITS.maxSize, Math.max(LIMITS.minSize, Math.round(box.width))),
              height: Math.min(LIMITS.maxSize, Math.max(LIMITS.minSize, Math.round(box.height))),
            },
          }),
        ),
    }),
    [dispatch, editable],
  );

  const addComponent = (type: ComponentType, at?: { x: number; y: number }) => {
    if (docNodes.length >= LIMITS.maxNodes) {
      dispatch(toastShown(`A diagram can have at most ${LIMITS.maxNodes} components.`));
      return;
    }
    const info = COMPONENT_TYPE_INFO[type];
    const center = at ?? flow.screenToFlowPosition({ x: window.innerWidth / 2, y: window.innerHeight / 2 });
    let position = { x: snap(center.x - info.defaultSize.width / 2), y: snap(center.y - info.defaultSize.height / 2) };
    // Don't stack new components exactly on top of each other.
    while (docNodes.some((n) => n.position.x === position.x && n.position.y === position.y)) {
      position = { x: position.x + LIMITS.grid * 2, y: position.y + LIMITS.grid * 2 };
    }
    dispatch(
      nodeAdded({
        id: newId(),
        type,
        title: info.defaultTitle,
        description: '',
        metadata: [],
        position: { x: clamp(position.x), y: clamp(position.y) },
        size: { ...info.defaultSize },
      }),
    );
  };

  const onConnect = (connection: Connection) => {
    const problem = connectionProblem(connection.source, connection.target, docEdges);
    if (problem) {
      dispatch(toastShown(problem));
      return;
    }
    dispatch(edgeAdded({ id: newId(), source: connection.source, target: connection.target, label: '' }));
  };

  const onDrop = (event: DragEvent) => {
    event.preventDefault();
    const type = event.dataTransfer.getData(DRAG_TYPE) as ComponentType;
    if (editable && type && COMPONENT_TYPE_INFO[type]) {
      addComponent(type, flow.screenToFlowPosition({ x: event.clientX, y: event.clientY }));
    }
  };

  // ---------- conflict, drafts, reload ----------
  const reloadLatest = async () => {
    const latest = await dispatch(
      diagramsApi.endpoints.getDiagram.initiate({ workspaceId, diagramId: diagram.id }, { forceRefetch: true }),
    );
    if (latest.data?.document) {
      dispatch(diagramLoaded({ diagramId: diagram.id, document: latest.data.document, revision: latest.data.revision }));
      session.reload(serialize(latest.data.document), latest.data.revision);
      lastChange.current = store.getState().diagramEditor.changeVersion;
    }
  };

  const restoreDraft = () => {
    const text = session.takeDraft();
    const document = text ? parseDocument(text) : null;
    if (document) {
      dispatch(documentReplaced(document));
    }
    rerender((n) => n + 1);
  };

  // ---------- view ----------
  const [grid, setGrid] = useState(() => readGrid(userId));
  const toggleGrid = () => {
    const next = !grid;
    setGrid(next);
    try {
      localStorage.setItem(`${GRID_KEY}:${userId}`, next ? 'on' : 'off');
    } catch {
      // A preference only.
    }
  };

  if (!loaded) {
    return null;
  }

  return (
    <CanvasActionsContext.Provider value={actions}>
      <DiagramToolbar grid={grid} onToggleGrid={toggleGrid}>
        {breadcrumbs}
      </DiagramToolbar>
      <DiagramStatusBars
        editor={editor}
        draft={readOnly ? null : session.pendingDraft}
        onRetry={() => session.controller.retry()}
        onReload={() => void reloadLatest()}
        onOverwrite={() => {
          session.controller.setBase(editor.currentRevision ?? session.controller.revision);
          session.controller.resume();
          session.controller.flush();
        }}
        onCopy={() => void navigator.clipboard?.writeText(session.controller.currentText)}
        onRestoreDraft={restoreDraft}
        onDiscardDraft={() => {
          session.discardDraft();
          rerender((n) => n + 1);
        }}
      />
      <div className="canvas-body" onDragOver={(e) => e.preventDefault()} onDrop={onDrop} data-testid="diagram-canvas">
        <ReactFlow<ComponentFlowNode, LabelledFlowEdge>
          aria-label="Diagram canvas"
          colorMode="system"
          nodes={flowNodes}
          edges={flowEdges}
          nodeTypes={nodeTypes}
          edgeTypes={edgeTypes}
          onNodesChange={onNodesChange}
          onEdgesChange={onEdgesChange}
          onSelectionChange={onSelectionChange}
          onNodeDragStop={commitPositions}
          onSelectionDragStop={(event, nodes) => commitPositions(event, null, nodes)}
          onDelete={({ nodes, edges }) => {
            if (nodes.length > 0 || edges.length > 0) {
              dispatch(elementsDeleted({ nodeIds: nodes.map((n) => n.id), edgeIds: edges.map((e) => e.id) }));
            }
          }}
          onConnect={onConnect}
          isValidConnection={(c) => !!c.source && !!c.target && connectionProblem(c.source, c.target, docEdges) === null}
          nodesDraggable={editable}
          nodesConnectable={editable}
          edgesReconnectable={false}
          deleteKeyCode={editable ? ['Delete', 'Backspace'] : null}
          selectionKeyCode="Shift"
          multiSelectionKeyCode={['Shift', 'Control', 'Meta']}
          snapToGrid={grid}
          snapGrid={[LIMITS.grid, LIMITS.grid]}
          minZoom={LIMITS.minZoom}
          maxZoom={LIMITS.maxZoom}
          defaultViewport={initial.document?.viewport ?? undefined}
          fitView={!initial.document?.viewport}
          fitViewOptions={{ padding: 0.15 }}
          proOptions={{ hideAttribution: false }}
        >
          <Background variant={BackgroundVariant.Dots} gap={LIMITS.grid} size={1.5} />
          <MiniMap pannable zoomable ariaLabel="Overview" />
        </ReactFlow>
        {docNodes.length === 0 && (
          <div className="canvas-empty">
            <p>{editable ? 'Drag a component from the palette to start.' : 'This diagram is empty.'}</p>
          </div>
        )}
        {editable && <Palette disabled={docNodes.length >= LIMITS.maxNodes} onAdd={(type) => addComponent(type)} />}
        <PropertiesPanel selection={selection} readOnly={readOnly} onDeleted={() => setSelection({ nodeIds: [], edgeIds: [] })} />
      </div>
    </CanvasActionsContext.Provider>
  );
}

type DiagramProblems = Array<{ code: string; path: string; message: string }>;

/** Zoom −/%/+, fit and grid (spec 004 FR-019). */
function DiagramToolbar({ grid, onToggleGrid, children }: { grid: boolean; onToggleGrid: () => void; children?: ReactNode }) {
  const flow = useReactFlow();
  const { zoom } = useViewport();

  return (
    <div className="canvas-toolbar">
      {children ?? <span className="spacer" />}
      <button type="button" className="icon-button" aria-label="Zoom out" disabled={zoom <= LIMITS.minZoom + 0.001} onClick={() => void flow.zoomOut()}>
        <MinusIcon />
      </button>
      <span className="zoom-level" aria-label="Zoom level">
        {Math.round(zoom * 100)}%
      </span>
      <button type="button" className="icon-button" aria-label="Zoom in" disabled={zoom >= LIMITS.maxZoom - 0.001} onClick={() => void flow.zoomIn()}>
        <PlusIcon />
      </button>
      <button type="button" className="btn-ghost" onClick={() => void flow.fitView({ padding: 0.15 })}>
        <FitIcon />
        Fit
      </button>
      <button type="button" className="btn-ghost" aria-pressed={grid} onClick={onToggleGrid}>
        <GridIcon />
        Snap to grid
      </button>
    </div>
  );
}
