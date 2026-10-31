import { screen } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { registerHandler } from '../../support/handlers/auth';
import { authSession } from '../../support/handlers/users';
import { listHandler, pageOf, problem } from '../../support/handlers/workspaces';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

async function submit(user: ReturnType<typeof renderApp>['user'], name: string, email: string, password: string, confirm = password) {
  if (name) await user.type(await screen.findByLabelText('Name'), name);
  if (email) await user.type(screen.getByLabelText('Email'), email);
  if (password) await user.type(screen.getByLabelText('Password'), password);
  if (confirm) await user.type(screen.getByLabelText('Confirm password'), confirm);
  await user.click(screen.getByRole('button', { name: 'Create account' }));
}

describe('RegisterPage', () => {
  it('checks the rules on the client before sending anything', async () => {
    let requests = 0;
    server.use(registerHandler(() => (requests++, HttpResponse.json(authSession(), { status: 201 }))));
    const { user } = renderApp({ route: '/register', auth: 'signedOut' });
    await screen.findByLabelText('Name');

    await submit(user, '', 'not-an-email', 'elevenchars');

    expect(screen.getByText('Name is required.')).toBeInTheDocument();
    expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument();
    expect(screen.getByText('Password must be at least 12 characters.')).toBeInTheDocument();
    expect(screen.getByText('12–128 characters')).toBeInTheDocument();
    expect(requests).toBe(0);
  });

  it('rejects a password equal to the email', async () => {
    const { user } = renderApp({ route: '/register', auth: 'signedOut' });

    await submit(user, 'Ana', 'ana@example.com', 'ana@example.com');

    expect(screen.getByText('Password must not be your email address.')).toBeInTheDocument();
  });

  it('shows email_taken on the email field', async () => {
    server.use(registerHandler(() => problem(409, 'email_taken', 'An account with this email already exists.')));
    const { user } = renderApp({ route: '/register', auth: 'signedOut' });

    await submit(user, 'Ana', 'ana@example.com', 'correct horse battery');

    expect(await screen.findByText('An account with this email already exists.')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true');
  });

  it('shows server field errors next to their fields', async () => {
    server.use(
      registerHandler(() =>
        problem(400, 'validation_failed', 'One or more fields are invalid.', { displayName: ['Name must not contain control characters.'] }),
      ),
    );
    const { user } = renderApp({ route: '/register', auth: 'signedOut' });

    await submit(user, 'Ana', 'ana@example.com', 'correct horse battery');

    expect(await screen.findByText('Name must not contain control characters.')).toBeInTheDocument();
  });

  it('signs the new account in and lands on the workspace list', async () => {
    server.use(
      registerHandler(() => HttpResponse.json(authSession(), { status: 201 })),
      listHandler(() => HttpResponse.json(pageOf([]))),
    );
    const { user, store } = renderApp({ route: '/register', auth: 'signedOut' });

    await submit(user, 'Ana', 'ana@example.com', 'correct horse battery');

    expect(await screen.findByText(/You have no workspaces yet/)).toBeInTheDocument();
    expect(store.getState().auth.status).toBe('signedIn');
  });

  it('asks for the password twice and refuses a mismatch without sending anything', async () => {
    let requests = 0;
    server.use(registerHandler(() => (requests++, HttpResponse.json(authSession(), { status: 201 }))));
    const { user } = renderApp({ route: '/register', auth: 'signedOut' });

    await submit(user, 'Ana', 'ana@example.com', 'correct horse battery', 'correct horse batterz');

    expect(screen.getByText('Passwords do not match.')).toBeInTheDocument();
    expect(requests).toBe(0);
  });

  it('shows a password strength guide as the person types', async () => {
    const { user } = renderApp({ route: '/register', auth: 'signedOut' });

    const password = await screen.findByLabelText('Password');
    await user.type(password, 'short');
    expect(screen.getByText('Password strength: Too short')).toBeInTheDocument();
    await user.clear(password);
    await user.type(password, 'Correct-Horse-9-Battery');
    expect(screen.getByText('Password strength: Strong')).toBeInTheDocument();
  });
});
