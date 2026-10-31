import { screen, within } from '@testing-library/react';
import { delay, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { USER_A } from '../../support/handlers/users';
import { listHandler, pageOf, problem, summaries } from '../../support/handlers/workspaces';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

describe('WorkspaceListPage', () => {
  it('shows a loading state first', async () => {
    server.use(
      listHandler(async () => {
        await delay(50);
        return HttpResponse.json(pageOf([]));
      }),
    );
    renderApp({ userId: USER_A });

    expect(await screen.findByRole('status')).toHaveTextContent('Loading');
  });

  it('shows the empty state with a create link', async () => {
    server.use(listHandler(() => HttpResponse.json(pageOf([]))));
    renderApp({ userId: USER_A });

    expect(await screen.findByText(/You have no workspaces yet/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Create your first workspace' })).toHaveAttribute('href', '/workspaces/new');
  });

  it('lists workspaces in the order returned, with role and link', async () => {
    const items = summaries(3);
    server.use(listHandler(() => HttpResponse.json(pageOf(items))));
    renderApp({ userId: USER_A });

    const list = await screen.findByRole('list');
    const rows = within(list).getAllByRole('listitem');
    expect(rows.map((r) => within(r).getByRole('link').textContent)).toEqual(['Workspace 1', 'Workspace 2', 'Workspace 3']);
    expect(within(rows[0]).getByRole('link')).toHaveAttribute('href', `/workspaces/${items[0].id}`);
    expect(rows[0]).toHaveTextContent('owner');
    expect(screen.queryByRole('navigation', { name: 'Pagination' })).not.toBeInTheDocument();
  });

  it('pages when there are more workspaces than one page holds', async () => {
    const requestedPages: string[] = [];
    server.use(
      listHandler((request) => {
        const page = Number(new URL(request.url).searchParams.get('page'));
        requestedPages.push(String(page));
        return HttpResponse.json(
          page === 1 ? pageOf(summaries(20, 'First'), 1, 20, 25) : pageOf(summaries(5, 'Second'), 2, 20, 25),
        );
      }),
    );
    const { user } = renderApp({ userId: USER_A });

    expect(await screen.findByText('First 1')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Next' }));

    expect(await screen.findByText('Second 1')).toBeInTheDocument();
    expect(screen.getByText('Page 2 of 2')).toBeInTheDocument();
    expect(requestedPages).toEqual(['1', '2']);
  });

  it('shows the database message and correlation id on 503', async () => {
    server.use(listHandler(() => problem(503, 'service_unavailable', 'The database is unavailable.')));
    renderApp({ userId: USER_A });

    expect(await screen.findByRole('alert')).toHaveTextContent("DevSync can't reach its database right now.");
    expect(screen.getByText('corr-503')).toBeInTheDocument();
  });
});
