import { AutosaveController, type SaveOutcome, type StatusUpdate } from '../../shared/editing/autosave';
import {
  createDraftWriter,
  readDraft,
  removeDraft,
  sendKeepalive,
  writeDraft,
  type Draft,
  type DraftWriter,
} from '../../shared/editing/drafts';

interface Options {
  workspaceId: string;
  diagramId: string;
  userId: string;
  /** The canonical document as loaded. */
  json: string;
  revision: number;
  save: (json: string, baseRevision: number) => Promise<SaveOutcome>;
  onStatus: (update: StatusUpdate) => void;
}

/**
 * One open diagram's autosave and draft (spec 004 FR-026, FR-026a), like a file session in 003.
 * Its "text" is the canonical document JSON, so an unchanged diagram is never re-saved.
 */
export class DiagramSession {
  readonly controller: AutosaveController;
  pendingDraft: Draft | null;
  private readonly drafts: DraftWriter;
  private readonly draftKey: string;

  constructor(private readonly options: Options) {
    this.draftKey = `diagram-${options.diagramId}`;
    this.drafts = createDraftWriter(options.userId, this.draftKey);
    this.controller = new AutosaveController(options.json, options.revision, {
      save: options.save,
      onStatus: options.onStatus,
      onSaved: (json) => {
        if (this.controller.currentText === json) {
          this.drafts.cancel();
          removeDraft(options.userId, this.draftKey);
        }
      },
    });

    const draft = readDraft(options.userId, this.draftKey);
    if (draft && draft.text === options.json) {
      removeDraft(options.userId, this.draftKey);
    }
    this.pendingDraft = draft && draft.text !== options.json ? draft : null;
  }

  edit(json: string) {
    this.controller.change(json);
    if (this.controller.isDirty) {
      this.drafts.write(json, this.controller.revision);
    } else {
      this.drafts.cancel();
      removeDraft(this.options.userId, this.draftKey);
    }
  }

  /** The draft's text, now based on the draft's revision so a stale draft conflicts instead of overwriting. */
  takeDraft(): string | null {
    const draft = this.pendingDraft;
    if (!draft) {
      return null;
    }
    this.pendingDraft = null;
    this.controller.setBase(draft.baseRevision);
    return draft.text;
  }

  discardDraft() {
    this.pendingDraft = null;
    removeDraft(this.options.userId, this.draftKey);
  }

  /** Reload latest: the server copy becomes both the text and the base. */
  reload(json: string, revision: number) {
    this.controller.setBase(revision, json);
    this.drafts.cancel();
    removeDraft(this.options.userId, this.draftKey);
    this.controller.resume();
  }

  /** The tab was hidden: save normally and keep a draft. */
  hide() {
    if (this.controller.isDirty) {
      this.drafts.cancel();
      writeDraft(this.options.userId, this.draftKey, { text: this.controller.currentText, baseRevision: this.controller.revision });
    }
    this.controller.flush();
  }

  /** The page is unloading: keep a draft and try a keepalive save. */
  leave(accessToken: string | null) {
    if (!this.controller.isDirty) {
      return;
    }
    this.drafts.cancel();
    writeDraft(this.options.userId, this.draftKey, { text: this.controller.currentText, baseRevision: this.controller.revision });
    sendKeepalive({
      path: `/api/workspaces/${encodeURIComponent(this.options.workspaceId)}/diagrams/${encodeURIComponent(this.options.diagramId)}/document`,
      contentType: 'application/json',
      revision: this.controller.revision,
      body: this.controller.currentText,
      accessToken,
    });
  }

  /** Leaving the diagram: save what is pending, keep a draft, stop timers. */
  close() {
    if (this.controller.isDirty) {
      this.drafts.cancel();
      writeDraft(this.options.userId, this.draftKey, { text: this.controller.currentText, baseRevision: this.controller.revision });
    }
    this.controller.flush();
    this.controller.dispose();
  }
}
