import { Link, NavLink } from 'react-router';
import { ChevronRightIcon } from '../../shared/ui/icons';
import { PresenceBar } from '../presence/PresenceBar';

type Section = 'overview' | 'files' | 'diagrams';

const SECTIONS: Array<{ id: Section; label: string; path: string }> = [
  { id: 'overview', label: 'Overview', path: '' },
  { id: 'files', label: 'Files', path: '/files' },
  { id: 'diagrams', label: 'Diagrams', path: '/diagrams' },
];

/** Breadcrumbs, the Overview · Files · Diagrams tabs and who is online, shared by workspace pages (spec 004 FR-039, spec 005 FR-028). */
export function WorkspaceNav({ workspaceId, name, current }: { workspaceId: string; name: string; current: Section }) {
  const base = `/workspaces/${workspaceId}`;
  const section = SECTIONS.find((s) => s.id === current)!;

  return (
    <>
      <nav aria-label="Breadcrumb" className="breadcrumbs">
        <Link to="/workspaces">Workspaces</Link>
        <ChevronRightIcon size={14} />
        {current === 'overview' ? (
          <span aria-current="page">{name}</span>
        ) : (
          <>
            <Link to={base}>{name}</Link>
            <ChevronRightIcon size={14} />
            <span aria-current="page">{section.label}</span>
          </>
        )}
      </nav>
      <div className="tabs-row">
        <nav aria-label="Workspace sections" className="tabs-nav">
          {SECTIONS.map((s) => (
            <NavLink key={s.id} to={`${base}${s.path}`} end className={s.id === current ? 'active' : undefined}>
              {s.label}
            </NavLink>
          ))}
        </nav>
        <PresenceBar />
      </div>
    </>
  );
}
