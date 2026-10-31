import { describe, expect, it, vi } from 'vitest';
import { clearUserTabs, loadTabs, saveTabs } from '@/features/files/tabStorage';

// Spec 003 FR-033: open tabs survive a refresh, per user and workspace, in this browser only.
describe('tabStorage', () => {
  it('saves and loads tabs per user and workspace', () => {
    saveTabs('ana', 'ws1', { itemIds: ['a', 'b', 'c'], activeId: 'b' });

    expect(localStorage.getItem('devsync:tabs:ana:ws1')).not.toBeNull();
    expect(loadTabs('ana', 'ws1', new Set(['a', 'b', 'c']))).toEqual({ itemIds: ['a', 'b', 'c'], activeId: 'b' });
    expect(loadTabs('ben', 'ws1', new Set(['a']))).toBeNull();
    expect(loadTabs('ana', 'ws2', new Set(['a']))).toBeNull();
  });

  it('drops files that no longer exist, and the active one falls back to the first', () => {
    saveTabs('ana', 'ws1', { itemIds: ['a', 'gone', 'c'], activeId: 'gone' });

    expect(loadTabs('ana', 'ws1', new Set(['a', 'c']))).toEqual({ itemIds: ['a', 'c'], activeId: 'a' });
  });

  it("clears one user's tabs only", () => {
    saveTabs('ana', 'ws1', { itemIds: ['a'], activeId: 'a' });
    saveTabs('ana', 'ws2', { itemIds: ['b'], activeId: 'b' });
    saveTabs('ben', 'ws1', { itemIds: ['c'], activeId: 'c' });

    clearUserTabs('ana');

    expect(loadTabs('ana', 'ws1', new Set(['a']))).toBeNull();
    expect(loadTabs('ana', 'ws2', new Set(['b']))).toBeNull();
    expect(loadTabs('ben', 'ws1', new Set(['c']))).not.toBeNull();
  });

  it('ignores broken or unavailable storage', () => {
    localStorage.setItem('devsync:tabs:ana:ws1', '{not json');
    expect(loadTabs('ana', 'ws1', new Set(['a']))).toBeNull();

    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('full', 'QuotaExceededError');
    });
    expect(() => saveTabs('ana', 'ws1', { itemIds: ['a'], activeId: 'a' })).not.toThrow();
  });
});
