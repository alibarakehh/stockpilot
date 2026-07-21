import type { CSSProperties } from 'react'
import type { InventoryIntelligence } from '../types'

export function InsightsPanel({
  data,
  loading,
}: {
  data: InventoryIntelligence | null
  loading: boolean
}) {
  if (loading || !data)
    return (
      <div className="panel empty-state">
        <span className="spinner" />
        Analyzing inventory…
      </div>
    )

  const money = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: data.currencyCode,
    maximumFractionDigits: 0,
  })

  return (
    <div className="insights-layout">
      <section className="health-card">
        <div
          className="health-ring"
          style={{ '--score': `${data.overallHealthScore * 3.6}deg` } as CSSProperties}
        >
          <div>
            <strong>{data.overallHealthScore}</strong>
            <span>/100</span>
          </div>
        </div>
        <div>
          <span className="eyebrow light">INVENTORY HEALTH</span>
          <h2>
            {data.overallHealthScore >= 85
              ? 'Looking healthy'
              : data.overallHealthScore >= 60
                ? 'Needs attention'
                : 'Action required'}
          </h2>
          <p>{data.executiveSummary}</p>
        </div>
      </section>

      <section className="panel recommendations">
        <header className="panel-header">
          <div>
            <span className="eyebrow">SMART RECOMMENDATIONS</span>
            <h2>Priority actions</h2>
          </div>
          <span className="ai-chip">✦ Explainable AI</span>
        </header>
        {data.insights.length ? (
          <div className="recommendation-list">
            {data.insights.map((insight) => (
              <article className={`recommendation ${insight.severity}`} key={insight.itemId}>
                <span className="risk-dot" />
                <div className="recommendation-copy">
                  <div>
                    <strong>{insight.title}</strong>
                    <span>
                      {insight.itemName} · {insight.sku}
                    </span>
                  </div>
                  <p>{insight.recommendation}</p>
                </div>
                <div className="health-mini">
                  <strong>{insight.healthScore}</strong>
                  <span>health</span>
                </div>
              </article>
            ))}
          </div>
        ) : (
          <div className="empty-state">
            <strong>No urgent actions</strong>
            <span>Every active product is above its reorder level.</span>
          </div>
        )}
      </section>

      <section className="panel category-panel">
        <header className="panel-header">
          <div>
            <span className="eyebrow">PORTFOLIO</span>
            <h2>Category exposure</h2>
          </div>
        </header>
        <div className="category-list">
          {data.categories.map((category) => {
            const max = Math.max(...data.categories.map((item) => item.value), 1)
            return (
              <div className="category-row" key={category.category}>
                <div>
                  <strong>{category.category}</strong>
                  <span>
                    {category.itemCount} SKUs · {category.units} units
                  </span>
                </div>
                <div className="category-bar">
                  <i style={{ width: `${(category.value / max) * 100}%` }} />
                </div>
                <strong>{money.format(category.value)}</strong>
                <span className={category.atRiskCount ? 'risk-count' : 'safe-count'}>
                  {category.atRiskCount} at risk
                </span>
              </div>
            )
          })}
        </div>
      </section>

      <aside className="method-note">
        <strong>How recommendations work</strong>
        <p>
          StockPilot evaluates quantity against reorder levels and calculates a transparent target
          of two reorder cycles plus a 25% safety buffer. Your inventory data never leaves this
          application.
        </p>
      </aside>
    </div>
  )
}
