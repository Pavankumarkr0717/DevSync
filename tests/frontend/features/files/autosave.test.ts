import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AutosaveController, type SaveOutcome, type StatusUpdate } from '@/shared/editing/autosave';

// Spec 003 FR-020–FR-022, SC-003, research R10.

interface Call {
  text: string;
  revision: number;
  resolve: (outcome: SaveOutcome) => void;
}

function setup(initial = '', revision = 1) {
  const calls: Call[] = [];
  const statuses: StatusUpdate[] = [];
  const saved: Array<[string, number]> = [];
  let auto: ((call: Call) => SaveOutcome | undefined) | null = (call) => ({ ok: true, revision: call.revision + 1 });

  const controller = new AutosaveController(initial, revision, {
    save: (text, base) =>
      new Promise<SaveOutcome>((resolve) => {
        const call = { text, revision: base, resolve };
        calls.push(call);
        const outcome = auto?.(call);
        if (outcome) {
          resolve(outcome);
        }
      }),
    onStatus: (update) => statuses.push(update),
    onSaved: (text, rev) => saved.push([text, rev]),
  });

  return {
    controller,
    calls,
    saved,
    last: () => statuses[statuses.length - 1],
    /** Responses are given by hand from now on. */
    manual: () => {
      auto = null;
    },
    respondWith: (fn: (call: Call) => SaveOutcome | undefined) => {
      auto = fn;
    },
  };
}

async function advance(ms: number) {
  await vi.advanceTimersByTimeAsync(ms);
}

