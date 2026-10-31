import { toApiError } from '../api/problem';
import { FORBIDDEN_TEXT } from './uiSlice';

export const SIGN_IN_TEXT = 'Sign in to continue.';
export const DATABASE_DOWN_TEXT = "DevSync can't reach its database right now. Try again shortly.";

/** Shows a safe, human-readable message for an API error, with its correlation id for support. */
export function ErrorMessage({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const apiError = toApiError(error);

  let message = apiError.title;
  if (apiError.status === 401) {
    message = SIGN_IN_TEXT;
  } else if (apiError.status === 403) {
    message = FORBIDDEN_TEXT;
  } else if (apiError.status === 503) {
    message = DATABASE_DOWN_TEXT;
  }

  return (
    <div role="alert" className="error">
      <p>{message}</p>
      {apiError.correlationId && (
        <p className="correlation">
          Reference: <code>{apiError.correlationId}</code>
        </p>
      )}
      {onRetry && (
        <button type="button" onClick={onRetry}>
          Retry
        </button>
      )}
    </div>
  );
}
