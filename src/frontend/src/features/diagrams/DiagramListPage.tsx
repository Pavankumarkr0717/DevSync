import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { toApiError } from '../../shared/api/problem';
import type { DiagramSummary } from '../../shared/api/types';
import { formatDate } from '../../shared/text';
import { EmptyState } from '../../shared/ui/EmptyState';
import { ErrorMessage } from '../../shared/ui/ErrorMessage';
import { DiagramIcon, PencilIcon, PlusIcon, TrashIcon } from '../../shared/ui/icons';
import { Loading } from '../../shared/ui/Loading';
import { WorkspaceNav } from '../workspaces/WorkspaceNav';
import { WorkspaceNotFound } from '../workspaces/WorkspaceNotFound';
import { useGetWorkspaceQuery } from '../workspaces/workspacesApi';
import { MAX_NAME, nameErrorText, nameProblem } from './diagramNames';
import { useCreateDiagramMutation, useDeleteDiagramMutation, useListDiagramsQuery, useRenameDiagramMutation } from './diagramsApi';

const counts = (d: Pick<DiagramSummary, 'nodeCount' | 'edgeCount'>) =>
  `${d.nodeCount} component${d.nodeCount === 1 ? '' : 's'} · ${d.edgeCount} connection${d.edgeCount === 1 ? '' : 's'}`;

