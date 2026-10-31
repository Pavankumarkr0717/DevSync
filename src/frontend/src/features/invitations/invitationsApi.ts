import { baseApi } from '../../shared/api/baseApi';
import type { ReceivedInvitation, WorkspaceSummary } from '../../shared/api/types';

const invitationsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    listMyInvitations: build.query<ReceivedInvitation[], void>({
      query: () => '/invitations',
      providesTags: [{ type: 'Invitation', id: 'MINE' }],
    }),
    acceptInvitation: build.mutation<WorkspaceSummary, string>({
      query: (id) => ({ url: `/invitations/${encodeURIComponent(id)}/accept`, method: 'POST' }),
      invalidatesTags: [
        { type: 'Invitation', id: 'MINE' },
        { type: 'Workspace', id: 'LIST' },
      ],
    }),
    declineInvitation: build.mutation<void, string>({
      query: (id) => ({ url: `/invitations/${encodeURIComponent(id)}/decline`, method: 'POST' }),
      invalidatesTags: [{ type: 'Invitation', id: 'MINE' }],
    }),
  }),
});

export const { useListMyInvitationsQuery, useAcceptInvitationMutation, useDeclineInvitationMutation } = invitationsApi;
