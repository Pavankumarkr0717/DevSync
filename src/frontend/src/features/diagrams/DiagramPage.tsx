import { lazy, Suspense, useState } from 'react';
import { Link, useParams } from 'react-router';
import { useAppDispatch, useAppSelector } from '../../app/hooks';
import { toApiError } from '../../shared/api/problem';
import type { Diagram } from '../../shared/api/types';
import { ErrorMessage } from '../../shared/ui/ErrorMessage';
import { AlertIcon, ArrowLeftIcon, ChevronRightIcon, CloudCheckIcon, CloudOffIcon, LogoMark, PencilIcon } from '../../shared/ui/icons';
import { Loading } from '../../shared/ui/Loading';
import { ThemeToggle } from '../../shared/ui/ThemeToggle';
import { toastShown } from '../../shared/ui/uiSlice';
import { WorkspaceNotFound } from '../workspaces/WorkspaceNotFound';
import { useGetWorkspaceQuery } from '../workspaces/workspacesApi';
import { selectDiagramEditor } from './diagramEditorSlice';
import { MAX_NAME, nameErrorText, nameProblem } from './diagramNames';
import { diagramsApi, useGetDiagramQuery, useRenameDiagramMutation } from './diagramsApi';
import { statusText } from './saveStatus';
import { EMPTY_DOCUMENT, serialize } from './document';

// React Flow is only downloaded when a diagram is opened (plan: bundle budget).
const DiagramEditor = lazy(() => import('./DiagramEditor'));

/** The full-window canvas page (spec 004 US2–US4; contracts/ui-routes.md "Canvas page layout"). */
export function DiagramPage() {
  const { workspaceId = '', diagramId = '' } = useParams();
  const workspace = useGetWorkspaceQuery(workspaceId);
  const diagram = useGetDiagramQuery({ workspaceId, diagramId }, { skip: !workspace.data, refetchOnMountOrArgChange: true });
  const listUrl = `/workspaces/${workspaceId}/diagrams`;

  if (workspace.isLoading || diagram.isLoading) {
    return <Loading />;
  }
  if (workspace.error) {
    return toApiError(workspace.error).status === 404 ? <WorkspaceNotFound /> : <ErrorMessage error={workspace.error} />;
  }
  if (diagram.error) {
    return toApiError(diagram.error).status === 404 ? (
      <section className="canvas-missing">
        <h1>Diagram not found</h1>
        <p>
          It may have been deleted. <Link to={listUrl}>Back to diagrams</Link>
        </p>
      </section>
    ) : (
      <ErrorMessage error={diagram.error} />
    );
  }
  if (!workspace.data || !diagram.data) {
    return null;
  }

  const role = workspace.data.myRole;
  const canEdit = role === 'owner' || role === 'editor';

  return (
    <section className="canvas-page" aria-label={`Diagram ${diagram.data.name}`}>
      <CanvasHeader workspaceId={workspaceId} diagram={diagram.data} canEdit={canEdit} listUrl={listUrl} />
      {diagram.data.document ? (
        <Suspense fallback={<Loading />}>
          <DiagramEditor
            key={diagram.data.id}
            workspaceId={workspaceId}
            diagram={diagram.data}
            readOnly={!canEdit}
            breadcrumbs={
              <nav aria-label="Breadcrumb" className="breadcrumbs">
                <Link to={`/workspaces/${workspaceId}`}>{workspace.data.name}</Link>
                <ChevronRightIcon size={14} />
                <Link to={listUrl}>Diagrams</Link>
                <ChevronRightIcon size={14} />
                <span aria-current="page">{diagram.data.name}</span>
              </nav>
            }
          />
        </Suspense>
      ) : (
        <UnsupportedBanner workspaceId={workspaceId} diagram={diagram.data} canEdit={canEdit} />
      )}
    </section>
  );
}

