import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  clearUserDrafts,
  createDraftWriter,
  KEEPALIVE_LIMIT_BYTES,
  purgeOldDrafts,
  readDraft,
  removeDraft,
  sendKeepaliveSave,
  writeDraft,
} from '@/shared/editing/drafts';

// Spec 003 FR-035–FR-037, research R11.
const ANA = 'user-ana';
const BEN = 'user-ben';

describe('drafts', () => {
  it('are stored per user and file', () => {
    writeDraft(ANA, 'f1', { text: 'mine', baseRevision: 3 });

    expect(readDraft(ANA, 'f1')).toMatchObject({ text: 'mine', baseRevision: 3 });
    expect(readDraft(BEN, 'f1')).toBeNull();
    expect(readDraft(ANA, 'f2')).toBeNull();
    expect(Object.keys(localStorage)).toEqual(['devsync:draft:user-ana:f1']);

    removeDraft(ANA, 'f1');
    expect(readDraft(ANA, 'f1')).toBeNull();
  });

  it("clearing one user's drafts leaves other users' drafts", () => {
    writeDraft(ANA, 'f1', { text: 'a', baseRevision: 1 });
    writeDraft(ANA, 'f2', { text: 'b', baseRevision: 1 });
    writeDraft(BEN, 'f1', { text: 'c', baseRevision: 1 });
    localStorage.setItem('unrelated', 'keep');

    clearUserDrafts(ANA);

    expect(readDraft(ANA, 'f1')).toBeNull();
    expect(readDraft(ANA, 'f2')).toBeNull();
    expect(readDraft(BEN, 'f1')).not.toBeNull();
    expect(localStorage.getItem('unrelated')).toBe('keep');
  });

  it('drafts older than 7 days are purged', () => {
    const now = Date.parse('2026-10-31T12:00:00Z');
    localStorage.setItem('devsync:draft:u:old', JSON.stringify({ text: 'x', baseRevision: 1, savedAt: '2026-10-24T11:59:59Z' }));
    localStorage.setItem('devsync:draft:u:new', JSON.stringify({ text: 'y', baseRevision: 1, savedAt: '2026-10-24T12:00:01Z' }));
    localStorage.setItem('devsync:draft:u:junk', 'not json');

    purgeOldDrafts(now);

    expect(localStorage.getItem('devsync:draft:u:old')).toBeNull();
    expect(localStorage.getItem('devsync:draft:u:junk')).toBeNull();
    expect(localStorage.getItem('devsync:draft:u:new')).not.toBeNull();
  });

  it('tolerates storage that throws (quota or disabled)', () => {
    const spy = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('full', 'QuotaExceededError');
    });
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('denied', 'SecurityError');
    });

    expect(() => writeDraft(ANA, 'f1', { text: 'x', baseRevision: 1 })).not.toThrow();
    expect(readDraft(ANA, 'f1')).toBeNull();
    expect(() => clearUserDrafts(ANA)).not.toThrow();
    expect(() => purgeOldDrafts()).not.toThrow();
    expect(spy).toHaveBeenCalled();
  });

  describe('writer', () => {
    beforeEach(() => vi.useFakeTimers());
    afterEach(() => vi.useRealTimers());

    it('writes at most every 500 ms while typing, then the latest text', async () => {
      const setItem = vi.spyOn(Storage.prototype, 'setItem');
      const writer = createDraftWriter(ANA, 'f1');

      for (let i = 1; i <= 10; i++) {
        writer.write(`v${i}`, 2);
        await vi.advanceTimersByTimeAsync(100);
      }
      await vi.advanceTimersByTimeAsync(500);

      expect(setItem.mock.calls.length).toBeLessThanOrEqual(3);
      expect(readDraft(ANA, 'f1')).toMatchObject({ text: 'v10', baseRevision: 2 });
    });

    it('flush writes synchronously (for pagehide) and cancel drops a pending write', () => {
      const writer = createDraftWriter(ANA, 'f1');

      writer.write('first', 1);
      writer.write('pending', 1);
      writer.flush();
      expect(readDraft(ANA, 'f1')?.text).toBe('pending');

      writer.write('later', 1);
      writer.cancel();
      vi.advanceTimersByTime(1000);
      expect(readDraft(ANA, 'f1')?.text).toBe('pending');
    });
  });
});

describe('sendKeepaliveSave', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('sends a keepalive PUT with the revision when the body fits in 64 KiB', () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null));
    vi.stubGlobal('fetch', fetchMock);

    expect(sendKeepaliveSave({ workspaceId: 'w', itemId: 'f', revision: 4, text: 'é'.repeat(100), accessToken: 'tok' })).toBe(true);

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toMatch(/\/api\/workspaces\/w\/files\/f\/content$/);
    expect(init).toMatchObject({ method: 'PUT', keepalive: true, body: 'é'.repeat(100) });
    expect(init.headers).toMatchObject({
      Authorization: 'Bearer tok',
      'If-Match': '"4"',
      'Content-Type': 'text/plain; charset=utf-8',
    });
  });

  it('does not try when the body is over 64 KiB or there is no token', () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);

    expect(KEEPALIVE_LIMIT_BYTES).toBe(65_536);
    expect(sendKeepaliveSave({ workspaceId: 'w', itemId: 'f', revision: 1, text: 'a'.repeat(65_537), accessToken: 't' })).toBe(false);
    expect(sendKeepaliveSave({ workspaceId: 'w', itemId: 'f', revision: 1, text: 'a', accessToken: null })).toBe(false);
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
