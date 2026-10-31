/** Length in Unicode code points, matching the server's validation. */
export function codePointLength(value: string): number {
  return [...value].length;
}

export function formatDate(iso: string): string {
  return new Date(iso).toLocaleString();
}
