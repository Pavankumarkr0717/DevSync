import { baseApi } from '../../shared/api/baseApi';
import type {
  AssignableRole,
  CreateWorkspaceRequest,
  Member,
  WorkspaceDetails,
  WorkspaceInvitation,
  WorkspacePage,
} from '../../shared/api/types';

// The API cache is reset whenever the signed-in account changes (see app/store.ts),
// so cached results are never shared between users.
export interface ListWorkspacesArgs {
  page: number;
  pageSize: number;
}

export const workspacesApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    createWorkspace: build.mutation<WorkspaceDetails, CreateWorkspaceRequest>({
      query: (body) => ({ url: '/workspaces', method: 'POST', body }),
      invalidatesTags: [{ type: 'Workspace', id: 'LIST' }],
    }),
    listWorkspaces: build.query<WorkspacePage, ListWorkspacesArgs>({
      query: (args) => ({ url: '/workspaces', params: { page: args.page, pageSize: args.pageSize } }),
      providesTags: [{ type: 'Workspace', id: 'LIST' }],
    }),
    getWorkspace: build.query<WorkspaceDetails, string>({
      query: (id) => `/workspaces/${encodeURIComponent(id)}`,
      providesTags: (_result, _error, id) => [{ type: 'Workspace', id }],
    }),
    updateWorkspace: build.mutation<WorkspaceDetails, { id: string } & CreateWorkspaceRequest>({
      query: ({ id, ...body }) => ({ url: `/workspaces/${encodeURIComponent(id)}`, method: 'PUT', body }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: 'Workspace', id },
        { type: 'Workspace', id: 'LIST' },
      ],
    }),
    changeMemberRole: build.mutation<Member, { workspaceId: string; userId: string; role: AssignableRole }>({
      query: ({ workspaceId, userId, role }) => ({
        url: `/workspaces/${encodeURIComponent(workspaceId)}/members/${encodeURIComponent(userId)}`,
        method: 'PATCH',
        body: { role },
      }),
      invalidatesTags: (_result, _error, { workspaceId }) => [{ type: 'Workspace', id: workspaceId }],
    }),
    removeMember: build.mutation<void, { workspaceId: string; userId: string }>({
      query: ({ workspaceId, userId }) => ({
        url: `/workspaces/${encodeURIComponent(workspaceId)}/members/${encodeURIComponent(userId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { workspaceId }) => [{ type: 'Workspace', id: workspaceId }],
    }),
    listWorkspaceInvitations: build.query<WorkspaceInvitation[], string>({
      query: (workspaceId) => `/workspaces/${encodeURIComponent(workspaceId)}/invitations`,
      providesTags: (_result, _error, workspaceId) => [{ type: 'Invitation', id: workspaceId }],
    }),
    inviteMember: build.mutation<WorkspaceInvitation, { workspaceId: string; email: string; role: AssignableRole }>({
      query: ({ workspaceId, ...body }) => ({
        url: `/workspaces/${encodeURIComponent(workspaceId)}/invitations`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_result, _error, { workspaceId }) => [{ type: 'Invitation', id: workspaceId }],
    }),
    revokeInvitation: build.mutation<void, { workspaceId: string; invitationId: string }>({
      query: ({ workspaceId, invitationId }) => ({
        url: `/workspaces/${encodeURIComponent(workspaceId)}/invitations/${encodeURIComponent(invitationId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { workspaceId }) => [{ type: 'Invitation', id: workspaceId }],
    }),
  }),
});

export const {
  useCreateWorkspaceMutation,
  useListWorkspacesQuery,
  useGetWorkspaceQuery,
  useUpdateWorkspaceMutation,
  useChangeMemberRoleMutation,
  useRemoveMemberMutation,
  useListWorkspaceInvitationsQuery,
  useInviteMemberMutation,
  useRevokeInvitationMutation,
} = workspacesApi;
