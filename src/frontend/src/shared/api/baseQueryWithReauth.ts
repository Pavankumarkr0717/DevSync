import {
  fetchBaseQuery,
  type BaseQueryFn,
  type FetchArgs,
  type FetchBaseQueryError,
} from '@reduxjs/toolkit/query/react';
import { sessionEnded, type AuthState } from '../../features/auth/authSlice';
import { permissionDenied } from '../ui/uiSlice';
import { CLIENT_HEADER, CLIENT_HEADER_VALUE, refreshSession } from './session';

const rawBaseQuery = fetchBaseQuery({
  // Same-origin, absolute so it also works under the test environment.
  baseUrl: `${window.location.origin}/api`,
  credentials: 'same-origin',
  prepareHeaders: (headers, { getState }) => {
    const token = (getState() as { auth: AuthState }).auth.accessToken;
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }
    headers.set(CLIENT_HEADER, CLIENT_HEADER_VALUE);
    return headers;
  },
});

function errorCode(error: FetchBaseQueryError | undefined): string | undefined {
  const data = error?.data;
  return typeof data === 'object' && data !== null && 'code' in data ? String((data as { code: unknown }).code) : undefined;
}

function isAuthRequest(args: string | FetchArgs): boolean {
  const url = typeof args === 'string' ? args : args.url;
  return url.startsWith('/auth/');
}

/**
 * Sends the in-memory access token. On 401 token_expired it renews the session once (shared by
 * all concurrent requests) and retries; any other 401 ends the session. A 403 refreshes the view
 * and shows "You don't have permission to do that".
 */
export const baseQueryWithReauth: BaseQueryFn<string | FetchArgs, unknown, FetchBaseQueryError> = async (
  args,
  api,
  extraOptions,
) => {
  let result = await rawBaseQuery(args, api, extraOptions);

  if (result.error?.status === 401 && !isAuthRequest(args)) {
    if (errorCode(result.error) === 'token_expired') {
      const outcome = await refreshSession(api.dispatch);
      if (outcome === 'refreshed') {
        result = await rawBaseQuery(args, api, extraOptions);
        if (result.error?.status === 401) {
          api.dispatch(sessionEnded());
        }
      }
    } else {
      api.dispatch(sessionEnded());
    }
  } else if (result.error?.status === 403 && errorCode(result.error) === 'forbidden') {
    api.dispatch(permissionDenied());
  }

  return result;
};
