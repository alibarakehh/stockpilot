import { useInsights } from '../../features/inventory/queries'
import { ErrorState, PageHeader } from '../Dashboard'
import { InsightsPanel } from '../InsightsPanel'

export function InsightsPage() {
  const insights = useInsights()
  return (
    <>
      <PageHeader
        eyebrow="Decision support"
        title="Inventory intelligence"
        description="Transparent recommendations based on current stock and reorder levels."
      />
      {insights.isError ? (
        <ErrorState error={insights.error} onRetry={() => void insights.refetch()} />
      ) : (
        <InsightsPanel data={insights.data ?? null} loading={insights.isLoading} />
      )}
    </>
  )
}
