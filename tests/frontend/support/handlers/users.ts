import type { AuthSession, UserProfile, UserRef } from '@/shared/api/types';

export const USERS: UserRef[] = [
  { id: '0192f5a0-0000-7000-8000-000000000001', displayName: 'Ana' },
  { id: '0192f5a0-0000-7000-8000-000000000002', displayName: 'Ben' },
  { id: '0192f5a0-0000-7000-8000-000000000003', displayName: 'Cy' },
];

export const USER_A = USERS[0].id;
export const USER_B = USERS[1].id;

export function profile(userId: string = USER_A): UserProfile {
  const user = USERS.find((u) => u.id === userId) ?? USERS[0];
  return { id: user.id, displayName: user.displayName, email: `${user.displayName.toLowerCase()}@example.com`, createdAt: '2026-10-30T08:00:00Z' };
}

export function authSession(accessToken = 'fresh-access-token', userId: string = USER_A): AuthSession {
  return {
    accessToken,
    accessTokenExpiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
    user: profile(userId),
  };
}
