import type { ReactNode } from 'react';

interface EmptyStateProps {
  icon: ReactNode;
  title: string;
  children?: ReactNode;
  action?: ReactNode;
}

/** A centered "nothing here yet" panel with an optional action (docs/design/design-system.md). */
export function EmptyState({ icon, title, children, action }: EmptyStateProps) {
  return (
    <div className="empty-state">
      <div className="empty-state-icon">{icon}</div>
      <p className="empty-state-title">{title}</p>
      {children && <div className="meta">{children}</div>}
      {action}
    </div>
  );
}
