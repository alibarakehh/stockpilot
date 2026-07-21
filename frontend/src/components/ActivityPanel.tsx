import type { InventoryMovement } from '../types'

const labels: Record<InventoryMovement['type'], string> = {
  OpeningBalance: 'Opening balance',
  Receipt: 'Received',
  Issue: 'Issued',
  Damage: 'Damaged',
  Return: 'Returned',
  Correction: 'Corrected',
}

export function ActivityPanel({
  movements,
  loading,
  compact = false,
  hideHeader = false,
}: {
  movements: InventoryMovement[]
  loading: boolean
  compact?: boolean
  hideHeader?: boolean
}) {
  return (
    <section className={`panel activity-panel${compact ? ' compact-panel' : ''}`}>
      {!hideHeader && !compact && (
        <header className="panel-header">
          <div>
            <span className="eyebrow">AUDIT TRAIL</span>
            <h2>Recent stock activity</h2>
          </div>
        </header>
      )}
      {loading ? (
        <div className="activity-loading">
          <span className="spinner" /> Loading activity…
        </div>
      ) : movements.length === 0 ? (
        <div className="empty-state compact">
          <span>No stock movements recorded yet.</span>
        </div>
      ) : (
        <div className="activity-list">
          {movements.map((movement) => (
            <article key={movement.id}>
              <span className={`movement-mark ${movement.change > 0 ? 'positive' : 'negative'}`}>
                {movement.change > 0 ? '+' : '−'}
              </span>
              <div>
                <strong>
                  {labels[movement.type]} {movement.itemName}
                </strong>
                <span>
                  {movement.reason} · {movement.performedByName} · {movement.previousQuantity} →{' '}
                  {movement.newQuantity}
                </span>
              </div>
              <strong className={movement.change > 0 ? 'positive-number' : 'negative-number'}>
                {movement.change > 0 ? '+' : ''}
                {movement.change}
              </strong>
              <time dateTime={movement.createdAtUtc}>
                {new Intl.DateTimeFormat('en-US', {
                  month: 'short',
                  day: 'numeric',
                  hour: 'numeric',
                  minute: '2-digit',
                }).format(new Date(movement.createdAtUtc))}
              </time>
            </article>
          ))}
        </div>
      )}
    </section>
  )
}
