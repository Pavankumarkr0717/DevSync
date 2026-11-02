import { PRESENCE_SCHEMA, type AccessRevoked, type OnlineUsers, type PresenceUser, type UserJoined, type UserLeft } from './presenceTypes';

/*
 * Every message from the hub is checked before it reaches the store (constitution VI): an unknown
 * schema version or a wrong shape is dropped, never half-applied.
 */

type Json = Record<string, unknown>;

function isObject(value: unknown): value is Json {
  return typeof value === 'object' && value !== null;
}

function isEnvelope(value: unknown): value is Json & { workspaceId: string } {
  return isObject(value) && value.v === PRESENCE_SCHEMA && typeof value.workspaceId === 'string';
}

function isVersioned(value: Json): value is Json & { version: number } {
  return typeof value.version === 'number' && Number.isFinite(value.version);
}

function toUser(value: unknown): PresenceUser | null {
  if (!isObject(value) || typeof value.userId !== 'string' || typeof value.displayName !== 'string') {
    return null;
  }
  return {
    userId: value.userId,
    displayName: value.displayName,
    avatarUrl: typeof value.avatarUrl === 'string' ? value.avatarUrl : null,
    status: 'online',
  };
}

export function parseOnlineUsers(value: unknown): OnlineUsers | null {
  if (!isEnvelope(value) || !isVersioned(value) || !Array.isArray(value.users)) {
    return null;
  }
  const users = value.users.map(toUser);
  if (users.some((u) => u === null)) {
    return null;
  }
  return { v: PRESENCE_SCHEMA, workspaceId: value.workspaceId, version: value.version, users: users as PresenceUser[] };
}

export function parseUserJoined(value: unknown): UserJoined | null {
  const user = isObject(value) ? toUser(value.user) : null;
  if (!isEnvelope(value) || !isVersioned(value) || !user) {
    return null;
  }
  return { v: PRESENCE_SCHEMA, workspaceId: value.workspaceId, version: value.version, user };
}

export function parseUserLeft(value: unknown): UserLeft | null {
  const joined = parseUserJoined(value);
  if (!joined || !isObject(value) || (value.reason !== 'left' && value.reason !== 'removed')) {
    return null;
  }
  return { ...joined, reason: value.reason };
}

export function parseAccessRevoked(value: unknown): AccessRevoked | null {
  return isEnvelope(value) ? { v: PRESENCE_SCHEMA, workspaceId: value.workspaceId } : null;
}

/** The code a hub error carries (the server sends the code as the error's last word). */
export function hubErrorCode(error: unknown): string | null {
  const message = error instanceof Error ? error.message : typeof error === 'string' ? error : '';
  const match = /([a-z_]+)\s*$/.exec(message);
  return match ? match[1] : null;
}
