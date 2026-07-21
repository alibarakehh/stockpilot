import { ArchiveRestore, Eye, Pencil, RefreshCcw, Trash2 } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { InventoryItem, UserRole } from '../types'
import { StatusBadge } from './StatusBadge'

interface InventoryTableProps {
  items: InventoryItem[]
  loading: boolean
  role: UserRole
  archived?: boolean
  onEdit: (item: InventoryItem) => void
  onAdjust: (item: InventoryItem) => void
  onDelete: (item: InventoryItem) => void
  onRestore?: (item: InventoryItem) => void
}

function money(value: number, currencyCode: string) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: currencyCode }).format(value)
}

export function InventoryTable({
  items,
  loading,
  role,
  archived = false,
  onEdit,
  onAdjust,
  onDelete,
  onRestore,
}: InventoryTableProps) {
  if (loading) return <InventorySkeleton />
  if (!items.length)
    return (
      <div className="empty-state">
        <strong>{archived ? 'No archived items' : 'No matching items'}</strong>
        <span>
          {archived ? 'Archived products will appear here.' : 'Try changing the search or filters.'}
        </span>
      </div>
    )

  return (
    <>
      <div className="table-wrap desktop-inventory">
        <table>
          <thead>
            <tr>
              <th>Product</th>
              <th>Category</th>
              <th>Stock</th>
              <th>Status</th>
              <th>Purchase / sale</th>
              <th>Value</th>
              <th>
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id}>
                <td>
                  {archived ? (
                    <div className="product-link">
                      <strong>{item.name}</strong>
                      <span className="subtext">{item.sku}</span>
                    </div>
                  ) : (
                    <Link className="product-link" to={`/inventory/${item.id}`}>
                      <strong>{item.name}</strong>
                      <span className="subtext">{item.sku}</span>
                    </Link>
                  )}
                </td>
                <td>
                  {item.category}
                  {item.location && <span className="subtext">Location {item.location}</span>}
                </td>
                <td>
                  <strong>{item.quantity}</strong>
                  <span className="subtext">Reorder at {item.reorderLevel}</span>
                </td>
                <td>
                  <StatusBadge status={item.status} />
                </td>
                <td>
                  {money(item.purchasePrice, item.currencyCode)}
                  <span className="subtext">
                    Sell {money(item.sellingPrice, item.currencyCode)}
                  </span>
                </td>
                <td>
                  <strong>{money(item.inventoryValue, item.currencyCode)}</strong>
                </td>
                <td className="actions">
                  <ItemActions
                    item={item}
                    role={role}
                    archived={archived}
                    onEdit={onEdit}
                    onAdjust={onAdjust}
                    onDelete={onDelete}
                    onRestore={onRestore}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="inventory-cards">
        {items.map((item) => (
          <article className="inventory-card" key={item.id}>
            <header>
              <div>
                {archived ? (
                  <strong>{item.name}</strong>
                ) : (
                  <Link to={`/inventory/${item.id}`}>{item.name}</Link>
                )}
                <span>
                  {item.sku} · {item.category}
                </span>
              </div>
              <StatusBadge status={item.status} />
            </header>
            <dl>
              <div>
                <dt>On hand</dt>
                <dd>{item.quantity}</dd>
              </div>
              <div>
                <dt>Reorder at</dt>
                <dd>{item.reorderLevel}</dd>
              </div>
              <div>
                <dt>Value</dt>
                <dd>{money(item.inventoryValue, item.currencyCode)}</dd>
              </div>
            </dl>
            <div className="card-actions">
              <ItemActions
                item={item}
                role={role}
                archived={archived}
                onEdit={onEdit}
                onAdjust={onAdjust}
                onDelete={onDelete}
                onRestore={onRestore}
              />
            </div>
          </article>
        ))}
      </div>
    </>
  )
}

function ItemActions({
  item,
  role,
  archived,
  onEdit,
  onAdjust,
  onDelete,
  onRestore,
}: Omit<InventoryTableProps, 'items' | 'loading'> & { item: InventoryItem }) {
  if (archived)
    return (
      <button className="text-button adjust" onClick={() => onRestore?.(item)}>
        <ArchiveRestore size={14} /> Restore
      </button>
    )
  return (
    <>
      <Link
        className="icon-action"
        aria-label={`View ${item.name}`}
        title="View details"
        to={`/inventory/${item.id}`}
      >
        <Eye size={15} />
      </Link>
      {role !== 'Viewer' && item.status !== 'Discontinued' && (
        <button className="text-button adjust" onClick={() => onAdjust(item)}>
          <RefreshCcw size={13} /> Stock
        </button>
      )}
      {role !== 'Viewer' && (
        <button
          className="icon-action"
          aria-label={`Edit ${item.name}`}
          title="Edit"
          onClick={() => onEdit(item)}
        >
          <Pencil size={15} />
        </button>
      )}
      {role === 'Admin' && (
        <button
          className="icon-action danger"
          aria-label={`Archive ${item.name}`}
          title="Archive"
          onClick={() => onDelete(item)}
        >
          <Trash2 size={15} />
        </button>
      )}
    </>
  )
}

function InventorySkeleton() {
  return (
    <div className="inventory-skeleton" aria-live="polite" aria-label="Loading inventory">
      {[1, 2, 3, 4].map((row) => (
        <span key={row} />
      ))}
    </div>
  )
}
