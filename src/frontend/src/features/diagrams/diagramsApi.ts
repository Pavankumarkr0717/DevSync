import { baseApi } from '../../shared/api/baseApi';
import type { Diagram, DiagramSummary, SaveDiagramResult } from '../../shared/api/types';

// Endpoints from specs/004-system-design-canvas/contracts/openapi.yaml.

export interface DiagramRef {
  workspaceId: string;
  diagramId: string;
}

const diagrams = (workspaceId: string) => `/workspaces/${encodeURIComponent(workspaceId)}/diagrams`;
const diagram = ({ workspaceId, diagramId }: DiagramRef) => `${diagrams(workspaceId)}/${encodeURIComponent(diagramId)}`;

export const diagramsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    listDiagrams: build.query<DiagramSummary[], string>({
      query: (workspaceId) => diagrams(workspaceId),
      providesTags: (_result, _error, workspaceId) => [{ type: 'DiagramList', id: workspaceId }],
    }),
    getDiagram: build.query<Diagram, DiagramRef>({
      query: (ref) => diagram(ref),
      providesTags: (_result, _error, { diagramId }) => [{ type: 'Diagram', id: diagramId }],
    }),
    createDiagram: build.mutation<DiagramSummary, { workspaceId: string; name: string }>({
      query: ({ workspaceId, name }) => ({ url: diagrams(workspaceId), method: 'POST', body: { name } }),
      invalidatesTags: (_result, _error, { workspaceId }) => [{ type: 'DiagramList', id: workspaceId }],
    }),
    renameDiagram: build.mutation<DiagramSummary, DiagramRef & { name: string }>({
      query: ({ name, ...ref }) => ({ url: `${diagram(ref)}/rename`, method: 'POST', body: { name } }),
      invalidatesTags: (_result, _error, { workspaceId, diagramId }) => [
        { type: 'DiagramList', id: workspaceId },
        { type: 'Diagram', id: diagramId },
      ],
    }),
    deleteDiagram: build.mutation<void, DiagramRef>({
      query: (ref) => ({ url: diagram(ref), method: 'DELETE' }),
      invalidatesTags: (_result, _error, { workspaceId }) => [{ type: 'DiagramList', id: workspaceId }],
    }),
    /** Saves the whole document if the diagram is still at `revision` (If-Match). */
    saveDiagramDocument: build.mutation<SaveDiagramResult, DiagramRef & { revision: number; json: string; replaceUnsupported?: boolean }>({
      query: ({ revision, json, replaceUnsupported, ...ref }) => ({
        url: `${diagram(ref)}/document${replaceUnsupported ? '?replaceUnsupported=true' : ''}`,
        method: 'PUT',
        body: json,
        headers: { 'Content-Type': 'application/json', 'If-Match': `"${revision}"` },
      }),
      invalidatesTags: (_result, _error, { workspaceId }) => [{ type: 'DiagramList', id: workspaceId }],
    }),
  }),
});

export const {
  useListDiagramsQuery,
  useGetDiagramQuery,
  useCreateDiagramMutation,
  useRenameDiagramMutation,
  useDeleteDiagramMutation,
} = diagramsApi;
