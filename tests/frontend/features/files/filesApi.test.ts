import { describe, expect, it } from 'vitest';
import { makeStore } from '@/app/store';
import { filesApi } from '@/features/files/filesApi';
import { contentOf, fileHandlers, idOf, seedTree, WS } from '../../support/handlers/files';
import { authState } from '../../support/renderWithProviders';
import { server } from '../../support/server';

describe('filesApi', () => {
  it('creates a file, saves it with If-Match, and gets 412 for a stale revision', async () => {
    seedTree(['src/']);
    server.use(...fileHandlers);
    const store = makeStore({ auth: authState() });

    const created = await store
      .dispatch(filesApi.endpoints.createItem.initiate({ workspaceId: WS, parentId: idOf('src'), kind: 'file', name: 'a.ts' }))
      .unwrap();
    const loaded = await store.dispatch(filesApi.endpoints.getFileContent.initiate({ workspaceId: WS, itemId: created.id })).unwrap();
    expect(loaded).toEqual({ text: '', revision: 1 });

    const saved = await store
      .dispatch(filesApi.endpoints.saveFileContent.initiate({ workspaceId: WS, itemId: created.id, revision: 1, text: 'x\r\n' }))
      .unwrap();
    expect(saved.revision).toBe(2);
    expect(contentOf(created.id)).toEqual({ text: 'x\r\n', revision: 2 });
    // The cached copy follows the save without a refetch.
    expect(filesApi.endpoints.getFileContent.select({ workspaceId: WS, itemId: created.id })(store.getState()).data).toEqual({
      text: 'x\r\n',
      revision: 2,
    });

    const stale = await store.dispatch(
      filesApi.endpoints.saveFileContent.initiate({ workspaceId: WS, itemId: created.id, revision: 1, text: 'y' }),
    );
    expect(stale.error).toMatchObject({ status: 412, data: { code: 'revision_conflict', currentRevision: 2 } });
    expect(contentOf(created.id)?.text).toBe('x\r\n');
  });
});
