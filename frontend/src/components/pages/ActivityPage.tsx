import { useState } from 'react'
import { useMovements } from '../../features/inventory/queries'
import { ActivityPanel } from '../ActivityPanel'
import { ErrorState, PageHeader } from '../Dashboard'
import { Pagination } from '../Pagination'

export function ActivityPage() {
  const [page, setPage] = useState(1)
  const movements = useMovements(undefined, page, 25)
  const data = movements.data
  return (
    <>
      <PageHeader
        eyebrow="Audit trail"
        title="Stock activity"
        description="Every quantity change, who made it, and why."
      />
      {movements.isError ? (
        <ErrorState error={movements.error} onRetry={() => void movements.refetch()} />
      ) : (
        <div className="activity-page">
          <ActivityPanel movements={data?.items ?? []} loading={movements.isLoading} hideHeader />
          <Pagination
            page={data?.page ?? page}
            pageSize={data?.pageSize ?? 25}
            total={data?.total ?? 0}
            totalPages={data?.totalPages ?? 0}
            setPage={setPage}
          />
        </div>
      )}
    </>
  )
}
