import { createEntityAdapter, createSlice, type EntityState, type PayloadAction } from '@reduxjs/toolkit';
import type { DiagramDocument, DiagramEdgeDoc, DiagramNodeDoc, DiagramProblem, Viewport } from '../../shared/api/types';
import type { SaveFailure, SaveState } from '../../shared/editing/autosave';
import { sessionEnded, signedOut } from '../auth/authSlice';

/**
 * The open diagram's persisted data, normalized (spec 004 research R9). Only committed changes land
 * here (drag stop, resize end, connect, delete, panel edits); in-flight drags, selection and the live
 * viewport stay in React Flow, so a drag is one change and one save.
 */

const byId = (a: { id: string }, b: { id: string }) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0);
export const nodesAdapter = createEntityAdapter<DiagramNodeDoc>({ sortComparer: byId });
export const edgesAdapter = createEntityAdapter<DiagramEdgeDoc>({ sortComparer: byId });

export interface DiagramEditorState {
  diagramId: string | null;
  nodes: EntityState<DiagramNodeDoc, string>;
  edges: EntityState<DiagramEdgeDoc, string>;
  /** The view stored with the diagram; the live view is read from React Flow when saving. */
  viewport: Viewport | null;
  /** Bumped when the whole document is replaced (load, reload latest, draft restore). */
  loadVersion: number;
  /** Bumped on every committed change. */
  changeVersion: number;
  status: SaveState;
  reason?: SaveFailure;
  baseRevision: number;
  currentRevision?: number;
  problems: DiagramProblem[];
}

const initialState: DiagramEditorState = {
  diagramId: null,
  nodes: nodesAdapter.getInitialState(),
  edges: edgesAdapter.getInitialState(),
  viewport: null,
  loadVersion: 0,
  changeVersion: 0,
  status: 'saved',
  baseRevision: 0,
  problems: [],
};

const slice = createSlice({
  name: 'diagramEditor',
  initialState,
  reducers: {
    diagramLoaded(state, action: PayloadAction<{ diagramId: string; document: DiagramDocument; revision: number }>) {
      const { diagramId, document, revision } = action.payload;
      state.diagramId = diagramId;
      nodesAdapter.setAll(state.nodes, document.nodes);
      edgesAdapter.setAll(state.edges, document.edges);
      state.viewport = document.viewport;
      state.loadVersion++;
      state.status = 'saved';
      state.reason = undefined;
      state.currentRevision = undefined;
      state.baseRevision = revision;
      state.problems = [];
    },
    /** Replaces the content (draft restore) without resetting the save state. */
    documentReplaced(state, action: PayloadAction<DiagramDocument>) {
      nodesAdapter.setAll(state.nodes, action.payload.nodes);
      edgesAdapter.setAll(state.edges, action.payload.edges);
      state.viewport = action.payload.viewport;
      state.loadVersion++;
      state.changeVersion++;
    },
    nodeAdded(state, action: PayloadAction<DiagramNodeDoc>) {
      nodesAdapter.addOne(state.nodes, action.payload);
      state.changeVersion++;
    },
    nodesMoved(state, action: PayloadAction<Array<{ id: string; position: { x: number; y: number } }>>) {
      nodesAdapter.updateMany(
        state.nodes,
        action.payload.map((m) => ({ id: m.id, changes: { position: m.position } })),
      );
      state.changeVersion++;
    },
    nodeResized(state, action: PayloadAction<{ id: string; position: { x: number; y: number }; size: { width: number; height: number } }>) {
      const { id, position, size } = action.payload;
      nodesAdapter.updateOne(state.nodes, { id, changes: { position, size } });
      state.changeVersion++;
    },
    nodeUpdated(state, action: PayloadAction<{ id: string; changes: Partial<Omit<DiagramNodeDoc, 'id'>> }>) {
      nodesAdapter.updateOne(state.nodes, action.payload);
      state.changeVersion++;
    },
    edgeAdded(state, action: PayloadAction<DiagramEdgeDoc>) {
      edgesAdapter.addOne(state.edges, action.payload);
      state.changeVersion++;
    },
    edgeUpdated(state, action: PayloadAction<{ id: string; label: string }>) {
      edgesAdapter.updateOne(state.edges, { id: action.payload.id, changes: { label: action.payload.label } });
      state.changeVersion++;
    },
    /** Deletes nodes with every edge attached to them, plus any listed edges (FR-017). */
    elementsDeleted(state, action: PayloadAction<{ nodeIds?: string[]; edgeIds?: string[] }>) {
      const nodeIds = new Set(action.payload.nodeIds ?? []);
      const attached = Object.values(state.edges.entities)
        .filter((e) => nodeIds.has(e.source) || nodeIds.has(e.target))
        .map((e) => e.id);
      nodesAdapter.removeMany(state.nodes, [...nodeIds]);
      edgesAdapter.removeMany(state.edges, [...(action.payload.edgeIds ?? []), ...attached]);
      state.changeVersion++;
    },
    saveStateChanged(
      state,
      action: PayloadAction<{ status: SaveState; reason?: SaveFailure; baseRevision?: number; currentRevision?: number }>,
    ) {
      const { status, reason, baseRevision, currentRevision } = action.payload;
      state.status = status;
      state.reason = status === 'failed' ? reason : undefined;
      state.currentRevision = status === 'failed' && reason === 'conflict' ? currentRevision : undefined;
      if (baseRevision !== undefined) {
        state.baseRevision = baseRevision;
      }
      if (status !== 'failed') {
        state.problems = [];
      }
    },
    problemsReceived(state, action: PayloadAction<DiagramProblem[]>) {
      state.problems = action.payload;
    },
  },
  extraReducers: (builder) => {
    builder.addCase(signedOut, () => initialState);
    builder.addCase(sessionEnded, () => initialState);
  },
});

export const {
  diagramLoaded,
  documentReplaced,
  nodeAdded,
  nodesMoved,
  nodeResized,
  nodeUpdated,
  edgeAdded,
  edgeUpdated,
  elementsDeleted,
  saveStateChanged,
  problemsReceived,
} = slice.actions;
export const diagramEditorReducer = slice.reducer;

type Root = { diagramEditor: DiagramEditorState };
const nodeSelectors = nodesAdapter.getSelectors((s: Root) => s.diagramEditor.nodes);
const edgeSelectors = edgesAdapter.getSelectors((s: Root) => s.diagramEditor.edges);
export const selectDiagramNodes = nodeSelectors.selectAll;
export const selectDiagramEdges = edgeSelectors.selectAll;
export const selectDiagramNode = nodeSelectors.selectById;
export const selectDiagramEdge = edgeSelectors.selectById;
export const selectDiagramEditor = (s: Root) => s.diagramEditor;
