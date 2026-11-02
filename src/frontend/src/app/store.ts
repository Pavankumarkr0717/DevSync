import { combineReducers, configureStore, createListenerMiddleware, isAnyOf } from '@reduxjs/toolkit';
import { authReducer, sessionEnded, signedIn, signedOut } from '../features/auth/authSlice';
import { clearUserDrafts } from '../shared/editing/drafts';
import { diagramEditorReducer } from '../features/diagrams/diagramEditorSlice';
import { editorReducer } from '../features/files/editorSlice';
import { presenceReducer } from '../features/presence/presenceSlice';
import { clearUserTabs } from '../features/files/tabStorage';
import { baseApi } from '../shared/api/baseApi';
import { permissionDenied, uiReducer } from '../shared/ui/uiSlice';

const rootReducer = combineReducers({
  auth: authReducer,
  editor: editorReducer,
  diagramEditor: diagramEditorReducer,
  presence: presenceReducer,
  ui: uiReducer,
  [baseApi.reducerPath]: baseApi.reducer,
});

export type RootState = ReturnType<typeof rootReducer>;

export function makeStore(preloadedState?: Partial<RootState>) {
  const listener = createListenerMiddleware();

  // Cached data belongs to one account: drop it whenever the account changes or the session ends.
  listener.startListening({
    matcher: isAnyOf(signedIn, signedOut, sessionEnded),
    effect: (_action, api) => {
      api.dispatch(baseApi.util.resetApiState());
    },
  });

  // Signing out on this browser removes that account's unsaved drafts and remembered tabs (spec 003
  // FR-037). An ended session keeps them: that is exactly when they are needed.
  listener.startListening({
    actionCreator: signedOut,
    effect: (_action, api) => {
      const userId = (api.getOriginalState() as RootState).auth.user?.id;
      if (userId) {
        clearUserDrafts(userId);
        clearUserTabs(userId);
      }
    },
  });

  // A 403 means our view of the caller's role is stale: refetch.
  listener.startListening({
    actionCreator: permissionDenied,
    effect: (_action, api) => {
      api.dispatch(baseApi.util.invalidateTags(['Workspace', 'Invitation', 'FileTree', 'DiagramList']));
    },
  });

  return configureStore({
    reducer: rootReducer,
    preloadedState,
    middleware: (getDefault) => getDefault().prepend(listener.middleware).concat(baseApi.middleware),
  });
}

export type AppStore = ReturnType<typeof makeStore>;
export type AppDispatch = AppStore['dispatch'];