function CanvasHeader({ workspaceId, diagram, canEdit, listUrl }: { workspaceId: string; diagram: Diagram; canEdit: boolean; listUrl: string }) {
  const dispatch = useAppDispatch();
  const editor = useAppSelector(selectDiagramEditor);
  const [rename, renaming] = useRenameDiagramMutation();
  const [editingName, setEditingName] = useState<string | null>(null);
  const ours = editor.diagramId === diagram.id && diagram.document !== null;
  const readOnly = !canEdit || (ours && (editor.reason === 'forbidden' || editor.reason === 'deleted'));
  const status = ours ? statusText(editor, readOnly) : readOnly ? 'Read only' : '';
  const failed = ours && editor.status === 'failed';

  const commitName = async () => {
    const name = editingName ?? '';
    const problem = nameProblem(name);
    if (problem) {
      dispatch(toastShown(problem));
      return;
    }
    try {
      if (name.trim() !== diagram.name) {
        await rename({ workspaceId, diagramId: diagram.id, name: name.trim() }).unwrap();
      }
      setEditingName(null);
    } catch (e) {
      dispatch(toastShown(nameErrorText(e)));
    }
  };

  return (
    <header className="canvas-header">
      <Link to={listUrl} className="icon-button" aria-label="Back to diagrams">
        <ArrowLeftIcon />
      </Link>
      <Link to="/workspaces" aria-label="DevSync home">
        <LogoMark size={28} />
      </Link>
      {editingName !== null ? (
        <form
          className="canvas-title"
          onSubmit={(e) => {
            e.preventDefault();
            void commitName();
          }}
        >
          <input
            aria-label="Diagram name"
            value={editingName}
            maxLength={MAX_NAME + 20}
            autoFocus
            disabled={renaming.isLoading}
            onChange={(e) => setEditingName(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Escape') {
                setEditingName(null);
              }
            }}
          />
        </form>
      ) : (
        <h1 className="canvas-title">
          {diagram.name}
          {canEdit && (
            <button type="button" className="icon-button" aria-label="Rename diagram" onClick={() => setEditingName(diagram.name)}>
              <PencilIcon size={16} />
            </button>
          )}
        </h1>
      )}
      <span className="spacer" />
      {status && (
        <span
          className={`canvas-status${failed ? ' failed' : status === 'Saved' ? ' saved' : ''}`}
          data-testid="save-status"
          role="status"
        >
          {failed ? <CloudOffIcon size={18} /> : <CloudCheckIcon size={18} />}
          {status}
        </span>
      )}
      <ThemeToggle />
    </header>
  );
}

/** A stored format newer than this release (FR-031): explain, and let editors start over. */
function UnsupportedBanner({ workspaceId, diagram, canEdit }: { workspaceId: string; diagram: Diagram; canEdit: boolean }) {
  const dispatch = useAppDispatch();
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const format = diagram.unsupportedSchemaVersion ? ` (format ${diagram.unsupportedSchemaVersion})` : '';

  const startOver = async () => {
    setBusy(true);
    try {
      await dispatch(
        diagramsApi.endpoints.saveDiagramDocument.initiate({
          workspaceId,
          diagramId: diagram.id,
          revision: diagram.revision,
          json: serialize(EMPTY_DOCUMENT),
          replaceUnsupported: true,
        }),
      ).unwrap();
      dispatch(diagramsApi.util.invalidateTags([{ type: 'Diagram', id: diagram.id }]));
    } catch (e) {
      const api = toApiError(e);
      setError(api.detail ?? api.title);
      setBusy(false);
    }
  };

  return (
    <div className="canvas-body">
      <div className="notice unsupported-banner" role="alert" data-testid="unsupported-banner">
        <AlertIcon />
        <div>
          <p>This diagram was saved by a newer version of DevSync{format} and can&apos;t be opened here.</p>
          {error && <p className="field-error">{error}</p>}
          {canEdit && !confirming && (
            <button type="button" onClick={() => setConfirming(true)}>
              Start over
            </button>
          )}
          {canEdit && confirming && (
            <p>
              Replace it with an empty diagram? This can&apos;t be undone.{' '}
              <button type="button" className="danger" disabled={busy} onClick={() => void startOver()}>
                Replace with empty diagram
              </button>{' '}
              <button type="button" onClick={() => setConfirming(false)}>
                Cancel
              </button>
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
