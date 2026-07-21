import { ArrowRight, PackagePlus } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import {
  useCategories,
  useInsights,
  useInventorySummary,
  useMovements,
} from '../../features/inventory/queries'
import type { User } from '../../types'
import { ActivityPanel } from '../ActivityPanel'
import { ErrorState, PageHeader } from '../Dashboard'

export function DashboardPage({ user }: { user: User }) {
  const navigate = useNavigate()
  const summary = useInventorySummary()
  const insights = useInsights()
  const movements = useMovements(undefined, 1, 6)
  const categories = useCategories()
  const error = summary.error ?? insights.error ?? movements.error
  const currency = summary.data?.currencyCode ?? insights.data?.currencyCode ?? 'USD'
  const money = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency,
    maximumFractionDigits: 0,
  })

  return (
    <>
      <PageHeader
        eyebrow="Overview"
        title={`Welcome back, ${user.name.split(' ')[0]}`}
        description="A live snapshot of stock health and the work that needs attention."
        actions={
          <Link className="button primary" to="/inventory">
            <PackagePlus aria-hidden="true" size={16} /> Open inventory
          </Link>
        }
      />
      {error && (
        <ErrorState
          error={error}
          onRetry={() =>
            void Promise.all([summary.refetch(), insights.refetch(), movements.refetch()])
          }
        />
      )}
      <section className="stats-grid" aria-label="Inventory overview">
        <article>
          <span>Total SKUs</span>
          <strong>{summary.data?.totalItems ?? '—'}</strong>
          <small>
            {summary.data?.totalUnits ?? 0} units across {categories.data?.length ?? 0} categories
          </small>
        </article>
        <article>
          <span>Inventory value</span>
          <strong>{summary.data ? money.format(summary.data.totalValue) : '—'}</strong>
          <small>current stock on hand</small>
        </article>
        <button className="stat-card risk" onClick={() => navigate('/inventory?status=LowStock')}>
          <span>Low stock</span>
          <strong>{summary.data?.lowStockCount ?? '—'}</strong>
          <small>{summary.data?.outOfStockCount ?? 0} additional item(s) out of stock</small>
        </button>
        <button className="stat-card" onClick={() => navigate('/insights')}>
          <span>Health score</span>
          <strong>
            {insights.data?.overallHealthScore ?? '—'}
            <em>/100</em>
          </strong>
          <small>review explainable recommendations</small>
        </button>
      </section>
      <div className="section-heading">
        <div>
          <span className="eyebrow">Latest changes</span>
          <h2>Recent activity</h2>
        </div>
        <Link className="text-link" to="/activity">
          View full history <ArrowRight size={14} />
        </Link>
      </div>
      <ActivityPanel
        movements={movements.data?.items ?? []}
        loading={movements.isLoading}
        compact
      />
    </>
  )
}
