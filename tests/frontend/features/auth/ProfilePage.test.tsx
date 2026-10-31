import { screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderApp } from '../../support/renderWithProviders';
import { server } from '../../support/server';

describe('ProfilePage', () => {
  afterEach(() => vi.restoreAllMocks());

  it('shows name, email and member since', async () => {
    renderApp({ route: '/profile' });

    expect(await screen.findByRole('heading', { name: 'Profile' })).toBeInTheDocument();
    expect(screen.getByText('Name').nextElementSibling).toHaveTextContent('Ana');
    expect(screen.getByText('Email').nextElementSibling).toHaveTextContent('ana@example.com');
    expect(screen.getByText('Member since').nextElementSibling).toHaveTextContent(new Date('2026-10-30T08:00:00Z').toLocaleString());
  });

  it('signs out and clears the session', async () => {
    let calls = 0;
    server.use(http.post('*/api/auth/logout', () => (calls++, new HttpResponse(null, { status: 204 }))));
    const { user, store } = renderApp({ route: '/profile' });

    const page = await screen.findByRole('main');
    await within(page).findByRole('heading', { name: 'Profile' });
    await user.click(within(page).getByRole('button', { name: 'Sign out' }));

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(calls).toBe(1);
    expect(store.getState().auth.accessToken).toBeNull();
    expect(store.getState().auth.user).toBeNull();
  });

  it('asks before signing out everywhere', async () => {
    let calls = 0;
    server.use(http.post('*/api/auth/logout-all', () => (calls++, new HttpResponse(null, { status: 204 }))));
    const confirm = vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValueOnce(true);
    const { user } = renderApp({ route: '/profile' });

    await user.click(await screen.findByRole('button', { name: 'Sign out everywhere' }));
    expect(calls).toBe(0);

    await user.click(screen.getByRole('button', { name: 'Sign out everywhere' }));
    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(calls).toBe(1);
    expect(confirm).toHaveBeenCalledTimes(2);
  });
});
