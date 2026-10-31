import { Link, Navigate } from 'react-router';
import { useAppSelector } from '../../app/hooks';
import { DiagramIcon, FileIcon, UsersIcon } from '../../shared/ui/icons';
import { selectAuthStatus } from '../auth/authSlice';

const FEATURES = [
  {
    icon: <UsersIcon />,
    title: 'Workspaces and roles',
    text: 'Invite teammates as Owners, Editors or Viewers. The server checks every action.',
  },
  {
    icon: <FileIcon />,
    title: 'Code editor',
    text: 'Folders and files with syntax highlighting, tabs and autosave that never overwrites newer work.',
  },
  {
    icon: <DiagramIcon />,
    title: 'System design canvas',
    text: 'Draw clients, gateways, services, databases, caches and queues, and connect them.',
  },
];

/**
 * The public welcome page at `/`. Signed-in people go straight to their workspaces. The preview is an
 * illustration of the editor and the canvas, not a working control.
 */
export function LandingPage() {
  const status = useAppSelector(selectAuthStatus);
  if (status === 'signedIn') {
    return <Navigate to="/workspaces" replace />;
  }

  return (
    <section className="landing">
      <div className="landing-hero">
        <p className="eyebrow mono">Collaborative engineering workspace</p>
        <h1>
          Build together. <span className="accent-text">Design together.</span> Engineer together.
        </h1>
        <p className="landing-lead">
          DevSync brings your team&apos;s code and architecture into one workspace: organize projects, edit code in the
          browser, and sketch the system it runs on.
        </p>
        <div className="landing-actions">
          <Link to="/register" className="btn btn-primary">
            Get started
          </Link>
          <Link to="/login" className="btn">
            Sign in
          </Link>
        </div>
      </div>

      <div className="landing-preview" aria-hidden="true">
        <div className="preview-window">
          <div className="preview-bar">
            <span />
            <span />
            <span />
            <span className="preview-title mono">payments / src / OrderService.cs</span>
          </div>
          <div className="preview-body">
            <ul className="preview-tree mono">
              <li>▾ src</li>
              <li className="active">
                <b className="file-badge pastel-purple">C#</b> OrderService.cs
              </li>
              <li>
                <b className="file-badge pastel-blue">TS</b> checkout.ts
              </li>
              <li>
                <b className="file-badge pastel-orange">{'{ }'}</b> appsettings.json
              </li>
            </ul>
            <pre className="preview-code mono">
              <code>
                <span className="tok-kw">public sealed class</span> <span className="tok-type">OrderService</span>
                {'\n{\n    '}
                <span className="tok-kw">public async</span> <span className="tok-type">Task</span>&lt;
                <span className="tok-type">Order</span>&gt; PlaceAsync(<span className="tok-type">Cart</span> cart)
                {'\n    {\n        '}
                <span className="tok-kw">var</span> order = <span className="tok-type">Order</span>.From(cart);
                {'\n        '}
                <span className="tok-kw">await</span> _orders.SaveAsync(order);
                {'\n        '}
                <span className="tok-kw">return</span> order;{'\n    }\n}'}
              </code>
            </pre>
            <div className="preview-canvas">
              <span className="preview-node pastel-blue">Client</span>
              <span className="preview-edge" />
              <span className="preview-node pastel-purple">API Gateway</span>
              <span className="preview-edge" />
              <span className="preview-node pastel-green">Orders</span>
              <span className="preview-edge" />
              <span className="preview-node pastel-yellow">Orders DB</span>
            </div>
          </div>
        </div>
      </div>

      <ul className="landing-features">
        {FEATURES.map((f) => (
          <li key={f.title}>
            <span className="landing-feature-icon">{f.icon}</span>
            <h2>{f.title}</h2>
            <p className="meta">{f.text}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
