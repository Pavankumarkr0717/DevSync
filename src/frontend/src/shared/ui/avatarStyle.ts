const PASTELS = ['green', 'orange', 'purple', 'blue', 'pink', 'yellow'] as const;

/** Up to two initials from a display name: "Maya Chen" → "MC", "ana" → "A". */
export function initials(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean);
  const letters = words.length > 1 ? [words[0][0], words[words.length - 1][0]] : [words[0]?.[0] ?? '?'];
  return letters.join('').toUpperCase();
}

/** A stable pastel for an id, so a person always has the same colour. */
export function pastelFor(id: string): (typeof PASTELS)[number] {
  let hash = 0;
  for (const ch of id) {
    hash = (hash * 31 + ch.charCodeAt(0)) >>> 0;
  }
  return PASTELS[hash % PASTELS.length];
}
