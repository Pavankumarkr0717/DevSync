import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ErrorMessage } from '@/shared/ui/ErrorMessage';

const problemError = (status: number, title: string) => ({
  status,
  data: { status, title, code: 'x', correlationId: `corr-${status}` },
});

describe('ErrorMessage', () => {
  it('asks to sign in on 401', () => {
    render(<ErrorMessage error={problemError(401, 'Sign in to continue.')} />);

    expect(screen.getByRole('alert')).toHaveTextContent('Sign in to continue.');
  });

  it('explains a permission problem on 403', () => {
    render(<ErrorMessage error={problemError(403, 'Forbidden')} />);

    expect(screen.getByRole('alert')).toHaveTextContent("You don't have permission to do that.");
  });

  it('explains a database outage on 503 and shows the correlation id', () => {
    render(<ErrorMessage error={problemError(503, 'The database is unavailable.')} />);

    expect(screen.getByRole('alert')).toHaveTextContent("DevSync can't reach its database right now.");
    expect(screen.getByText('corr-503')).toBeInTheDocument();
  });

  it('shows the problem title for other errors', () => {
    render(<ErrorMessage error={problemError(500, 'An unexpected error occurred.')} />);

    expect(screen.getByRole('alert')).toHaveTextContent('An unexpected error occurred.');
    expect(screen.getByText('corr-500')).toBeInTheDocument();
  });

  it('never renders a raw non-JSON body', () => {
    render(<ErrorMessage error={{ status: 'PARSING_ERROR', originalStatus: 502, data: '<html>Bad Gateway</html>' }} />);

    expect(screen.getByRole('alert')).toHaveTextContent('Something went wrong.');
    expect(screen.queryByText(/Bad Gateway/)).not.toBeInTheDocument();
  });

  it('explains when the server cannot be reached', () => {
    render(<ErrorMessage error={{ status: 'FETCH_ERROR', error: 'TypeError: fetch failed' }} />);

    expect(screen.getByRole('alert')).toHaveTextContent("Can't reach DevSync");
  });
});
