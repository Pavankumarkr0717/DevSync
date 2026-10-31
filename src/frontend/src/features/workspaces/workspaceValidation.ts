import { codePointLength } from '../../shared/text';

export const NAME_MAX = 100;
export const DESCRIPTION_MAX = 500;

export type WorkspaceFieldErrors = Partial<Record<'name' | 'description', string>>;

/** Mirrors the server rules for workspace name and description (code points after trimming). */
export function validateWorkspace(name: string, description: string): WorkspaceFieldErrors {
  const errors: WorkspaceFieldErrors = {};
  const trimmedName = name.trim();
  if (trimmedName.length === 0) {
    errors.name = 'Name is required.';
  } else if (codePointLength(trimmedName) > NAME_MAX) {
    errors.name = `Name must be at most ${NAME_MAX} characters.`;
  }
  if (codePointLength(description.trim()) > DESCRIPTION_MAX) {
    errors.description = `Description must be at most ${DESCRIPTION_MAX} characters.`;
  }
  return errors;
}
