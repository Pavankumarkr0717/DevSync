import { Link } from 'react-router';

export const NOT_FOUND_TEXT = 'Workspace not found';

/** Missing and not-a-member look the same, by design. */
export function WorkspaceNotFound() {
  return (
    <section>
      <h1>{NOT_FOUND_TEXT}</h1>
      <p>
        It may not exist, or you may not be a member. <Link to="/workspaces">Back to your workspaces</Link>
      </p>
    </section>
  );
}
