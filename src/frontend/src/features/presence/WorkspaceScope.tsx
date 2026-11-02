import { useEffect } from 'react';
import { Outlet, useParams } from 'react-router';
import { useAppStore } from '../../app/hooks';
import { getPresenceClient } from './presenceClient';

/**
 * Layout route for one workspace's pages (spec 005 research R9). Moving between the overview,
 * Files and Diagrams keeps a single join; switching workspaces swaps it on the same connection;
 * leaving the workspace pages closes the connection.
 */
export function WorkspaceScope() {
  const { workspaceId = '' } = useParams();
  const store = useAppStore();

  useEffect(() => {
    const client = getPresenceClient(store);
    client.join(workspaceId);
    return () => client.release();
  }, [store, workspaceId]);

  return <Outlet />;
}
