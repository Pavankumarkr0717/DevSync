import type { DiagramEditorState } from './diagramEditorSlice';

export const FAILURE_TEXT: Record<string, string> = {
  conflict: 'Someone else saved a newer version',
  deleted: 'This diagram was deleted',
  forbidden: 'You no longer have permission to edit',
  tooLarge: 'Diagram is larger than 1 MiB',
  invalidContent: "The diagram couldn't be saved",
  offline: "Couldn't reach the server — retrying",
};

/** Text for the header status (spec 004 FR-024). */
export function statusText(editor: Pick<DiagramEditorState, 'status' | 'reason' | 'problems'>, readOnly: boolean): string {
  if (editor.status === 'failed' && editor.reason) {
    const first = editor.reason === 'invalidContent' && editor.problems[0] ? `: ${editor.problems[0].message}` : '';
    return `Save failed: ${FAILURE_TEXT[editor.reason]}${first}`;
  }
  if (readOnly) {
    return 'Read only';
  }
  return editor.status === 'dirty' ? 'Unsaved changes' : editor.status === 'saving' ? 'Saving…' : 'Saved';
}
