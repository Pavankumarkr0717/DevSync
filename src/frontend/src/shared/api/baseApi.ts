import { createApi } from '@reduxjs/toolkit/query/react';
import { baseQueryWithReauth } from './baseQueryWithReauth';

/** The single RTK Query API. Features add endpoints with injectEndpoints. */
export const baseApi = createApi({
  reducerPath: 'api',
  baseQuery: baseQueryWithReauth,
  tagTypes: ['Workspace', 'Invitation'],
  endpoints: () => ({}),
});
