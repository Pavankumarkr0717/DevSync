import { baseApi } from '../../shared/api/baseApi';
import type { FileText, Item, ItemKind, SaveResult, Tree } from '../../shared/api/types';

// Endpoints from specs/003-file-explorer-editor/contracts/openapi.yaml. Items are addressed only
// by id within a workspace; nothing here ever builds a path from names.

export interface ItemRef {
  workspaceId: string;
  itemId: string;
}

export interface SaveFileArgs extends ItemRef {
  /** The revision the text is based on, sent as If-Match. */
  revision: number;
  text: string;
}

const files = (workspaceId: string) => `/workspaces/${encodeURIComponent(workspaceId)}`;
const item = ({ workspaceId, itemId }: ItemRef) => `${files(workspaceId)}/items/${encodeURIComponent(itemId)}`;
const content = ({ workspaceId, itemId }: ItemRef) => `${files(workspaceId)}/files/${encodeURIComponent(itemId)}/content`;

/** Revision from a strong ETag such as "7". */
export function revisionFromETag(etag: string | null): number {
  const revision = Number(etag?.replace(/"/g, ''));
  return Number.isInteger(revision) && revision > 0 ? revision : 0;
}

export const filesApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getTree: build.query<Tree, string>({
      query: (workspaceId) => `${files(workspaceId)}/tree`,
      providesTags: (_result, _error, workspaceId) => [{ type: 'FileTree', id: workspaceId }],
    }),
    getFileContent: build.query<FileText, ItemRef>({
      query: (ref) => ({
        url: content(ref),
        // Success is raw text with the revision in the ETag; errors are Problem Details JSON.
        responseHandler: async (response) =>
          response.ok
            ? { text: await response.text(), revision: revisionFromETag(response.headers.get('ETag')) }
            : response.json(),
      }),
      providesTags: (_result, _error, { itemId }) => [{ type: 'FileContent', id: itemId }],
    }),
    createItem: build.mutation<Item, { workspaceId: string; parentId: string | null; kind: ItemKind; name: string }>({
      query: ({ workspaceId, ...body }) => ({ url: `${files(workspaceId)}/items`, method: 'POST', body }),
      invalidatesTags: (_result, _error, { workspaceId }) => [{ type: 'FileTree', id: workspaceId }],
    }),
    renameItem: build.mutation<Item, ItemRef & { name: string }>({
      query: ({ name, ...ref }) => ({ url: `${item(ref)}/rename`, method: 'POST', body: { name } }),
      invalidatesTags: (_result, _error, { workspaceId }) => [{ type: 'FileTree', id: workspaceId }],
    }),
    moveItem: build.mutation<Item, ItemRef & { parentId: string | null }>({
      query: ({ parentId, ...ref }) => ({ url: `${item(ref)}/move`, method: 'POST', body: { parentId } }),
      invalidatesTags: (_result, _error, { workspaceId }) => [{ type: 'FileTree', id: workspaceId }],
    }),
    deleteItem: build.mutation<void, ItemRef>({
      query: (ref) => ({ url: item(ref), method: 'DELETE' }),
      invalidatesTags: (_result, _error, { workspaceId }) => [{ type: 'FileTree', id: workspaceId }],
    }),
    saveFileContent: build.mutation<SaveResult, SaveFileArgs>({
      query: ({ revision, text, ...ref }) => ({
        url: content(ref),
        method: 'PUT',
        body: text,
        headers: { 'Content-Type': 'text/plain; charset=utf-8', 'If-Match': `"${revision}"` },
      }),
      // Keep the cached copy in step with what was just saved instead of refetching it.
      async onQueryStarted({ text, ...ref }, { dispatch, queryFulfilled }) {
        try {
          const { data } = await queryFulfilled;
          dispatch(
            filesApi.util.updateQueryData('getFileContent', { workspaceId: ref.workspaceId, itemId: ref.itemId }, () => ({
              text,
              revision: data.revision,
            })),
          );
        } catch {
          // Failures are reported by the caller (autosave status).
        }
      },
    }),
  }),
});

export const {
  useGetTreeQuery,
  useGetFileContentQuery,
  useCreateItemMutation,
  useRenameItemMutation,
  useMoveItemMutation,
  useDeleteItemMutation,
  useSaveFileContentMutation,
} = filesApi;
