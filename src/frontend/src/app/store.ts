import { combineReducers, configureStore, createListenerMiddleware, isAnyOf } from '@reduxjs/toolkit';
import { authReducer, sessionEnded, signedIn, signedOut } from '../features/auth/authSlice';
import { baseApi } from '../shared/api/baseApi';
import { permissionDenied, uiReducer } from '../shared/ui/uiSlice';

const rootReducer = combineReducers({
  auth: authReducer,
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

  // A 403 means our view of the caller's role is stale: refetch.
  listener.startListening({
    actionCreator: permissionDenied,
    effect: (_action, api) => {
      api.dispatch(baseApi.util.invalidateTags(['Workspace', 'Invitation']));
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
