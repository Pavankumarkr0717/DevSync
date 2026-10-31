import { Navigate, Route, Routes } from 'react-router';
import { LoginPage } from '../features/auth/LoginPage';
import { ProfilePage } from '../features/auth/ProfilePage';
import { RegisterPage } from '../features/auth/RegisterPage';
import { RequireAuth } from '../features/auth/RequireAuth';
import { DiagramListPage } from '../features/diagrams/DiagramListPage';
import { DiagramPage } from '../features/diagrams/DiagramPage';
import { FilesPage } from '../features/files/FilesPage';
import { InvitationsPage } from '../features/invitations/InvitationsPage';
import { CreateWorkspacePage } from '../features/workspaces/CreateWorkspacePage';
import { WorkspaceDetailsPage } from '../features/workspaces/WorkspaceDetailsPage';
import { WorkspaceListPage } from '../features/workspaces/WorkspaceListPage';
import { AppShell } from './AppShell';
import { NotFoundPage } from './NotFoundPage';

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route path="login" element={<LoginPage />} />
        <Route path="register" element={<RegisterPage />} />
        <Route element={<RequireAuth />}>
          <Route index element={<Navigate to="/workspaces" replace />} />
          <Route path="workspaces" element={<WorkspaceListPage />} />
          <Route path="workspaces/new" element={<CreateWorkspacePage />} />
          <Route path="workspaces/:workspaceId" element={<WorkspaceDetailsPage />} />
          <Route path="workspaces/:workspaceId/files" element={<FilesPage />} />
          <Route path="workspaces/:workspaceId/diagrams" element={<DiagramListPage />} />
          <Route path="workspaces/:workspaceId/diagrams/:diagramId" element={<DiagramPage />} />
          <Route path="invitations" element={<InvitationsPage />} />
          <Route path="profile" element={<ProfilePage />} />
        </Route>
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}
