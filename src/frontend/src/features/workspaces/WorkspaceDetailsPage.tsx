import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { toApiError } from '../../shared/api/problem';
import { formatDate } from '../../shared/text';
import { ErrorMessage } from '../../shared/ui/ErrorMessage';
import { Loading } from '../../shared/ui/Loading';
import { EditWorkspaceForm } from './EditWorkspaceForm';
import { InvitationsPanel } from './InvitationsPanel';
import { MembersPanel } from './MembersPanel';
import { WorkspaceNav } from './WorkspaceNav';
import { WorkspaceNotFound } from './WorkspaceNotFound';
import { useGetWorkspaceQuery } from './workspacesApi';

export function WorkspaceDetailsPage() {
  const { workspaceId = '' } = useParams();
  const { data: workspace, error, isLoading } = useGetWorkspaceQuery(workspaceId);
  const [editing, setEditing] = useState(false);

  if (isLoading) {
    return <Loading />;
  }

  if (error) {
    if (toApiError(error).status === 404) {
      return <WorkspaceNotFound />;
    }
    return <ErrorMessage error={error} />;
  }

  if (!workspace) {
    return null;
  }

  const canEdit = workspace.myRole === 'owner' || workspace.myRole === 'editor';

  return (
    <section>
      <WorkspaceNav workspaceId={workspace.id} name={workspace.name} current="overview" />
      <p className="toolbar">
        <Link to="/workspaces">← Your workspaces</Link>
        <span className="nav">
          <Link to={`/workspaces/${workspace.id}/files`}>Open files</Link>
          <Link to={`/workspaces/${workspace.id}/diagrams`}>Open diagrams</Link>
        </span>
      </p>
      {editing && canEdit ? (
        <EditWorkspaceForm workspace={workspace} onDone={() => setEditing(false)} />
      ) : (
        <>
          <div className="toolbar">
            <h1>{workspace.name}</h1>
            {canEdit && (
              <button type="button" onClick={() => setEditing(true)}>
                Edit
              </button>
            )}
          </div>
          <p>{workspace.description ?? <em>No description</em>}</p>
        </>
      )}
      <dl className="panel">
        <dt>Owner</dt>
        <dd>{workspace.owner.displayName}</dd>
        <dt>Your role</dt>
        <dd>{workspace.myRole}</dd>
        <dt>Created</dt>
        <dd>{formatDate(workspace.createdAt)}</dd>
      </dl>
      <MembersPanel workspace={workspace} />
      {workspace.myRole === 'owner' && <InvitationsPanel workspaceId={workspace.id} />}
    </section>
  );
}
