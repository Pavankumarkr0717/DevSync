export const DEFAULT_ROUTE = '/workspaces';

/** Only same-origin relative paths are allowed as a post-sign-in destination (no open redirects). */
export function safeReturnTo(value: string | null | undefined): string {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.startsWith('/\\')) {
    return DEFAULT_ROUTE;
  }
  return value;
}
