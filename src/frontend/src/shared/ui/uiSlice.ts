import { createAction, createSlice } from '@reduxjs/toolkit';

export const FORBIDDEN_TEXT = "You don't have permission to do that.";

/** Dispatched when the server answers 403 forbidden; the view is refreshed and a toast shown (FR-036). */
export const permissionDenied = createAction('ui/permissionDenied');

interface UiState {
  toast: string | null;
}

const uiSlice = createSlice({
  name: 'ui',
  initialState: { toast: null } as UiState,
  reducers: {
    toastDismissed(state) {
      state.toast = null;
    },
  },
  extraReducers: (builder) => {
    builder.addCase(permissionDenied, (state) => {
      state.toast = FORBIDDEN_TEXT;
    });
  },
});

export const { toastDismissed } = uiSlice.actions;
export const uiReducer = uiSlice.reducer;
export const selectToast = (state: { ui: UiState }) => state.ui.toast;
