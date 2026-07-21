import { ArrowLeft, MapPin, PackageCheck, Pencil, RefreshCcw, Trash2, Truck } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import {
  useAdjustStock,
  useArchiveItem,
  useCategories,
  useInventoryItem,
  useMovements,
  useUpdateItem,
} from '../../features/inventory/queries'
import type { ItemInput, MovementType, User } from '../../types'
import { ActivityPanel } from '../ActivityPanel'
import { ConfirmDialog } from '../ConfirmDialog'
import { ErrorState, PageHeader } from '../Dashboard'
import { ItemModal } from '../ItemModal'
import { StatusBadge } from '../StatusBadge'
import { StockAdjustmentModal } from '../StockAdjustmentModal'

export function ItemDetailPage({ user }: { user: User }) {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const itemQuery = useInventoryItem(id)
  const movements = useMovements(id, 1, 50)
  const categories = useCategories()
  const updateItem = useUpdateItem()
  const adjustStock = useAdjustStock()
  const archiveItem = useArchiveItem()
  const [editing, setEditing] = useState(false)
  const [adjusting, setAdjusting] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [message, setMessage] = useState('')
  const item = itemQuery.data

  if (itemQuery.isError)
    return (
      <>
        <PageHeader eyebrow="Inventory" title="Item details" />
        <ErrorState error={itemQuery.error} onRetry={() => void itemQuery.refetch()} />
      </>
    )
  if (!item)
    return (
      <>
        <PageHeader eyebrow="Inventory" title="Item details" />
        <div className="panel empty-state">
          <span className="spinner" /> Loading item…
        </div>
      </>
    )

  const itemId = item.id
  const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: item.currencyCode })
  async function save(input: ItemInput) {
    await updateItem.mutateAsync({ id: itemId, input })
    setEditing(false)
    flash('Item details updated.')
  }
  async function adjust(input: {
    requestId: string
    change: number
    type: MovementType
    reason: string
    version: number
  }) {
    await adjustStock.mutateAsync({ id: itemId, input })
    setAdjusting(false)
    flash('Stock movement recorded.')
  }
  function flash(text: string) {
    setMessage(text)
    window.setTimeout(() => setMessage(''), 3500)
  }

  return (
    <>
      <Link className="back-link" to="/inventory">
        <ArrowLeft size={15} /> Back to inventory
      </Link>
      <PageHeader
        eyebrow={item.sku}
        title={item.name}
        description={item.description || 'No description has been added.'}
        actions={
          <div className="detail-actions">
            {user.role !== 'Viewer' && item.status !== 'Discontinued' && (
              <button className="button primary" onClick={() => setAdjusting(true)}>
                <RefreshCcw size={15} /> Update stock
              </button>
            )}
            {user.role !== 'Viewer' && (
              <button className="button secondary" onClick={() => setEditing(true)}>
                <Pencil size={15} /> Edit
              </button>
            )}
            {user.role === 'Admin' && (
              <button
                className="icon-button danger"
                aria-label="Archive item"
                title="Archive item"
                onClick={() => setDeleting(true)}
              >
                <Trash2 size={16} />
              </button>
            )}
          </div>
        }
      />
      {message && (
        <div className="toast" role="status" aria-live="polite">
          {message}
        </div>
      )}
      <section className="detail-grid">
        <article className="panel stock-hero">
          <div>
            <span className="eyebrow">Stock on hand</span>
            <strong>{item.quantity}</strong>
            <StatusBadge status={item.status} />
          </div>
          <progress
            className="stock-meter"
            max="100"
            value={Math.min(
              100,
              item.reorderLevel ? (item.quantity / (item.reorderLevel * 2)) * 100 : 100,
            )}
            aria-label={`Stock level: ${item.quantity} units on hand; reorder level ${item.reorderLevel}`}
          />
          <p>
            Reorder level: <strong>{item.reorderLevel}</strong>
          </p>
        </article>
        <article className="panel detail-card">
          <PackageCheck size={20} />
          <div>
            <span>Inventory value</span>
            <strong>{money.format(item.inventoryValue)}</strong>
            <small>
              {money.format(item.purchasePrice)} purchase · {money.format(item.sellingPrice)} sale
            </small>
          </div>
        </article>
        <article className="panel detail-card">
          <MapPin size={20} />
          <div>
            <span>Storage location</span>
            <strong>{item.location || 'Not assigned'}</strong>
            <small>{item.category}</small>
          </div>
        </article>
        <article className="panel detail-card">
          <Truck size={20} />
          <div>
            <span>Supplier</span>
            <strong>{item.supplier || 'Not assigned'}</strong>
            <small>
              {item.procurementStatus === 'Ordered' ? 'Replenishment ordered' : 'No open order'}
            </small>
          </div>
        </article>
      </section>
      <section className="item-metadata panel">
        <div>
          <span>Created</span>
          <strong>{formatDate(item.createdAtUtc)}</strong>
        </div>
        <div>
          <span>Last updated</span>
          <strong>{formatDate(item.updatedAtUtc)}</strong>
        </div>
        <div>
          <span>Lifecycle</span>
          <strong>{item.lifecycleStatus}</strong>
        </div>
        <div>
          <span>Version</span>
          <strong>{item.version}</strong>
        </div>
      </section>
      {movements.isError ? (
        <ErrorState error={movements.error} onRetry={() => void movements.refetch()} />
      ) : (
        <ActivityPanel movements={movements.data?.items ?? []} loading={movements.isLoading} />
      )}
      {editing && (
        <ItemModal
          item={item}
          categories={categories.data ?? []}
          currencyCode={item.currencyCode}
          onClose={() => setEditing(false)}
          onSave={save}
        />
      )}
      {adjusting && (
        <StockAdjustmentModal item={item} onClose={() => setAdjusting(false)} onSave={adjust} />
      )}
      {deleting && (
        <ConfirmDialog
          title={`Archive ${item.name}?`}
          message="The product leaves active inventory, while its stock and audit history remain available to administrators."
          confirmLabel="Archive item"
          onCancel={() => setDeleting(false)}
          onConfirm={async () => {
            await archiveItem.mutateAsync(item.id)
            navigate('/inventory', { replace: true })
          }}
        />
      )}
    </>
  )
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' }).format(
    new Date(value),
  )
}
