import { useState } from 'react';
import { Link } from 'react-router';
import { formatDate } from '../../shared/text';
import { ErrorMessage } from '../../shared/ui/ErrorMessage';
import { Loading } from '../../shared/ui/Loading';
import { Pager } from '../../shared/ui/Pager';
import { useListWorkspacesQuery } from './workspacesApi';

export const PAGE_SIZE = 20;

export function WorkspaceListPage() {
  const [page, setPage] = useState(1);
  const { data, error, isLoading } = useListWorkspacesQuery({ page, pageSize: PAGE_SIZE });

  return (
    <section>
      <div className="toolbar">
        <h1>Your workspaces</h1>
        <Link to="/workspaces/new">New workspace</Link>
      </div>

      {isLoading && <Loading />}
      {error && <ErrorMessage error={error} />}

      {data && data.totalCount === 0 && (
        <p>
          You have no workspaces yet. <Link to="/workspaces/new">Create your first workspace</Link>.
        </p>
      )}

      {data && data.items.length > 0 && (
        <>
          <ul className="workspace-list">
            {data.items.map((workspace) => (
              <li key={workspace.id}>
                <Link to={`/workspaces/${workspace.id}`}>{workspace.name}</Link>
                <div className="meta">
                  {workspace.role} · created {formatDate(workspace.createdAt)}
                </div>
              </li>
            ))}
          </ul>
          <Pager page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} onPageChange={setPage} />
        </>
      )}
    </section>
  );
}
