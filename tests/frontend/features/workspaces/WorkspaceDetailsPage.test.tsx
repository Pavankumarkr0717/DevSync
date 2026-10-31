import { screen, within } from '@testing-library/react';
import { delay, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { USER_A } from '../../support/handlers/users';
import { createHandler, detailsHandler, listHandler, pageOf, problem, workspaceDetails } from '../../support/handlers/workspaces';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

const workspace = workspaceDetails();

describe('WorkspaceDetailsPage', () => {
  it('shows a loading state first', async () => {
    server.use(
      detailsHandler(async () => {
        await delay(50);
        return HttpResponse.json(workspace);
      }),
    );
    renderApp({ route: `/workspaces/${workspace.id}`, userId: USER_A });

    expect(await screen.findByRole('status')).toHaveTextContent('Loading');
  });

  it('shows name, description, owner, created date and members', async () => {
    server.use(detailsHandler(() => HttpResponse.json(workspace)));
    renderApp({ route: `/workspaces/${workspace.id}`, userId: USER_A });

    expect(await screen.findByRole('heading', { name: 'Interview Prep' })).toBeInTheDocument();
    expect(screen.getByText('Mock interviews')).toBeInTheDocument();
    expect(screen.getByText('Owner').nextElementSibling).toHaveTextContent('Ana');
    expect(screen.getByText('Created').nextElementSibling).toHaveTextContent(new Date(workspace.createdAt).toLocaleString());
    const members = within(screen.getByRole('list', { name: 'Members' })).getAllByRole('listitem');
    expect(members).toHaveLength(1);
    expect(members[0]).toHaveTextContent('Ana — owner');
  });

  it('says "No description" when there is none', async () => {
    server.use(detailsHandler(() => HttpResponse.json(workspaceDetails({ description: null }))));
    renderApp({ route: `/workspaces/${workspace.id}`, userId: USER_A });

    expect(await screen.findByText('No description')).toBeInTheDocument();
  });

  it('shows "Workspace not found" on 404', async () => {
    server.use(detailsHandler(() => problem(404, 'not_found', 'Workspace not found.')));
    renderApp({ route: '/workspaces/whatever', userId: USER_A });

    expect(await screen.findByRole('heading', { name: 'Workspace not found' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /back to your workspaces/i })).toHaveAttribute('href', '/workspaces');
  });

  it('shows the database message and correlation id on 503', async () => {
    server.use(detailsHandler(() => problem(503, 'service_unavailable', 'The database is unavailable.')));
    renderApp({ route: `/workspaces/${workspace.id}`, userId: USER_A });

    expect(await screen.findByRole('alert')).toHaveTextContent("DevSync can't reach its database right now.");
    expect(screen.getByText('corr-503')).toBeInTheDocument();
  });

  it('create → details: the new workspace is shown after creating it from the list', async () => {
    const created = workspaceDetails({ id: '0193aaaa-0000-7000-8000-0000000000cc', name: 'From The List' });
    server.use(
      listHandler(() => HttpResponse.json(pageOf([]))),
      createHandler(() => HttpResponse.json(created, { status: 201 })),
      detailsHandler(() => HttpResponse.json(created)),
    );
    const { user } = renderApp({ userId: USER_A });

    await user.click(await screen.findByRole('link', { name: 'New workspace' }));
    await user.type(await screen.findByLabelText('Name'), 'From The List');
    await user.click(screen.getByRole('button', { name: 'Create workspace' }));

    expect(await screen.findByRole('heading', { name: 'From The List' })).toBeInTheDocument();
  });
});
