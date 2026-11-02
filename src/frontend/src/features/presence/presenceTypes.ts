/** Payloads of the workspace hub (specs/005-workspace-presence/contracts/workspace-hub.md). */

export const PRESENCE_SCHEMA = 1;

export interface PresenceUser {
  userId: string;
  displayName: string;
  avatarUrl: string | null;
  status: 'online';
}

export interface OnlineUsers {
  v: 1;
  workspaceId: string;
  version: number;
  users: PresenceUser[];
}

export interface UserJoined {
  v: 1;
  workspaceId: string;
  version: number;
  user: PresenceUser;
}

export interface UserLeft {
  v: 1;
  workspaceId: string;
  version: number;
  user: PresenceUser;
  reason: 'left' | 'removed';
}

export interface AccessRevoked {
  v: 1;
  workspaceId: string;
}

/** The tab's live connection: quiet while `connected`, explained otherwise (FR-031). */
export type ConnectionStatus = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'failed';
