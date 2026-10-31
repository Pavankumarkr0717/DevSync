/** Placeholder cards while a card list loads; screen readers hear "Loading…". */
export function CardSkeletons({ count = 3, preview = false }: { count?: number; preview?: boolean }) {
  return (
    <div role="status" aria-live="polite">
      <span className="visually-hidden">Loading…</span>
      <ul className="card-grid" aria-hidden="true">
        {Array.from({ length: count }, (_, i) => (
          <li key={i} className="board-card skeleton-card">
            {preview && <span className="skeleton skeleton-preview" />}
            <span className="skeleton-lines">
              <span className="skeleton skeleton-avatar" />
              <span className="skeleton skeleton-line" />
              <span className="skeleton skeleton-line short" />
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
