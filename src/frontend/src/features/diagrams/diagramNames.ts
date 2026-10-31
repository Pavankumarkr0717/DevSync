import { toApiError } from '../../shared/api/problem';
import { codePointLength } from '../../shared/text';

export const MAX_NAME = 100;

/** Why a diagram name can't be used as typed, or null (contracts/ui-routes.md "Name errors"). */
export function nameProblem(name: string): string | null {
  if (name.trim().length === 0) {
    return 'Name is required.';
  }
  if (codePointLength(name.trim()) > MAX_NAME) {
    return `Name must be at most ${MAX_NAME} characters.`;
  }
  return null;
}

/** The message for a failed create or rename. */
export function nameErrorText(error: unknown): string {
  const api = toApiError(error);
  switch (api.code) {
    case 'diagram_name_taken':
      return 'A diagram with this name already exists.';
    case 'too_many_diagrams':
      return 'This workspace has reached 50 diagrams.';
    default:
      return api.detail ?? api.title;
  }
}
