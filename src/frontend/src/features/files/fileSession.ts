import { AutosaveController, type SaveOutcome, type StatusUpdate } from '../../shared/editing/autosave';
import { createDraftWriter, readDraft, removeDraft, sendKeepaliveSave, writeDraft, type Draft, type DraftWriter } from '../../shared/editing/drafts';

interface Options {
  workspaceId: string;
  itemId: string;
  userId: string;
  text: string;
  revision: number;
  save: (text: string, baseRevision: number) => Promise<SaveOutcome>;
  onStatus: (update: StatusUpdate) => void;
}

/**
 * One open file's working state: its autosave controller and its draft. Lives while the tab is
 * open, independent of which tab is shown, so switching tabs never loses unsaved text.
 */
export class FileSession {
  readonly controller: AutosaveController;
  /** A draft from an earlier visit that differs from the server copy, until restored or discarded. */
  pendingDraft: Draft | null;
  /** Bumped whenever the editor's text must be replaced (restore, reload latest). */
  valueVersion = 0;
  private readonly drafts: DraftWriter;

  constructor(private readonly options: Options) {
    this.drafts = createDraftWriter(options.userId, options.itemId);
    this.controller = new AutosaveController(options.text, options.revision, {
      save: options.save,
      onStatus: options.onStatus,
      onSaved: (text) => {
        if (this.controller.currentText === text) {
          this.drafts.cancel();
          removeDraft(options.userId, options.itemId);
        }
      },
    });

    const draft = readDraft(options.userId, options.itemId);
    if (draft && draft.text === options.text) {
      removeDraft(options.userId, options.itemId);
    }
    this.pendingDraft = draft && draft.text !== options.text ? draft : null;
  }

  get text(): string {
    return this.controller.currentText;
  }

  edit(text: string) {
    this.controller.change(text);
    if (this.controller.isDirty) {
      this.drafts.write(text, this.controller.revision);
    } else {
      this.drafts.cancel();
      removeDraft(this.options.userId, this.options.itemId);
    }
  }

  /** Puts the draft back as unsaved edits based on the draft's revision, so a stale draft conflicts instead of overwriting. */
  restoreDraft() {
    const draft = this.pendingDraft;
    if (!draft) {
      return;
    }
    this.pendingDraft = null;
    this.controller.setBase(draft.baseRevision);
    this.valueVersion++;
    this.edit(draft.text);
  }

  discardDraft() {
    this.pendingDraft = null;
    removeDraft(this.options.userId, this.options.itemId);
  }

  /** Replaces the text with the server's copy (conflict "Reload latest"). */
  reload(text: string, revision: number) {
    this.controller.setBase(revision, text);
    this.drafts.cancel();
    removeDraft(this.options.userId, this.options.itemId);
    this.valueVersion++;
    this.controller.resume();
  }

  flush() {
    this.controller.flush();
  }

  /** The tab was hidden and may come back: save normally (the response keeps the revision in step) and keep a draft. */
  hide() {
    if (this.controller.isDirty) {
      this.drafts.cancel();
      writeDraft(this.options.userId, this.options.itemId, { text: this.text, baseRevision: this.controller.revision });
    }
    this.controller.flush();
  }

  /** The page is being unloaded: keep unsaved text as a draft and try a last keepalive save. */
  leave(accessToken: string | null) {
    if (!this.controller.isDirty) {
      return;
    }
    this.drafts.cancel();
    writeDraft(this.options.userId, this.options.itemId, { text: this.text, baseRevision: this.controller.revision });
    sendKeepaliveSave({
      workspaceId: this.options.workspaceId,
      itemId: this.options.itemId,
      revision: this.controller.revision,
      text: this.text,
      accessToken,
    });
  }

  /** The tab is closing or the page is going away: save what is pending, keep a draft, stop timers. */
  close() {
    if (this.controller.isDirty) {
      this.drafts.cancel();
      writeDraft(this.options.userId, this.options.itemId, { text: this.text, baseRevision: this.controller.revision });
    }
    this.controller.flush();
    this.controller.dispose();
  }
}
