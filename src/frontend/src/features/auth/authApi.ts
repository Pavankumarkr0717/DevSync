import { baseApi } from '../../shared/api/baseApi';
import type { AuthSession, LoginRequest, RegisterRequest } from '../../shared/api/types';
import { signedIn, signedOut } from './authSlice';

const authApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    register: build.mutation<AuthSession, RegisterRequest>({
      query: (body) => ({ url: '/auth/register', method: 'POST', body }),
      async onQueryStarted(_args, { dispatch, queryFulfilled }) {
        const { data } = await queryFulfilled.catch(() => ({ data: null }));
        if (data) {
          dispatch(signedIn(data));
        }
      },
    }),
    login: build.mutation<AuthSession, LoginRequest>({
      query: (body) => ({ url: '/auth/login', method: 'POST', body }),
      async onQueryStarted(_args, { dispatch, queryFulfilled }) {
        const { data } = await queryFulfilled.catch(() => ({ data: null }));
        if (data) {
          dispatch(signedIn(data));
        }
      },
    }),
    logout: build.mutation<void, void>({
      query: () => ({ url: '/auth/logout', method: 'POST' }),
      async onQueryStarted(_args, { dispatch, queryFulfilled }) {
        // Sign out locally even if the request fails: the user asked to leave.
        await queryFulfilled.catch(() => undefined);
        dispatch(signedOut());
      },
    }),
    logoutAll: build.mutation<void, void>({
      query: () => ({ url: '/auth/logout-all', method: 'POST' }),
      async onQueryStarted(_args, { dispatch, queryFulfilled }) {
        await queryFulfilled.catch(() => undefined);
        dispatch(signedOut());
      },
    }),
  }),
});

export const { useRegisterMutation, useLoginMutation, useLogoutMutation, useLogoutAllMutation } = authApi;
