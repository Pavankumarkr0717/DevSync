import { setPresenceTransport, type HubConnectionLike } from '@/features/presence/presenceClient';
import type { OnlineUsers, PresenceUser } from '@/features/presence/presenceTypes';
import { USERS } from './handlers/users';

type Handler = (payload: unknown) => void;
type JoinAnswer = (workspaceId: string) => unknown | Promise<unknown>;

export function presenceUser(index: number): PresenceUser {
  return { userId: USERS[index].id, displayName: USERS[index].displayName, avatarUrl: null, status: 'online' };
}

export function snapshot(workspaceId: string, version: number, users: PresenceUser[]): OnlineUsers {
  return { v: 1, workspaceId, version, users };
}

/**
 * An in-memory stand-in for a SignalR HubConnection (spec 005 research R12). Tests drive it:
 * emit server events, drop the connection, make starts fail, and inspect invocations.
 */
export class FakeHubConnection implements HubConnectionLike {
  state = 'Disconnected';
  readonly handlers = new Map<string, Handler[]>();
  readonly invocations: Array<{ method: string; args: unknown[] }> = [];
  onCalls = 0;
  starts = 0;
  stops = 0;
  failStarts = 0;
  private closeCallbacks: Array<(error?: Error) => void> = [];

  constructor(
    private readonly hub: FakeHub,
    readonly accessTokenFactory: () => Promise<string>,
  ) {}

  on(event: string, handler: Handler) {
    this.onCalls += 1;
    this.handlers.set(event, [...(this.handlers.get(event) ?? []), handler]);
  }

  async invoke<T>(method: string, ...args: unknown[]): Promise<T> {
    this.invocations.push({ method, args });
    if (this.state !== 'Connected') {
      throw new Error('Cannot send data if the connection is not in the Connected State.');
    }
    if (method === 'JoinWorkspace') {
      const { workspaceId } = args[0] as { workspaceId: string };
      return (await this.hub.answerJoin(workspaceId)) as T;
    }
    return undefined as T;
  }

  async start() {
    this.starts += 1;
    const token = await this.accessTokenFactory();
    if (this.hub.rejectedTokens.has(token)) {
      throw Object.assign(new Error('Unauthorized: Status code 401'), { statusCode: 401 });
    }
    if (this.failStarts > 0 || this.hub.down) {
      this.failStarts = Math.max(0, this.failStarts - 1);
      throw new Error('Failed to start the connection');
    }
    this.state = 'Connected';
  }

  async stop() {
    this.stops += 1;
    const wasConnected = this.state === 'Connected';
    this.state = 'Disconnected';
    if (wasConnected) {
      this.closeCallbacks.forEach((cb) => cb());
    }
  }

  onclose(callback: (error?: Error) => void) {
    this.closeCallbacks.push(callback);
  }

  /** The server sends an event. */
  emit(event: string, payload: unknown) {
    (this.handlers.get(event) ?? []).forEach((h) => h(payload));
  }

  /** The network or server drops the connection. */
  drop() {
    this.state = 'Disconnected';
    this.closeCallbacks.forEach((cb) => cb(new Error('Connection lost')));
  }

  joins(): string[] {
    return this.invocations
      .filter((i) => i.method === 'JoinWorkspace')
      .map((i) => (i.args[0] as { workspaceId: string }).workspaceId);
  }
}

/** All connections created during a test, with a configurable join answer. */
export class FakeHub {
  readonly connections: FakeHubConnection[] = [];
  /** When true every start fails (server unreachable). */
  down = false;
  /** Tokens the server answers with 401 (e.g. signed with a key from before a restart). */
  readonly rejectedTokens = new Set<string>();
  /** By default a join returns the joining user (Ana) alone, at version 1. */
  answerJoin: JoinAnswer = (workspaceId) => snapshot(workspaceId, 1, [presenceUser(0)]);

  get last(): FakeHubConnection {
    const connection = this.connections.at(-1);
    if (!connection) {
      throw new Error('No hub connection was created');
    }
    return connection;
  }

  install() {
    setPresenceTransport((accessTokenFactory) => {
      const connection = new FakeHubConnection(this, accessTokenFactory);
      this.connections.push(connection);
      return connection;
    });
  }
}

export let fakeHub = new FakeHub();

/** Called before every test: a fresh hub, so no state leaks between tests. */
export function resetFakeHub() {
  fakeHub = new FakeHub();
  fakeHub.install();
  return fakeHub;
}