/** The workspace's diagrams (spec 004 US1, US4; contracts/ui-routes.md "Diagram list"). */
export function DiagramListPage() {
  const { workspaceId = '' } = useParams();
  const navigate = useNavigate();
  const workspace = useGetWorkspaceQuery(workspaceId);
  const list = useListDiagramsQuery(workspaceId, { refetchOnFocus: true, skip: !workspace.data });
  const [create, creating] = useCreateDiagramMutation();
  const [creatingOpen, setCreatingOpen] = useState(false);
  const [renamingId, setRenamingId] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<DiagramSummary | null>(null);

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
  const canEdit = role === 'owner' || role === 'editor';
  const canDelete = role === 'owner';
  const diagrams = [...(list.data ?? [])].sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));

  const newButton = canEdit && (
    <button type="button" className="btn-primary" onClick={() => setCreatingOpen(true)}>
      <PlusIcon />
      New diagram
    </button>
  );

  return (
    <section>
      <WorkspaceNav workspaceId={workspaceId} name={workspace.data.name} current="diagrams" />
      <div className="page-header">
        <div>
          <h1>Diagrams</h1>
          <p className="meta">Your role: {role}</p>
        </div>
        {!creatingOpen && newButton}
      </div>

      {creatingOpen && (
        <NameForm
          label="New diagram name"
          submitText="Create"
          busy={creating.isLoading}
          onCancel={() => setCreatingOpen(false)}
          onSubmit={async (name) => {
            const created = await create({ workspaceId, name }).unwrap();
            navigate(`/workspaces/${workspaceId}/diagrams/${created.id}`);
          }}
        />
      )}

      {list.isLoading && <Loading />}
      {list.error && <ErrorMessage error={list.error} />}

      {list.data && diagrams.length === 0 && !creatingOpen && (
        <EmptyState icon={<DiagramIcon />} title="No diagrams yet" action={newButton || undefined}>
          <p>Diagrams show how your system&apos;s components connect.</p>
        </EmptyState>
      )}

      {diagrams.length > 0 && (
        <ul className="card-grid" data-testid="diagram-list" aria-label="Diagrams">
          {diagrams.map((d) => (
            <li key={d.id} className="board-card">
              <Link to={`/workspaces/${workspaceId}/diagrams/${d.id}`} className="card-preview" tabIndex={-1} aria-hidden="true">
                <span className="swatch pastel-blue" />
                <span className="swatch pastel-purple" />
                <span className="swatch pastel-green" />
              </Link>
              {renamingId === d.id ? (
                <div className="card-body">
                  <RenameForm workspaceId={workspaceId} diagram={d} onDone={() => setRenamingId(null)} />
                </div>
              ) : (
                <div className="card-body">
                  <div>
                    <Link to={`/workspaces/${workspaceId}/diagrams/${d.id}`} className="card-title">
                      {d.name}
                    </Link>
                    <div className="meta">{counts(d)}</div>
                    <div className="meta">Edited {formatDate(d.updatedAt)}</div>
                  </div>
                  <div className="nav">
                    {canEdit && (
                      <button type="button" className="icon-button" aria-label={`Rename ${d.name}`} onClick={() => setRenamingId(d.id)}>
                        <PencilIcon size={16} />
                      </button>
                    )}
                    {canDelete && (
                      <button type="button" className="icon-button" aria-label={`Delete ${d.name}`} onClick={() => setDeleting(d)}>
                        <TrashIcon size={16} />
                      </button>
                    )}
                  </div>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}

      {deleting && <DeleteDiagramDialog workspaceId={workspaceId} diagram={deleting} onClose={() => setDeleting(null)} />}
    </section>
  );
}

interface NameFormProps {
  label: string;
  submitText: string;
  initial?: string;
  busy: boolean;
  onSubmit: (name: string) => Promise<unknown>;
  onCancel: () => void;
}

/** A one-field name form with the contract's inline errors. */
function NameForm({ label, submitText, initial = '', busy, onSubmit, onCancel }: NameFormProps) {
  const [name, setName] = useState(initial);
  const [error, setError] = useState<string | null>(null);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const problem = nameProblem(name);
    if (problem) {
      setError(problem);
      return;
    }
    try {
      await onSubmit(name.trim());
    } catch (e) {
      setError(nameErrorText(e));
    }
  };

  return (
    <form className="inline-form" onSubmit={(e) => void submit(e)}>
      <input
        aria-label={label}
        value={name}
        maxLength={MAX_NAME + 20}
        autoFocus
        aria-invalid={error ? true : undefined}
        onChange={(e) => {
          setName(e.target.value);
          setError(null);
        }}
        onKeyDown={(e) => {
          if (e.key === 'Escape') {
            onCancel();
          }
        }}
      />
      <button type="submit" className="btn-primary" disabled={busy}>
        {submitText}
      </button>
      <button type="button" onClick={onCancel}>
        Cancel
      </button>
      {error && (
        <p role="alert" className="field-error">
          {error}
        </p>
      )}
    </form>
  );
}

function RenameForm({ workspaceId, diagram, onDone }: { workspaceId: string; diagram: DiagramSummary; onDone: () => void }) {
  const [rename, renaming] = useRenameDiagramMutation();
  return (
    <NameForm
      label={`New name for ${diagram.name}`}
      submitText="Rename"
      initial={diagram.name}
      busy={renaming.isLoading}
      onCancel={onDone}
      onSubmit={async (name) => {
        if (name !== diagram.name) {
          await rename({ workspaceId, diagramId: diagram.id, name }).unwrap();
        }
        onDone();
      }}
    />
  );
}

/** Confirms a permanent delete, naming the diagram and how many components go with it (FR-005). */
function DeleteDiagramDialog({ workspaceId, diagram, onClose }: { workspaceId: string; diagram: DiagramSummary; onClose: () => void }) {
  const [remove, removing] = useDeleteDiagramMutation();
  const [error, setError] = useState<string | null>(null);
  const what = diagram.nodeCount > 0 ? `${diagram.name} and its ${diagram.nodeCount} component${diagram.nodeCount === 1 ? '' : 's'}` : diagram.name;

  return (
    <div className="dialog-backdrop">
      <div
        role="alertdialog"
        aria-modal="true"
        aria-label={`Delete ${diagram.name}`}
        className="dialog"
        onKeyDown={(event) => {
          if (event.key === 'Escape') {
            onClose();
          }
        }}
      >
        <p>Delete {what}? This can&apos;t be undone.</p>
        {error && (
          <p role="alert" className="field-error">
            {error}
          </p>
        )}
        <div className="dialog-actions">
          <button type="button" autoFocus onClick={onClose}>
            Cancel
          </button>
          <button
            type="button"
            className="danger"
            disabled={removing.isLoading}
            onClick={async () => {
              try {
                await remove({ workspaceId, diagramId: diagram.id }).unwrap();
                onClose();
              } catch (e) {
                const api = toApiError(e);
                // Already gone counts as done.
                if (api.status === 404) {
                  onClose();
                } else {
                  setError(api.detail ?? api.title);
                }
              }
            }}
          >
            Delete
          </button>
        </div>
      </div>
    </div>
  );
}
