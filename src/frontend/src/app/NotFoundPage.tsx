import { Link } from 'react-router';

export function NotFoundPage() {
  return (
    <section>
      <h1>Page not found</h1>
      <p>
        <Link to="/workspaces">Go to your workspaces</Link>
      </p>
    </section>
  );
}
