import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { toApiError } from '../../shared/api/problem';
import { formatDate } from '../../shared/text';
import { ErrorMessage } from '../../shared/ui/ErrorMessage';
import { Loading } from '../../shared/ui/Loading';
import { EditWorkspaceForm } from './EditWorkspaceForm';
import { InvitationsPanel } from './InvitationsPanel';
import { MembersPanel } from './MembersPanel';
import { useGetWorkspaceQuery } from './workspacesApi';

export const NOT_FOUND_TEXT = 'Workspace not found';

export function WorkspaceDetailsPage() {
  const { workspaceId = '' } = useParams();
  const { data: workspace, error, isLoading } = useGetWorkspaceQuery(workspaceId);
  const [editing, setEditing] = useState(false);

  if (isLoading) {
    return <Loading />;
  }

  if (error) {
    // Missing and not-a-member look the same, by design.
    if (toApiError(error).status === 404) {
      return (
        <section>
          <h1>{NOT_FOUND_TEXT}</h1>
          <p>
            It may not exist, or you may not be a member. <Link to="/workspaces">Back to your workspaces</Link>
          </p>
        </section>
      );
    }
    return <ErrorMessage error={error} />;
  }

  if (!workspace) {
    return null;
  }

  const canEdit = workspace.myRole === 'owner' || workspace.myRole === 'editor';

  return (
    <section>
      <p>
        <Link to="/workspaces">← Your workspaces</Link>
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
      <dl>
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
