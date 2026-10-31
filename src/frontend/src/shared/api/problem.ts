import type { Problem } from './types';

/** A normalized view of any error returned by an RTK Query hook. */
export interface ApiError {
  /** HTTP status, or 0 when the server could not be reached. */
  status: number;
  title: string;
  code?: string;
  correlationId?: string;
  fieldErrors: Record<string, string[]>;
}

function isProblem(data: unknown): data is Problem {
  return typeof data === 'object' && data !== null && 'title' in data && 'status' in data;
}

/** Converts an RTK Query error (FetchBaseQueryError or SerializedError) into an ApiError. */
export function toApiError(error: unknown): ApiError {
  if (typeof error === 'object' && error !== null && 'status' in error) {
    const { status, data } = error as { status: unknown; data?: unknown; originalStatus?: number };

    if (typeof status === 'number') {
      if (isProblem(data)) {
        return {
          status,
          title: data.title,
          code: data.code,
          correlationId: data.correlationId,
          fieldErrors: data.errors ?? {},
        };
      }
      return { status, title: 'Something went wrong.', fieldErrors: {} };
    }

    // PARSING_ERROR: a non-JSON body, e.g. a proxy error page.
    const originalStatus = (error as { originalStatus?: number }).originalStatus;
    if (typeof originalStatus === 'number') {
      return { status: originalStatus, title: 'Something went wrong.', fieldErrors: {} };
    }

    // FETCH_ERROR / TIMEOUT_ERROR: the request never got a response.
    return { status: 0, title: "Can't reach DevSync. Check that it is running.", fieldErrors: {} };
  }

  return { status: 0, title: 'Something went wrong.', fieldErrors: {} };
}
