import { toApiError } from '../api/problem';
import type { SaveOutcome } from './autosave';

/** Maps a failed content save to the reason shown to the person (contracts/ui-routes.md). */
export function toSaveOutcome(error: unknown): SaveOutcome {
  const apiError = toApiError(error);
  const data = (error as { data?: { currentRevision?: unknown } } | undefined)?.data;
  switch (apiError.status) {
    case 412:
      return {
        ok: false,
        reason: 'conflict',
        currentRevision: typeof data?.currentRevision === 'number' ? data.currentRevision : undefined,
      };
    case 404:
      return { ok: false, reason: 'deleted' };
    case 403:
      return { ok: false, reason: 'forbidden' };
    case 413:
      return { ok: false, reason: 'tooLarge' };
    case 400:
      return { ok: false, reason: 'invalidContent' };
    default:
      // Network errors (status 0), 5xx and an ended session: keep the text and try again later.
      return { ok: false, reason: 'offline' };
  }
}
