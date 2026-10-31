import type { ReactNode } from 'react';
import type { WorkspaceRole } from '../api/types';

/**
 * A coloured badge for a workspace role (Owner, Editor, Viewer). It only communicates permissions;
 * the server decides what each role may do (spec 002 FR-030).
 */
export function RoleBadge({ role, children }: { role: WorkspaceRole; children?: ReactNode }) {
  return <span className={`role-badge role-${role}${children ? ' role-badge-label' : ''}`}>{children ?? role}</span>;
}