describe('AutosaveController', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('does not save before typing pauses for 1 second, then saves once', async () => {
    const t = setup();

    t.controller.change('a');
    expect(t.last()).toMatchObject({ status: 'dirty' });
    await advance(500);
    t.controller.change('ab');
    await advance(999);
    expect(t.calls).toHaveLength(0);

    await advance(1);
    expect(t.calls.map((c) => [c.text, c.revision])).toEqual([['ab', 1]]);
    expect(t.last()).toEqual({ status: 'saved', baseRevision: 2 });
    expect(t.saved).toEqual([['ab', 2]]);
  });

  it('saves at most every 10 seconds during continuous typing: at most 7 saves in 60 seconds', async () => {
    const t = setup();
    let text = '';
    for (let ms = 0; ms < 60_000; ms += 200) {
      text += 'x';
      t.controller.change(text);
      await advance(200);
    }
    await advance(1000);

    expect(t.calls.length).toBeGreaterThanOrEqual(6);
    expect(t.calls.length).toBeLessThanOrEqual(7);
    expect(t.calls[t.calls.length - 1].text).toBe(text);
    // The first save happened within the 10 s maximum wait.
    expect(t.calls[0].text.length).toBeLessThanOrEqual(51);
  });

  it('never has two saves in flight, and saves edits made during a save afterwards', async () => {
    const t = setup();
    t.manual();

    t.controller.change('one');
    await advance(1000);
    expect(t.calls).toHaveLength(1);
    expect(t.last()).toMatchObject({ status: 'saving' });

    t.controller.change('one two');
    t.controller.flush();
    await advance(5000);
    expect(t.calls).toHaveLength(1);

    t.calls[0].resolve({ ok: true, revision: 2 });
    await advance(0);
    expect(t.calls).toHaveLength(2);
    expect(t.calls[1]).toMatchObject({ text: 'one two', revision: 2 });
    t.calls[1].resolve({ ok: true, revision: 3 });
    await advance(0);
    expect(t.last()).toEqual({ status: 'saved', baseRevision: 3 });
  });

  it('sends nothing when the text is back to what was saved', async () => {
    const t = setup('same');

    t.controller.change('same!');
    t.controller.change('same');
    expect(t.last()).toEqual({ status: 'saved', baseRevision: 1 });
    await advance(15_000);

    expect(t.calls).toHaveLength(0);
  });

  it('flush saves immediately', async () => {
    const t = setup();

    t.controller.change('now');
    t.controller.flush();
    await advance(0);

    expect(t.calls.map((c) => c.text)).toEqual(['now']);
  });

  it('retries after a network failure at 2, 4 and 8 seconds until it succeeds', async () => {
    const t = setup();
    let failures = 3;
    t.respondWith((call) => (failures-- > 0 ? { ok: false, reason: 'offline' } : { ok: true, revision: call.revision + 1 }));

    t.controller.change('text');
    await advance(1000);
    expect(t.calls).toHaveLength(1);
    expect(t.last()).toMatchObject({ status: 'failed', reason: 'offline' });

    await advance(1999);
    expect(t.calls).toHaveLength(1);
    await advance(1);
    expect(t.calls).toHaveLength(2);
    await advance(4000);
    expect(t.calls).toHaveLength(3);
    await advance(8000);
    expect(t.calls).toHaveLength(4);
    expect(t.last()).toEqual({ status: 'saved', baseRevision: 2 });
  });

  it('caps the retry delay at 30 seconds', async () => {
    const t = setup();
    t.respondWith(() => ({ ok: false, reason: 'offline' }));

    t.controller.change('text');
    await advance(1000 + 2000 + 4000 + 8000);
    expect(t.calls).toHaveLength(4);
    await advance(16_000);
    expect(t.calls).toHaveLength(5);
    await advance(29_999);
    expect(t.calls).toHaveLength(5);
    await advance(1);
    expect(t.calls).toHaveLength(6);
  });

  it('retries on the next edit and on a manual retry', async () => {
    const t = setup();
    t.respondWith(() => ({ ok: false, reason: 'offline' }));
    t.controller.change('a');
    await advance(1000);
    expect(t.calls).toHaveLength(1);

    t.respondWith((call) => ({ ok: true, revision: call.revision + 1 }));
    t.controller.retry();
    await advance(0);
    expect(t.calls).toHaveLength(2);
    expect(t.last()).toEqual({ status: 'saved', baseRevision: 2 });

    t.respondWith(() => ({ ok: false, reason: 'offline' }));
    t.controller.change('ab');
    await advance(1000);
    t.respondWith((call) => ({ ok: true, revision: call.revision + 1 }));
    t.controller.change('abc');
    await advance(1000);
    expect(t.calls[t.calls.length - 1].text).toBe('abc');
    expect(t.last()).toEqual({ status: 'saved', baseRevision: 3 });
  });

  it.each(['tooLarge', 'invalidContent'] as const)('does not retry %s by itself, but does on the next edit', async (reason) => {
    const t = setup();
    t.respondWith(() => ({ ok: false, reason }));

    t.controller.change('x');
    await advance(1000);
    expect(t.last()).toMatchObject({ status: 'failed', reason });
    await advance(60_000);
    expect(t.calls).toHaveLength(1);

    t.controller.change('y');
    await advance(1000);
    expect(t.calls).toHaveLength(2);
  });

  it.each(['forbidden', 'deleted'] as const)('stops for good on %s', async (reason) => {
    const t = setup();
    t.respondWith(() => ({ ok: false, reason }));

    t.controller.change('x');
    await advance(1000);
    expect(t.last()).toMatchObject({ status: 'failed', reason });

    t.controller.change('xy');
    t.controller.flush();
    await advance(60_000);
    expect(t.calls).toHaveLength(1);
    expect(t.last()).toMatchObject({ status: 'failed', reason });
  });

  it('pauses on a conflict until resumed', async () => {
    const t = setup();
    t.respondWith(() => ({ ok: false, reason: 'conflict', currentRevision: 5 }));

    t.controller.change('mine');
    await advance(1000);
    expect(t.last()).toEqual({ status: 'failed', reason: 'conflict', baseRevision: 1, currentRevision: 5 });

    t.controller.change('mine, more');
    await advance(30_000);
    expect(t.calls).toHaveLength(1);
    expect(t.last()).toMatchObject({ status: 'failed', reason: 'conflict' });

    t.respondWith((call) => ({ ok: true, revision: call.revision + 1 }));
    t.controller.setBase(5);
    t.controller.resume();
    t.controller.flush();
    await advance(0);
    expect(t.calls[1]).toMatchObject({ text: 'mine, more', revision: 5 });
    expect(t.last()).toEqual({ status: 'saved', baseRevision: 6 });
  });

  it('treats a thrown save as offline', async () => {
    const t = setup();
    t.respondWith(() => {
      throw new Error('boom');
    });

    t.controller.change('x');
    await advance(1000);

    expect(t.last()).toMatchObject({ status: 'failed', reason: 'offline' });
  });

  it('stops all timers when disposed', async () => {
    const t = setup();

    t.controller.change('x');
    t.controller.dispose();
    await advance(20_000);

    expect(t.calls).toHaveLength(0);
  });
});
