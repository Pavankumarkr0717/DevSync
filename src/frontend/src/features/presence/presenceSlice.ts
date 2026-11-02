import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import { sessionEnded, signedOut } from '../auth/authSlice';
import type { ConnectionStatus, OnlineUsers, PresenceUser } from './presenceTypes';

/**
 * Live presence of the workspace this tab shows (spec 005 data-model "Frontend state"). The list
 * only ever comes from server snapshots; a snapshot is applied when it is for the current
 * workspace and newer than the one shown, so out-of-order and duplicate events are harmless.
 */
export interface PresenceState {
  status: ConnectionStatus;
  workspaceId: string | null;
  /** Version of the list shown. Reset to 0 on every join request. */
  version: number;
  /** Version of the last join result: notifications at or below it describe the past. */
  baseline: number;
  users: PresenceUser[];
  /** True once the current workspace's join has been answered, so the bar has something to show. */
  joined: boolean;
  revoked: boolean;
  /** Increments each time the connection comes back after a loss ("Connection restored."). */
  restored: number;
}

export const initialPresenceState: PresenceState = {
  status: 'idle',
  workspaceId: null,
  version: 0,
  baseline: Number.MAX_SAFE_INTEGER,
  users: [],
  joined: false,
  revoked: false,
  restored: 0,
};

const presenceSlice = createSlice({
  name: 'presence',
  initialState: initialPresenceState,
  reducers: {
    statusChanged(state, action: PayloadAction<ConnectionStatus>) {
      if (action.payload === 'connected' && (state.status === 'reconnecting' || state.status === 'failed')) {
        state.restored += 1;
      }
      state.status = action.payload;
    },
    joinRequested(state, action: PayloadAction<string>) {
      if (state.workspaceId !== action.payload) {
        state.users = [];
        state.joined = false;
        state.revoked = false;
      }
      state.workspaceId = action.payload;
      state.version = 0;
      state.baseline = Number.MAX_SAFE_INTEGER;
    },
    joinSucceeded(state, action: PayloadAction<OnlineUsers>) {
      const snapshot = action.payload;
      if (snapshot.workspaceId !== state.workspaceId) {
        return;
      }
      state.baseline = snapshot.version;
      state.joined = true;
      // An event that overtook the join result already holds a newer list.
      if (snapshot.version >= state.version) {
        state.version = snapshot.version;
        state.users = snapshot.users;
      }
    },
    joinRefused(state, action: PayloadAction<string>) {
      if (action.payload === state.workspaceId) {
        state.users = [];
        state.joined = false;
      }
    },
    snapshotReceived(state, action: PayloadAction<OnlineUsers>) {
      const snapshot = action.payload;
      if (snapshot.workspaceId === state.workspaceId && snapshot.version > state.version) {
        state.version = snapshot.version;
        state.users = snapshot.users;
      }
    },
    accessRevoked(state, action: PayloadAction<string>) {
      if (action.payload === state.workspaceId) {
        state.users = [];
        state.joined = false;
        state.revoked = true;
      }
    },
    presenceReset() {
      return initialPresenceState;
    },
  },
  extraReducers: (builder) => {
    builder.addCase(signedOut, () => initialPresenceState).addCase(sessionEnded, () => initialPresenceState);
  },
});

export const { statusChanged, joinRequested, joinSucceeded, joinRefused, snapshotReceived, accessRevoked, presenceReset } =
  presenceSlice.actions;
export const presenceReducer = presenceSlice.reducer;

type WithPresence = { presence: PresenceState };
export const selectPresence = (state: WithPresence) => state.presence;
