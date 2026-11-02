import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import type { Dispatch } from '@reduxjs/toolkit';
import type { AuthState } from '../auth/authSlice';
import { baseApi } from '../../shared/api/baseApi';
import { refreshSession } from '../../shared/api/session';
import { toastShown } from '../../shared/ui/uiSlice';
import { hubErrorCode, parseAccessRevoked, parseOnlineUsers, parseUserJoined, parseUserLeft } from './presenceEvents';
import { PRESENCE_SCHEMA } from './presenceTypes';
import {
  accessRevoked,
  joinRefused,
  joinRequested,
  joinSucceeded,
  presenceReset,
  snapshotReceived,
  statusChanged,
  type PresenceState,
} from './presenceSlice';

export const HUB_PATH = '/api/hubs/workspace';

/** Waits before each reconnect attempt (research R10); after the last one presence is "unavailable". */
export const RETRY_DELAYS_MS = [0, 2_000, 5_000, 10_000, 30_000];

/** Refresh the access token before connecting when it has less than this left. */
const TOKEN_MARGIN_MS = 30_000;

export const REVOKED_TEXT = 'You no longer have access to this workspace.';

/** The part of a SignalR HubConnection the client uses; tests substitute an in-memory fake. */
export interface HubConnectionLike {
  readonly state: string;
  on(event: string, handler: (payload: unknown) => void): void;
  invoke<T = unknown>(method: string, ...args: unknown[]): Promise<T>;
  start(): Promise<void>;
  stop(): Promise<void>;
  onclose(callback: (error?: Error) => void): void;
}

export type PresenceTransport = (accessTokenFactory: () => Promise<string>) => HubConnectionLike;

const signalRTransport: PresenceTransport = (accessTokenFactory) =>
  new HubConnectionBuilder()
    .withUrl(`${window.location.origin}${HUB_PATH}`, { accessTokenFactory })
    .configureLogging(LogLevel.Warning)
    .build();

let transport: PresenceTransport = signalRTransport;

/** Test seam: replace how connections are built (null restores SignalR). */
export function setPresenceTransport(factory: PresenceTransport | null) {
  transport = factory ?? signalRTransport;
}

interface PresenceStore {
  dispatch: Dispatch;
  getState: () => { auth: AuthState; presence: PresenceState };
}

class SessionEndedError extends Error {}

function isUnauthorized(error: unknown): boolean {
  return typeof error === 'object' && error !== null && (error as { statusCode?: unknown }).statusCode === 401;
}

/**
 * The tab's single real-time connection (spec 005 FR-022–FR-027). It joins the workspace the tab
 * shows, drops results that navigation has made stale, reconnects on its own schedule (refreshing
 * the access token first) and turns hub events into store updates and notifications. Handlers are
 * registered once, when the connection object is created.
 */
export class PresenceClient {
  private connection: HubConnectionLike | null = null;
  private desired: string | null = null;
  private generation = 0;
  private connecting: Promise<boolean> | null = null;
  private loop = 0;
  private stopping = false;
  /** Set when the server refused the token (401): renew it before the next attempt. */
  private tokenRejected = false;
  private readonly notified = new Set<string>();

  constructor(private readonly store: PresenceStore) {}

  /** Shows (and joins) a workspace; switching workspaces reuses the connection. */
  join(workspaceId: string) {
    this.desired = workspaceId;
    this.store.dispatch(joinRequested(workspaceId));
    void this.sync();
  }

  /**
   * The tab no longer shows a workspace. The connection is closed unless a workspace is shown again
   * in the same task (a route change from one workspace page to another unmounts and remounts).
   */
  release() {
    this.desired = null;
    queueMicrotask(() => {
      if (this.desired === null) {
        void this.shutdown();
      }
    });
  }

  /** Manual retry after automatic reconnection gave up (FR-026). */
  retry() {
    if (this.store.getState().presence.status === 'failed') {
      void this.reconnect();
    }
  }

  private async sync() {
    if (await this.ensureConnected()) {
      await this.joinDesired();
    }
  }

  private ensureConnected(): Promise<boolean> {
    if (this.connection?.state === 'Connected') {
      return Promise.resolve(true);
    }
    if (this.connecting) {
      return this.connecting;
    }
    const status = this.store.getState().presence.status;
    if (status === 'reconnecting' || status === 'failed') {
      // The reconnect loop owns the connection; it joins the desired workspace when it is back.
      return Promise.resolve(false);
    }
    this.connecting = this.firstConnect().finally(() => {
      this.connecting = null;
    });
    return this.connecting;
  }

  private async firstConnect(): Promise<boolean> {
    this.stopping = false;
    this.store.dispatch(statusChanged('connecting'));
    try {
      await this.ensureConnection().start();
      this.store.dispatch(statusChanged('connected'));
      return true;
    } catch (error) {
      this.tokenRejected = isUnauthorized(error);
      if (!this.stopping && !(error instanceof SessionEndedError)) {
        void this.reconnect();
      }
      return false;
    }
  }

  private ensureConnection(): HubConnectionLike {
    if (this.connection) {
      return this.connection;
    }
    const connection = transport(() => this.accessToken());
    connection.on('OnlineUsersUpdated', (payload) => this.onSnapshot(payload));
    connection.on('UserJoined', (payload) => this.onJoined(payload));
    connection.on('UserLeft', (payload) => this.onLeft(payload));
    connection.on('AccessRevoked', (payload) => this.onRevoked(payload));
    connection.onclose(() => {
      if (!this.stopping && connection === this.connection) {
        void this.reconnect();
      }
    });
    this.connection = connection;
    return connection;
  }

