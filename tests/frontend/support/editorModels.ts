// The text of each file "open" in the editor stand-in, as Monaco keeps one model per file. Shared through
// globalThis: the mocked editor module and the modules tests import may be separate instances.
const shared = globalThis as typeof globalThis & { __devsyncEditorModels?: Map<string, string> };

export const editorModels = (shared.__devsyncEditorModels ??= new Map<string, string>());

export function resetEditorModels() {
  editorModels.clear();
}
