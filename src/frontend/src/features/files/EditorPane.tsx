import { lazy, Suspense, useEffect, useState } from 'react';
import { useAppDispatch, useAppSelector } from '../../app/hooks';
import type { Item } from '../../shared/api/types';
import { ErrorMessage } from '../../shared/ui/ErrorMessage';
import { Loading } from '../../shared/ui/Loading';
import { ConflictBar } from './ConflictBar';
import { DraftBar } from './DraftBar';
import { fileLoaded, selectFileEdit } from './editorSlice';
import type { FileSession } from './fileSession';
import { languageFor } from './fileRules';
import { filesApi, useGetFileContentQuery } from './filesApi';
import { fileTypeFor } from './fileTypes';
import { SaveStatus } from './SaveStatus';

// Monaco is large: load it only when a file is opened (research R8).
const CodeEditor = lazy(() => import('./CodeEditor'));

/** Open files' sessions, kept by the page while their tabs are open. */
export interface FileSessions {
  get: (itemId: string) => FileSession | undefined;
  open: (item: Item, text: string, revision: number) => FileSession;
}

interface Props {
  workspaceId: string;
  item: Item;
  readOnly: boolean;
  sessions: FileSessions;
}

/** The active file: its save status, any draft offer, and the editor. */
export function EditorPane({ workspaceId, item, readOnly, sessions }: Props) {
  const dispatch = useAppDispatch();
  const content = useGetFileContentQuery({ workspaceId, itemId: item.id });
  const state = useAppSelector((s) => selectFileEdit(s, item.id));
  const [, rerender] = useState(0);
  // The session starts from the first content that arrives and lives while the tab is open, so later
  // refetches never overwrite unsaved text. Opening is idempotent.
  const session = sessions.get(item.id) ?? (content.data ? sessions.open(item, content.data.text, content.data.revision) : undefined);

  useEffect(() => {
    if (session && !state) {
      dispatch(fileLoaded({ itemId: item.id, revision: session.controller.revision }));
    }
  }, [dispatch, item.id, session, state]);

  if (!session) {
    return content.error ? <ErrorMessage error={content.error} onRetry={() => void content.refetch()} /> : <Loading />;
  }

  const editorReadOnly = readOnly || state?.reason === 'forbidden' || state?.reason === 'deleted';

  return (
    <div className="editor-pane">
      <SaveStatus state={state} readOnly={readOnly} onRetry={() => session.controller.retry()} />
      {state && (
        <ConflictBar
          state={state}
          onReload={async () => {
            const latest = await dispatch(
              filesApi.endpoints.getFileContent.initiate({ workspaceId, itemId: item.id }, { forceRefetch: true }),
            );
            if (latest.data) {
              session.reload(latest.data.text, latest.data.revision);
              rerender((n) => n + 1);
            }
          }}
          onOverwrite={() => {
            // A deliberate overwrite: save my text against the revision now on the server.
            session.controller.setBase(state.currentRevision ?? session.controller.revision);
            session.controller.resume();
            session.flush();
          }}
          onCopy={() => void navigator.clipboard?.writeText(session.text)}
        />
      )}
      {session.pendingDraft && !readOnly && (
        <DraftBar
          draft={session.pendingDraft}
          onRestore={() => {
            session.restoreDraft();
            rerender((n) => n + 1);
          }}
          onDiscard={() => {
            session.discardDraft();
            rerender((n) => n + 1);
          }}
        />
      )}
      <Suspense fallback={<Loading label="Loading editor…" />}>
        <CodeEditor
          key={item.id}
          workspaceId={workspaceId}
          itemId={item.id}
          language={languageFor(item.name)}
          value={session.text}
          valueVersion={session.valueVersion}
          readOnly={editorReadOnly}
          onChange={(text) => session.edit(text)}
        />
      </Suspense>
      <div className="status-bar" aria-label="Editor status" role="group">
        <span>{fileTypeFor(item.name).language}</span>
        <span>UTF-8</span>
        {editorReadOnly && <span className="status-readonly">Read only</span>}
      </div>
    </div>
  );
}
