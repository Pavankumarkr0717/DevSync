/** Where an open document stands relative to the server copy. */
export type SaveState = 'saved' | 'dirty' | 'saving' | 'failed';

/** Why the last save failed (shown in the save status). */
export type SaveFailure = 'conflict' | 'deleted' | 'forbidden' | 'tooLarge' | 'invalidContent' | 'offline';

/**
 * Debounced autosave for one open file (spec 003 FR-020–FR-022, research R10):
 * - saves after typing pauses for 1 s, and at least every 10 s while typing continues;
 * - never has two saves in flight, never sends text identical to what was last saved;
 * - a save is never aborted once sent (it may have committed); its result always updates the base revision;
 * - network/server failures retry at 2 s, 4 s, 8 s … (capped at 30 s) and on the next edit;
 * - a conflict pauses until the person chooses; "deleted" and "no permission" stop for good.
 * The latest text is held here (and in the editor's model), never in Redux.
 */

export type SaveOutcome = { ok: true; revision: number } | { ok: false; reason: SaveFailure; currentRevision?: number };

export interface StatusUpdate {
  status: SaveState;
  reason?: SaveFailure;
  baseRevision: number;
  currentRevision?: number;
}

export interface AutosaveOptions {
  save: (text: string, baseRevision: number) => Promise<SaveOutcome>;
  onStatus: (update: StatusUpdate) => void;
  /** Called after each successful save with the text that was stored. */
  onSaved?: (text: string, revision: number) => void;
  delayMs?: number;
  maxWaitMs?: number;
  retryDelaysMs?: number[];
  maxRetryDelayMs?: number;
}

export class AutosaveController {
  private text: string;
  private savedText: string;
  private baseRevision: number;
  private inFlight = false;
  private flushAfterSave = false;
  private paused = false;
  private stopped = false;
  private retries = 0;
  private lastFailure: SaveFailure | null = null;
  private debounceTimer: ReturnType<typeof setTimeout> | undefined;
  private maxWaitTimer: ReturnType<typeof setTimeout> | undefined;
  private retryTimer: ReturnType<typeof setTimeout> | undefined;
  private running: Promise<void> | null = null;
  private readonly options: Required<Omit<AutosaveOptions, 'onSaved'>> & Pick<AutosaveOptions, 'onSaved'>;

  constructor(initialText: string, baseRevision: number, options: AutosaveOptions) {
    this.text = initialText;
    this.savedText = initialText;
    this.baseRevision = baseRevision;
    this.options = {
      delayMs: 1000,
      maxWaitMs: 10_000,
      retryDelaysMs: [2000, 4000, 8000],
      maxRetryDelayMs: 30_000,
      ...options,
    };
  }

  /** The latest text, saved or not. */
  get currentText(): string {
    return this.text;
  }

  get isDirty(): boolean {
    return this.text !== this.savedText;
  }

  get revision(): number {
    return this.baseRevision;
  }

  change(text: string) {
    this.text = text;
    if (this.stopped) {
      return;
    }
    clearTimeout(this.retryTimer);

    if (!this.isDirty && !this.inFlight) {
      this.clearSchedule();
      if (!this.paused) {
        this.lastFailure = null;
        this.emit('saved');
      }
      return;
    }

    if (this.paused) {
      return; // still a conflict; the person decides
    }
    if (!this.inFlight) {
      this.lastFailure = null;
      this.emit('dirty');
    }
    this.schedule();
  }

  /** Save now (tab switch, tab close, leaving the page). */
  flush() {
    this.clearSchedule();
    clearTimeout(this.retryTimer);
    if (this.inFlight) {
      this.flushAfterSave = true;
      return;
    }
    void this.run();
  }

  /** Manual retry after a failure. */
  retry() {
    this.flush();
  }

  /** Saves now and waits; true when everything is saved (e.g. before closing a tab). */
  async settle(): Promise<boolean> {
    this.flush();
    while (this.running) {
      await this.running;
    }
    return !this.isDirty && this.lastFailure === null;
  }