  private async accessToken(): Promise<string> {
    const { auth } = this.store.getState();
    const expiresAt = auth.accessTokenExpiresAt ? Date.parse(auth.accessTokenExpiresAt) : 0;
    if (auth.accessToken && expiresAt - Date.now() > TOKEN_MARGIN_MS && !this.tokenRejected) {
      return auth.accessToken;
    }
    this.tokenRejected = false;
    const outcome = await refreshSession(this.store.dispatch);
    const token = this.store.getState().auth.accessToken;
    if (outcome === 'ended' || !token) {
      throw new SessionEndedError('Session ended');
    }
    if (outcome !== 'refreshed') {
      throw new Error('Could not refresh the session');
    }
    return token;
  }

  private async reconnect() {
    const loop = ++this.loop;
    const connection = this.ensureConnection();
    this.store.dispatch(statusChanged('reconnecting'));
    for (const delay of RETRY_DELAYS_MS) {
      if (delay > 0) {
        await new Promise((resolve) => setTimeout(resolve, delay));
      }
      if (loop !== this.loop || this.stopping) {
        return;
      }
      try {
        if (connection.state !== 'Connected') {
          await connection.start();
        }
        if (loop !== this.loop || this.stopping) {
          return;
        }
        this.store.dispatch(statusChanged('connected'));
        if (this.desired) {
          // A new connection starts a new baseline: the server may have restarted with lower versions.
          this.store.dispatch(joinRequested(this.desired));
        }
        await this.joinDesired();
        return;
      } catch (error) {
        // e.g. the server restarted with a new signing key: the session may still be valid.
        this.tokenRejected = isUnauthorized(error);
        if (error instanceof SessionEndedError || this.store.getState().auth.status !== 'signedIn') {
          // refreshSession has already ended the session; the app is going to the sign-in page.
          this.store.dispatch(statusChanged('idle'));
          return;
        }
      }
    }
    if (loop === this.loop && !this.stopping) {
      this.store.dispatch(statusChanged('failed'));
    }
  }

  private async joinDesired() {
    const target = this.desired;
    const connection = this.connection;
    if (!target || connection?.state !== 'Connected') {
      return;
    }
    const generation = ++this.generation;
    try {
      const result = await connection.invoke('JoinWorkspace', { v: PRESENCE_SCHEMA, workspaceId: target });
      if (generation !== this.generation || this.desired !== target) {
        return; // navigation moved on; a later join decides
      }
      const snapshot = parseOnlineUsers(result);
      if (snapshot && snapshot.workspaceId === target) {
        this.store.dispatch(joinSucceeded(snapshot));
      }
    } catch (error) {
      if (generation !== this.generation || this.desired !== target) {
        return;
      }
      if (hubErrorCode(error) === 'workspace_not_found') {
        this.store.dispatch(joinRefused(target));
      }
      // Other failures (the connection dropped mid-call) are retried by the reconnect loop.
    }
  }

  private async shutdown() {
    this.stopping = true;
    this.loop += 1;
    this.generation += 1;
    const connection = this.connection;
    this.connection = null;
    this.notified.clear();
    this.store.dispatch(presenceReset());
    if (connection) {
      try {
        await connection.stop();
      } catch {
        // Already closed.
      }
    }
  }

  private currentWorkspace() {
    return this.store.getState().presence.workspaceId;
  }

  private onSnapshot(payload: unknown) {
    const snapshot = parseOnlineUsers(payload);
    if (snapshot) {
      this.store.dispatch(snapshotReceived(snapshot));
    }
  }

  private onJoined(payload: unknown) {
    const event = parseUserJoined(payload);
    if (event) {
      this.notify(event.workspaceId, event.version, event.user.userId, `${event.user.displayName} joined the workspace.`);
    }
  }

  private onLeft(payload: unknown) {
    const event = parseUserLeft(payload);
    if (event) {
      this.notify(event.workspaceId, event.version, event.user.userId, `${event.user.displayName} left the workspace.`);
    }
  }

  /** At most one notification per event, never about oneself, never about the past (FR-030). */
  private notify(workspaceId: string, version: number, userId: string, text: string) {
    const { presence, auth } = this.store.getState();
    const key = `${workspaceId}:${version}`;
    if (workspaceId !== presence.workspaceId || version <= presence.baseline || userId === auth.user?.id || this.notified.has(key)) {
      return;
    }
    this.notified.add(key);
    this.store.dispatch(toastShown(text));
  }

  private onRevoked(payload: unknown) {
    const event = parseAccessRevoked(payload);
    if (!event || event.workspaceId !== this.currentWorkspace()) {
      return;
    }
    this.generation += 1;
    this.store.dispatch(accessRevoked(event.workspaceId));
    this.store.dispatch(toastShown(REVOKED_TEXT));
    // The pages refetch and show "Workspace not found" (FR-032).
    this.store.dispatch(baseApi.util.invalidateTags(['Workspace', 'FileTree', 'DiagramList', 'Diagram']));
  }
}

const clients = new WeakMap<object, PresenceClient>();

/** One client (one connection) per store, i.e. per tab. */
export function getPresenceClient(store: PresenceStore): PresenceClient {
  let client = clients.get(store);
  if (!client) {
    client = new PresenceClient(store);
    clients.set(store, client);
  }
  return client;
}
