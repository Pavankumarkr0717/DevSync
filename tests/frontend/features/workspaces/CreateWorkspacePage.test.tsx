import { screen, waitFor } from '@testing-library/react';
import { delay, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { USER_A } from '../../support/handlers/users';
import { createHandler, detailsHandler, problem, workspaceDetails } from '../../support/handlers/workspaces';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

function renderCreatePage() {
  return renderApp({ route: '/workspaces/new', userId: USER_A });
}

describe('CreateWorkspacePage', () => {
  it('blocks an empty or whitespace-only name without sending a request', async () => {
    let requests = 0;
    server.use(createHandler(() => (requests++, HttpResponse.json(workspaceDetails(), { status: 201 }))));
    const { user } = renderCreatePage();

    await user.click(await screen.findByRole('button', { name: 'Create workspace' }));
    expect(screen.getByText('Name is required.')).toBeInTheDocument();

    await user.type(screen.getByLabelText('Name'), '    ');
    await user.click(screen.getByRole('button', { name: 'Create workspace' }));
    expect(screen.getByText('Name is required.')).toBeInTheDocument();
    expect(screen.getByLabelText('Name')).toHaveAttribute('aria-invalid', 'true');

    expect(requests).toBe(0);
  });

  it('enforces the 100 / 500 character limits on the client, counting code points', async () => {
    let requests = 0;
    server.use(createHandler(() => (requests++, HttpResponse.json(workspaceDetails(), { status: 201 }))));
    const { user } = renderCreatePage();

    await user.click(await screen.findByLabelText('Name'));
    await user.paste('x'.repeat(101));
    await user.click(screen.getByLabelText('Description (optional)'));
    await user.paste('d'.repeat(501));
    await user.click(screen.getByRole('button', { name: 'Create workspace' }));

    expect(screen.getByText('Name must be at most 100 characters.')).toBeInTheDocument();
    expect(screen.getByText('Description must be at most 500 characters.')).toBeInTheDocument();
    expect(requests).toBe(0);
  });

  it('accepts a 100-emoji name (100 code points, 200 UTF-16 units)', async () => {
    let sentName = '';
    server.use(
      createHandler((body) => {
        sentName = (body as { name: string }).name;
        return HttpResponse.json(workspaceDetails(), { status: 201 });
      }),
      detailsHandler(() => HttpResponse.json(workspaceDetails())),
    );
    const { user } = renderCreatePage();

    await user.click(await screen.findByLabelText('Name'));
    await user.paste('😀'.repeat(100));
    await user.click(screen.getByRole('button', { name: 'Create workspace' }));

    await waitFor(() => expect(sentName).toBe('😀'.repeat(100)));
  });

  it("shows the server's field errors next to the matching field", async () => {
    server.use(
      createHandler(() =>
        problem(400, 'validation_failed', 'One or more fields are invalid.', {
          name: ['Name must not contain control characters.'],
        }),
      ),
    );
    const { user } = renderCreatePage();

    await user.type(await screen.findByLabelText('Name'), 'Looks fine');
    await user.click(screen.getByRole('button', { name: 'Create workspace' }));

    expect(await screen.findByText('Name must not contain control characters.')).toHaveAttribute('id', 'name-error');
  });

  it('disables submit while the request is pending', async () => {
    server.use(
      createHandler(async () => {
        await delay(100);
        return HttpResponse.json(workspaceDetails(), { status: 201 });
      }),
      detailsHandler(() => HttpResponse.json(workspaceDetails())),
    );
    const { user } = renderCreatePage();

    await user.type(await screen.findByLabelText('Name'), 'Team');
    await user.click(screen.getByRole('button', { name: 'Create workspace' }));

    expect(await screen.findByRole('button', { name: 'Creating…' })).toBeDisabled();
  });

  it('navigates to the new workspace after it is created', async () => {
    const created = workspaceDetails({ id: '0193aaaa-0000-7000-8000-0000000000aa', name: 'Brand New' });
    server.use(
      createHandler(() => HttpResponse.json(created, { status: 201 })),
      detailsHandler((id) => (id === created.id ? HttpResponse.json(created) : problem(404, 'not_found', 'Workspace not found.'))),
    );
    const { user } = renderCreatePage();

    await user.type(await screen.findByLabelText('Name'), 'Brand New');
    await user.click(screen.getByRole('button', { name: 'Create workspace' }));

    expect(await screen.findByRole('heading', { name: 'Brand New' })).toBeInTheDocument();
  });

  it('shows the database message and correlation id on 503', async () => {
    server.use(createHandler(() => problem(503, 'service_unavailable', 'The database is unavailable.')));
    const { user } = renderCreatePage();

    await user.type(await screen.findByLabelText('Name'), 'Team');
    await user.click(screen.getByRole('button', { name: 'Create workspace' }));

    expect(await screen.findByRole('alert')).toHaveTextContent("DevSync can't reach its database right now.");
    expect(screen.getByText('corr-503')).toBeInTheDocument();
  });
});
