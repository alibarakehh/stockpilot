import { ChevronLeft, ChevronRight } from 'lucide-react'

export function Pagination({
  page,
  pageSize,
  total,
  totalPages,
  setPage,
}: {
  page: number
  pageSize: number
  total: number
  totalPages: number
  setPage: (page: number) => void
}) {
  return (
    <footer className="pagination" aria-label="Pagination">
      <span>
        Showing {total ? (page - 1) * pageSize + 1 : 0}–{Math.min(page * pageSize, total)} of{' '}
        {total}
      </span>
      <div>
        <button aria-label="Previous page" disabled={page <= 1} onClick={() => setPage(page - 1)}>
          <ChevronLeft aria-hidden="true" size={15} />
        </button>
        <span aria-live="polite">
          Page {page} of {Math.max(totalPages, 1)}
        </span>
        <button
          aria-label="Next page"
          disabled={page >= totalPages}
          onClick={() => setPage(page + 1)}
        >
          <ChevronRight aria-hidden="true" size={15} />
        </button>
      </div>
    </footer>
  )
}
