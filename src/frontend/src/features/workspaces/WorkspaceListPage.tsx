import { useState } from 'react';
import { Link } from 'react-router';
import { useAppSelector } from '../../app/hooks';
import { formatDate } from '../../shared/text';
import { Avatar } from '../../shared/ui/Avatar';
import { EmptyState } from '../../shared/ui/EmptyState';
import { ErrorMessage } from '../../shared/ui/ErrorMessage';
import { GridIcon, PlusIcon } from '../../shared/ui/icons';
import { Loading } from '../../shared/ui/Loading';
import { Pager } from '../../shared/ui/Pager';
import { useListWorkspacesQuery } from './workspacesApi';

function greeting(now = new Date()): string {
  const hour = now.getHours();
  return hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';
}

export const PAGE_SIZE = 20;

export function WorkspaceListPage() {
  const [page, setPage] = useState(1);
  const { data, error, isLoading } = useListWorkspacesQuery({ page, pageSize: PAGE_SIZE });
  const firstName = useAppSelector((state) => state.auth.user?.displayName.split(' ')[0]);

  return (
    <section>
      <div className="page-header">
        <div>
          {firstName && (
            <p className="eyebrow">
              {greeting()}, {firstName}
            </p>
          )}
          <h1>Your workspaces</h1>
          <p className="meta">A little space for your next big idea.</p>
        </div>
        <Link to="/workspaces/new" className="btn btn-primary">
          <PlusIcon />
          New workspace
        </Link>
      </div>

      {isLoading && <Loading />}
      {error && <ErrorMessage error={error} />}

      {data && data.totalCount === 0 && (
        <EmptyState icon={<GridIcon />} title="No workspaces yet">
          <p>
            You have no workspaces yet. <Link to="/workspaces/new">Create your first workspace</Link>.
          </p>
        </EmptyState>
      )}

      {data && data.items.length > 0 && (
        <>
          <ul className="workspace-list">
            {data.items.map((workspace) => (
              <li key={workspace.id}>
                <Avatar id={workspace.id} name={workspace.name} size="lg" />
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