  pause() {
    this.paused = true;
    this.clearSchedule();
  }

  resume() {
    this.paused = false;
    if (this.isDirty) {
      this.emit('dirty');
      this.schedule();
    } else {
      this.emit('saved');
    }
  }

  /** Re-bases on a revision, e.g. "Overwrite with mine" (keeps text) or "Reload latest" (pass the server text). */
  setBase(revision: number, savedText?: string) {
    this.baseRevision = revision;
    if (savedText !== undefined) {
      this.savedText = savedText;
      this.text = savedText;
    }
  }

  dispose() {
    this.stopped = true;
    this.clearSchedule();
    clearTimeout(this.retryTimer);
  }

  private schedule() {
    clearTimeout(this.debounceTimer);
    this.debounceTimer = setTimeout(() => void this.run(), this.options.delayMs);
    this.maxWaitTimer ??= setTimeout(() => void this.run(), this.options.maxWaitMs);
  }

  private clearSchedule() {
    clearTimeout(this.debounceTimer);
    clearTimeout(this.maxWaitTimer);
    this.debounceTimer = undefined;
    this.maxWaitTimer = undefined;
  }

  private run(): Promise<void> {
    const attempt = this.attempt();
    this.running = attempt;
    void attempt.finally(() => {
      if (this.running === attempt) {
        this.running = null;
      }
    });
    return attempt;
  }

  private async attempt() {
    this.clearSchedule();
    if (this.inFlight) {
      this.flushAfterSave = true;
      return;
    }
    if (this.paused || this.stopped) {
      return;
    }
    if (!this.isDirty) {
      if (this.lastFailure === null) {
        this.emit('saved');
      }
      return;
    }

    const sending = this.text;
    this.inFlight = true;
    this.emit('saving');

    let outcome: SaveOutcome;
    try {
      outcome = await this.options.save(sending, this.baseRevision);
    } catch {
      outcome = { ok: false, reason: 'offline' };
    }
    this.inFlight = false;

    if (outcome.ok) {
      this.savedText = sending;
      this.baseRevision = outcome.revision;
      this.retries = 0;
      this.lastFailure = null;
      this.options.onSaved?.(sending, outcome.revision);
      if (this.stopped) {
        return;
      }
      if (this.isDirty) {
        this.emit('dirty');
        if (this.flushAfterSave) {
          this.flushAfterSave = false;
          void this.run();
        } else {
          this.schedule();
        }
      } else {
        this.flushAfterSave = false;
        this.emit('saved');
      }
      return;
    }

    this.flushAfterSave = false;
    this.lastFailure = outcome.reason;
    switch (outcome.reason) {
      case 'conflict':
        this.paused = true;
        this.emit('failed', outcome.reason, outcome.currentRevision);
        break;
      case 'deleted':
      case 'forbidden':
        this.emit('failed', outcome.reason);
        this.dispose();
        break;
      case 'offline': {
        this.emit('failed', outcome.reason);
        const delays = this.options.retryDelaysMs;
        const delay = Math.min(
          delays[this.retries] ?? delays[delays.length - 1] * 2 ** (this.retries - delays.length + 1),
          this.options.maxRetryDelayMs,
        );
        this.retries++;
        if (!this.stopped) {
          this.retryTimer = setTimeout(() => void this.run(), delay);
        }
        break;
      }
      default:
        // Too large or invalid: wait for the next edit.
        this.emit('failed', outcome.reason);
    }
  }

  private emit(status: SaveState, reason?: SaveFailure, currentRevision?: number) {
    const update: StatusUpdate = { status, baseRevision: this.baseRevision };
    if (reason) {
      update.reason = reason;
    }
    if (currentRevision !== undefined) {
      update.currentRevision = currentRevision;
    }
    this.options.onStatus(update);
  }
}
