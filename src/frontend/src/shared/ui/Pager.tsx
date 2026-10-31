interface PagerProps {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}

/** Previous/next controls, shown only when there is more than one page. */
export function Pager({ page, pageSize, totalCount, onPageChange }: PagerProps) {
  const pageCount = Math.ceil(totalCount / pageSize);
  if (pageCount <= 1) {
    return null;
  }

  return (
    <nav aria-label="Pagination" className="pager">
      <button type="button" onClick={() => onPageChange(page - 1)} disabled={page <= 1}>
        Previous
      </button>
      <span>
        Page {page} of {pageCount}
      </span>
      <button type="button" onClick={() => onPageChange(page + 1)} disabled={page >= pageCount}>
        Next
      </button>
    </nav>
  );
}
