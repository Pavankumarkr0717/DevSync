import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import type { SaveFailure, SaveState } from '../../shared/editing/autosave';
import { sessionEnded, signedOut } from '../auth/authSlice';

export type { SaveFailure, SaveState };

/**
 * Editor UI state (spec 003 research R9). Small and serializable on purpose: the text being
 * edited lives in the editor's per-file models, never here, so typing does not dispatch.
 */

export const MAX_OPEN_TABS = 20;



export interface FileEditState {
  status: SaveState;
  reason?: SaveFailure;
  /** The revision the editor's text is based on (sent as If-Match). */
  baseRevision: number;
  /** On a conflict: the revision now on the server. */
  currentRevision?: number;
}

export interface WorkspaceEditorState {
  openTabs: string[];
  activeId: string | null;
  expanded: Record<string, boolean>;
}

export interface EditorState {
  byWorkspace: Record<string, WorkspaceEditorState>;
  files: Record<string, FileEditState>;
}

const initialState: EditorState = { byWorkspace: {}, files: {} };

function workspace(state: EditorState, workspaceId: string): WorkspaceEditorState {
  return (state.byWorkspace[workspaceId] ??= { openTabs: [], activeId: null, expanded: {} });
}

const editorSlice = createSlice({
  name: 'editor',
  initialState,
  reducers: {
    /** Opens a file as the active tab; focuses its tab if it is already open. Ignored past the tab limit. */
    fileOpened(state, action: PayloadAction<{ workspaceId: string; itemId: string }>) {
      const ws = workspace(state, action.payload.workspaceId);
      if (!ws.openTabs.includes(action.payload.itemId)) {
        if (ws.openTabs.length >= MAX_OPEN_TABS) {
          return;
        }
        ws.openTabs.push(action.payload.itemId);
      }
      ws.activeId = action.payload.itemId;
    },
    tabActivated(state, action: PayloadAction<{ workspaceId: string; itemId: string }>) {
      const ws = workspace(state, action.payload.workspaceId);
      if (ws.openTabs.includes(action.payload.itemId)) {
        ws.activeId = action.payload.itemId;
      }
    },
    /** Closes tabs (and forgets their save state); the neighbour of the closed active tab becomes active. */
    tabsClosed(state, action: PayloadAction<{ workspaceId: string; itemIds: string[] }>) {
      const ws = workspace(state, action.payload.workspaceId);
      const closing = new Set(action.payload.itemIds);
      const activeIndex = ws.activeId ? ws.openTabs.indexOf(ws.activeId) : -1;
      ws.openTabs = ws.openTabs.filter((id) => !closing.has(id));
      if (ws.activeId && closing.has(ws.activeId)) {
        ws.activeId = ws.openTabs[Math.min(activeIndex, ws.openTabs.length - 1)] ?? null;
      }
      for (const id of closing) {
        delete state.files[id];
      }
    },
    tabsRestored(state, action: PayloadAction<{ workspaceId: string; itemIds: string[]; activeId: string | null }>) {
      const ws = workspace(state, action.payload.workspaceId);
      ws.openTabs = action.payload.itemIds.slice(0, MAX_OPEN_TABS);
      ws.activeId =
        action.payload.activeId && ws.openTabs.includes(action.payload.activeId)
          ? action.payload.activeId
          : (ws.openTabs[0] ?? null);
    },
    folderToggled(state, action: PayloadAction<{ workspaceId: string; folderId: string; expanded?: boolean }>) {
      const ws = workspace(state, action.payload.workspaceId);
      ws.expanded[action.payload.folderId] = action.payload.expanded ?? !ws.expanded[action.payload.folderId];
    },
    /** Starts tracking a file opened at a revision (or re-based after a reload). */
    fileLoaded(state, action: PayloadAction<{ itemId: string; revision: number }>) {
      state.files[action.payload.itemId] = { status: 'saved', baseRevision: action.payload.revision };
    },
    saveStateChanged(
      state,
      action: PayloadAction<{
        itemId: string;
        status: SaveState;
        reason?: SaveFailure;
        baseRevision?: number;
        currentRevision?: number;
      }>,
    ) {
      const { itemId, status, reason, baseRevision, currentRevision } = action.payload;
      const file = (state.files[itemId] ??= { status, baseRevision: baseRevision ?? 0 });
      file.status = status;
      file.reason = status === 'failed' ? reason : undefined;
      file.currentRevision = status === 'failed' && reason === 'conflict' ? currentRevision : undefined;
      if (baseRevision !== undefined) {
        file.baseRevision = baseRevision;
      }
    },
  },
  extraReducers: (builder) => {
    // Editor state belongs to one account.
    builder.addCase(signedOut, () => initialState);
    builder.addCase(sessionEnded, () => initialState);
  },
});

export const { fileOpened, tabActivated, tabsClosed, tabsRestored, folderToggled, fileLoaded, saveStateChanged } =
  editorSlice.actions;
export const editorReducer = editorSlice.reducer;

const noTabs: WorkspaceEditorState = { openTabs: [], activeId: null, expanded: {} };

export const selectWorkspaceEditor = (state: { editor: EditorState }, workspaceId: string) =>
  state.editor.byWorkspace[workspaceId] ?? noTabs;

export const selectFileEdit = (state: { editor: EditorState }, itemId: string | null) =>
  itemId ? state.editor.files[itemId] : undefined;
