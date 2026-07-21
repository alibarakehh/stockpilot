import type { StockStatus } from '../types'

const labels: Record<StockStatus, string> = {
  InStock: 'In stock',
  LowStock: 'Low stock',
  OutOfStock: 'Out of stock',
  Ordered: 'Ordered',
  Discontinued: 'Discontinued',
}

export function StatusBadge({ status }: { status: StockStatus }) {
  return (
    <span className={`status ${status.toLowerCase()}`}>
      <i />
      {labels[status]}
    </span>
  )
}
