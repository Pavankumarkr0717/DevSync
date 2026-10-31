import { useParams } from 'react-router';
import { useAppDispatch, useAppSelector } from '../../app/hooks';
import { toApiError } from '../../shared/api/problem';
import { ErrorMessage } from '../../shared/ui/ErrorMessage';
import { Loading } from '../../shared/ui/Loading';
import { toastShown } from '../../shared/ui/uiSlice';
import { WorkspaceNav } from '../workspaces/WorkspaceNav';
import { WorkspaceNotFound } from '../workspaces/WorkspaceNotFound';
import { useGetWorkspaceQuery } from '../workspaces/workspacesApi';
import { fileOpened, MAX_OPEN_TABS, selectWorkspaceEditor, tabActivated, tabsClosed } from './editorSlice';
import { EditorTabs } from './EditorTabs';
import { FileExplorer } from './FileExplorer';
import { EditorPane } from './EditorPane';
import { useGetTreeQuery } from './filesApi';
import { useFileSessions } from './useFileSessions';
import { useTabPersistence } from './useTabPersistence';

export const TAB_LIMIT_TEXT = 'Close a tab to open another file';

/** The files workspace: explorer beside tabs and the editor (spec 003 FR-030; contracts/ui-routes.md). */
export function FilesPage() {
  const { workspaceId = '' } = useParams();
  const dispatch = useAppDispatch();
  const workspace = useGetWorkspaceQuery(workspaceId);
  // Other people's changes appear when the window regains focus (research R12).
  const tree = useGetTreeQuery(workspaceId, { refetchOnFocus: true, refetchOnReconnect: true });
  const { openTabs, activeId } = useAppSelector((state) => selectWorkspaceEditor(state, workspaceId));
  const fileStates = useAppSelector((state) => state.editor.files);
  const sessions = useFileSessions(workspaceId, activeId);
  useTabPersistence(workspaceId, tree.data?.items);

  if (workspace.isLoading) {
    return <Loading />;
  }

  if (workspace.error) {
    return toApiError(workspace.error).status === 404 ? <WorkspaceNotFound /> : <ErrorMessage error={workspace.error} />;
  }

  if (!workspace.data) {
    return null;
  }

  const role = workspace.data.myRole;
  const items = tree.data?.items ?? [];
  const activeItem = items.find((i) => i.id === activeId && i.kind === 'file');

  function open(itemId: string) {
    if (!openTabs.includes(itemId) && openTabs.length >= MAX_OPEN_TABS) {
      dispatch(toastShown(TAB_LIMIT_TEXT));
      return;
    }
    dispatch(fileOpened({ workspaceId, itemId }));
  }

  /** Saves pending edits before closing; if that save fails the tab stays open showing why (FR-021). */
  async function closeTab(itemId: string) {
    const session = sessions.get(itemId);
    if (session?.controller.isDirty && fileStates[itemId]?.status !== 'failed') {
      const saved = await session.controller.settle();
      if (!saved) {
        dispatch(tabActivated({ workspaceId, itemId }));
        return;
      }
    }
    sessions.close(itemId);
    dispatch(tabsClosed({ workspaceId, itemIds: [itemId] }));
  }

  return (
    <section className="files-page">
      <WorkspaceNav workspaceId={workspaceId} name={workspace.data.name} current="files" />
      <div className="toolbar">
        <h1>{workspace.data.name}</h1>
        <span className="meta">Your role: {role}</span>
      </div>
      <div className="files-layout">
        <aside className="files-sidebar">
          {tree.isLoading ? (
            <Loading />
          ) : tree.error ? (
            toApiError(tree.error).status === 404 ? (
              <WorkspaceNotFound />
            ) : (
              <ErrorMessage error={tree.error} onRetry={() => void tree.refetch()} />
            )
          ) : (
            <FileExplorer
              workspaceId={workspaceId}
              items={items}
              role={role}
              onOpen={open}
              onDeleted={(itemIds) => {
                const closing = itemIds.filter((id) => openTabs.includes(id));
                closing.forEach((id) => sessions.discard(id));
                dispatch(tabsClosed({ workspaceId, itemIds: closing }));
              }}
            />
          )}
        </aside>
        <div className="editor-area">
          <EditorTabs
            workspaceId={workspaceId}
            items={items}
            openTabs={openTabs}
            activeId={activeId}
            onActivate={(itemId) => dispatch(tabActivated({ workspaceId, itemId }))}
            onClose={(itemId) => void closeTab(itemId)}
          />
          {activeItem ? (
            <EditorPane
              key={activeItem.id}
              workspaceId={workspaceId}
              item={activeItem}
              readOnly={role === 'viewer'}
              sessions={sessions}
            />
          ) : (
            <p className="meta editor-empty">Select a file to open it.</p>
          )}
        </div>
      </div>
    </section>
  );
}
