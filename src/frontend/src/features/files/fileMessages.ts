import { toApiError } from '../../shared/api/problem';
import type { SaveFailure } from './editorSlice';
import { FORBIDDEN_TEXT } from '../../shared/ui/uiSlice';
import { NAME_TAKEN_TEXT, UNSUPPORTED_EXTENSION_TEXT } from './fileRules';

// Messages for file-structure errors (specs/003-file-explorer-editor/contracts/ui-routes.md).
const MESSAGES: Record<string, string> = {
  name_taken: NAME_TAKEN_TEXT,
  unsupported_extension: UNSUPPORTED_EXTENSION_TEXT,
  too_deep: 'Folders can be nested at most 20 levels.',
  path_too_long: 'The full path would be longer than 1,024 characters.',
  too_many_items: 'This workspace has reached 5,000 files and folders.',
  invalid_move: 'A folder cannot be moved into itself or one of its folders.',
  parent_not_found: 'That folder no longer exists.',
  item_not_found: 'That file or folder no longer exists.',
};

export function structureErrorMessage(error: unknown): string {
  const apiError = toApiError(error);
  if (apiError.code === 'invalid_name') {
    return apiError.detail ?? apiError.title;
  }
  if (apiError.status === 403) {
    return FORBIDDEN_TEXT;
  }
  return (apiError.code && MESSAGES[apiError.code]) || apiError.title;
}

/** Why a save failed, as shown in the save status (FR-022). */
export const FAILURE_TEXT: Record<SaveFailure, string> = {
  conflict: 'Someone else saved a newer version',
  deleted: 'This file was deleted',
  forbidden: 'You no longer have permission to edit',
  tooLarge: 'File is larger than 1 MiB',
  invalidContent: "File contains characters that can't be saved",
  offline: "Couldn't reach the server — retrying",
};
