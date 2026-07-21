import { Archive, ChevronDown, Plus, Search, Sparkles, SlidersHorizontal, X } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAiDraftAvailability } from '../../features/ai/queries'
import {
  useAdjustStock,
  useArchiveItem,
  useArchivedInventory,
  useCategories,
  useCreateItem,
  useInventory,
  useRestoreItem,
  useUpdateItem,
} from '../../features/inventory/queries'
import type {
  AiInventoryDraft,
  InventoryItem,
  ItemInput,
  MovementType,
  StockStatus,
  User,
} from '../../types'
import { ConfirmDialog } from '../ConfirmDialog'
import { ErrorState, PageHeader } from '../Dashboard'
import { InventoryTable } from '../InventoryTable'
import { ItemModal } from '../ItemModal'
import { Pagination } from '../Pagination'
import { SmartIntakeModal } from '../SmartIntakeModal'
import { StockAdjustmentModal } from '../StockAdjustmentModal'

const statusOptions: { value: StockStatus; label: string }[] = [
  { value: 'InStock', label: 'In stock' },
  { value: 'LowStock', label: 'Low stock' },
  { value: 'OutOfStock', label: 'Out of stock' },
  { value: 'Ordered', label: 'Ordered' },
  { value: 'Discontinued', label: 'Discontinued' },
]
const sortOptions = [
  ['updated', 'Recently updated'],
  ['name', 'Name'],
  ['sku', 'SKU'],
  ['category', 'Category'],
  ['quantity', 'Quantity'],
  ['value', 'Inventory value'],
  ['location', 'Location'],
  ['supplier', 'Supplier'],
]

export function InventoryPage({ user }: { user: User }) {
  const [params, setParams] = useSearchParams()
  const [search, setSearch] = useState(params.get('search') ?? '')
  const [advanced, setAdvanced] = useState(
    [...params.keys()].some((key) =>
      ['supplier', 'location', 'minQuantity', 'maxQuantity'].includes(key),
    ),
  )
  const [editing, setEditing] = useState<InventoryItem | null | undefined>()
  const [draft, setDraft] = useState<AiInventoryDraft>()
  const [smartIntakeOpen, setSmartIntakeOpen] = useState(false)
  const [adjusting, setAdjusting] = useState<InventoryItem | null>(null)
  const [deleting, setDeleting] = useState<InventoryItem | null>(null)
  const [restoring, setRestoring] = useState<InventoryItem | null>(null)
  const [message, setMessage] = useState('')
  const archived = params.get('view') === 'archived' && user.role === 'Admin'
  const page = positiveNumber(params.get('page'), 1)
  const filters = useMemo(
    () => ({
      search: params.get('search') || undefined,
      category: params.get('category') || undefined,
      supplier: params.get('supplier') || undefined,
      location: params.get('location') || undefined,
      minQuantity: optionalNumber(params.get('minQuantity')),
      maxQuantity: optionalNumber(params.get('maxQuantity')),
      status: (params.get('status') as StockStatus | null) ?? undefined,
      sortBy: params.get('sortBy') || 'updated',
      descending: params.get('descending') !== 'false',
      page,
      pageSize: 10,
    }),
    [params, page],
  )
  const inventory = useInventory(filters, !archived)
  const archivedInventory = useArchivedInventory(
    { search: filters.search, page, pageSize: 10 },
    archived,
  )
  const categories = useCategories()
  const aiAvailability = useAiDraftAvailability(user.role !== 'Viewer')
  const activeQuery = archived ? archivedInventory : inventory
  const pageData = activeQuery.data
  const createItem = useCreateItem()
  const updateItem = useUpdateItem()
  const adjustStock = useAdjustStock()
  const archiveItem = useArchiveItem()
  const restoreItem = useRestoreItem()

  useEffect(() => {
    const timer = window.setTimeout(() => {
      const current = params.get('search') ?? ''
      if (current === search.trim()) return
      const next = new URLSearchParams(params)
      if (search.trim()) next.set('search', search.trim())
      else next.delete('search')
      next.delete('page')
      setParams(next, { replace: true })
    }, 300)
    return () => window.clearTimeout(timer)
  }, [params, search, setParams])

  function setFilter(key: string, value: string) {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    next.delete('page')
    setParams(next)
  }
  function setView(nextView: 'active' | 'archived') {
    const next = new URLSearchParams()
    if (nextView === 'archived') next.set('view', 'archived')
    setSearch('')
    setParams(next)
  }
  function clearFilters() {
    const next = new URLSearchParams()
    if (archived) next.set('view', 'archived')
    setSearch('')
    setParams(next)
  }
  function notify(text: string) {
    setMessage(text)
    window.setTimeout(() => setMessage(''), 3500)
  }
  async function saveItem(input: ItemInput) {
    if (editing) await updateItem.mutateAsync({ id: editing.id, input })
    else await createItem.mutateAsync(input)
    notify(editing ? 'Item details updated.' : 'Item added to inventory.')
    setEditing(undefined)
    setDraft(undefined)
  }
  async function saveAdjustment(input: {
    requestId: string
    change: number
    type: MovementType
    reason: string
    version: number
  }) {
    if (!adjusting) return
    await adjustStock.mutateAsync({ id: adjusting.id, input })
    setAdjusting(null)
    notify('Stock movement recorded.')
  }
  const hasFilters = [...params.keys()].some(
    (key) => !['page', 'sortBy', 'descending', 'view'].includes(key),
  )
  const aiUnavailableReason = aiAvailability.error
    ? 'AI Smart Intake is temporarily unavailable. Manual item entry remains available.'
    : aiAvailability.data && !aiAvailability.data.available
      ? aiAvailability.data.reason
      : null

  return (
    <>
      <PageHeader
        eyebrow="Operations"
        title="Inventory"
        description="Search, control, and trace every product in your workspace."
        actions={
          user.role !== 'Viewer' && !archived ? (
            <div className="inventory-create-actions">
              <button
                className="button ai-button"
                disabled={!aiAvailability.data?.available}
                aria-describedby={aiUnavailableReason ? 'ai-availability-note' : undefined}
                onClick={() => setSmartIntakeOpen(true)}
              >
                <Sparkles size={15} />
                {aiAvailability.isLoading ? 'Checking AI…' : 'AI Smart Entry'}
              </button>
              <button
                className="button primary"
                onClick={() => {
                  setDraft(undefined)
                  setEditing(null)
                }}
              >
                <Plus size={16} /> Add item
              </button>
            </div>
          ) : undefined
        }
      />
      {user.role !== 'Viewer' && !archived && aiUnavailableReason && (
        <div className="ai-availability-note" id="ai-availability-note">
          <Sparkles aria-hidden="true" size={14} /> {aiUnavailableReason}
        </div>
      )}
      {message && (
        <div className="toast" role="status" aria-live="polite">
          {message}
        </div>
      )}
      {user.role === 'Admin' && (
        <div className="view-tabs" role="group" aria-label="Inventory views">
          <button
            aria-pressed={!archived}
            className={!archived ? 'active' : ''}
            onClick={() => setView('active')}
          >
            Active inventory
          </button>
          <button
            aria-pressed={archived}
            className={archived ? 'active' : ''}
            onClick={() => setView('archived')}
          >
            <Archive size={14} /> Archive
          </button>
        </div>
      )}
      {categories.isError && !archived && (
        <div className="global-alert" role="alert">
          Categories could not be loaded. You can still search and manage inventory.
          <button onClick={() => void categories.refetch()}>Retry</button>
        </div>
      )}
      <section className="panel inventory-panel">
        <div className="filters">
          <label className="search-field">
            <Search aria-hidden="true" size={18} />
            <span className="sr-only">Search inventory</span>
            <input
              type="search"
              placeholder={
                archived ? 'Search archived items…' : 'Search name, SKU, location, supplier…'
              }
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </label>
          {!archived && (
            <>
              <select
                aria-label="Filter by category"
                value={filters.category ?? ''}
                onChange={(event) => setFilter('category', event.target.value)}
              >
                <option value="">All categories</option>
                {(categories.data ?? []).map((value) => (
                  <option key={value}>{value}</option>
                ))}
              </select>
              <select
                aria-label="Filter by status"
                value={filters.status ?? ''}
                onChange={(event) => setFilter('status', event.target.value)}
              >
                <option value="">All statuses</option>
                {statusOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
              <button
                className={`button filter-toggle${advanced ? ' active' : ''}`}
                aria-expanded={advanced}
                onClick={() => setAdvanced((value) => !value)}
              >
                <SlidersHorizontal size={15} /> More filters <ChevronDown size={14} />
              </button>
            </>
          )}
          {hasFilters && (
            <button className="text-button clear-filter" onClick={clearFilters}>
              <X size={13} /> Clear
            </button>
          )}
        </div>
        {advanced && !archived && (
          <div className="advanced-filters">
            <label>
              Supplier
              <input
                value={filters.supplier ?? ''}
                onChange={(event) => setFilter('supplier', event.target.value)}
                placeholder="Any supplier"
              />
            </label>
            <label>
              Location
              <input
                value={filters.location ?? ''}
                onChange={(event) => setFilter('location', event.target.value)}
                placeholder="Any location"
              />
            </label>
            <label>
              Minimum quantity
              <input
                type="number"
                min="0"
                value={filters.minQuantity ?? ''}
                onChange={(event) => setFilter('minQuantity', event.target.value)}
              />
            </label>
            <label>
              Maximum quantity
              <input
                type="number"
                min="0"
                value={filters.maxQuantity ?? ''}
                onChange={(event) => setFilter('maxQuantity', event.target.value)}
              />
            </label>
          </div>
        )}
        <div className="results-context">
          <span>
            {activeQuery.isFetching
              ? 'Updating results…'
              : `${pageData?.total ?? 0} ${(pageData?.total ?? 0) === 1 ? 'item' : 'items'}`}
          </span>
          <FilterChips params={params} onRemove={(key) => setFilter(key, '')} />
          {!archived && (
            <div className="sort-control">
              <label htmlFor="inventory-sort">Sort</label>
              <select
                id="inventory-sort"
                value={filters.sortBy}
                onChange={(event) => setFilter('sortBy', event.target.value)}
              >
                {sortOptions.map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
              <button
                aria-label={filters.descending ? 'Sort descending' : 'Sort ascending'}
                onClick={() => setFilter('descending', String(!filters.descending))}
              >
                {filters.descending ? '↓' : '↑'}
              </button>
            </div>
          )}
        </div>
        {activeQuery.isError ? (
          <ErrorState error={activeQuery.error} onRetry={() => void activeQuery.refetch()} />
        ) : (
          <InventoryTable
            items={pageData?.items ?? []}
            loading={activeQuery.isLoading}
            role={user.role}
            archived={archived}
            onEdit={(item) => {
              setDraft(undefined)
              setEditing(item)
            }}
            onAdjust={setAdjusting}
            onDelete={setDeleting}
            onRestore={setRestoring}
          />
        )}
        <Pagination
          page={pageData?.page ?? page}
          pageSize={pageData?.pageSize ?? 10}
          total={pageData?.total ?? 0}
          totalPages={pageData?.totalPages ?? 0}
          setPage={(value) => setFilter('page', String(value))}
        />
      </section>
      {editing !== undefined && (
        <ItemModal
          item={editing ?? undefined}
          draft={draft}
          categories={categories.data ?? []}
          currencyCode={pageData?.items[0]?.currencyCode ?? 'USD'}
          onClose={() => {
            setEditing(undefined)
            setDraft(undefined)
          }}
          onSave={saveItem}
        />
      )}
      {smartIntakeOpen && (
        <SmartIntakeModal
          onClose={() => setSmartIntakeOpen(false)}
          onReview={(generatedDraft) => {
            setDraft(generatedDraft)
            setSmartIntakeOpen(false)
            setEditing(null)
          }}
        />
      )}
      {adjusting && (
        <StockAdjustmentModal
          item={adjusting}
          onClose={() => setAdjusting(null)}
          onSave={saveAdjustment}
        />
      )}
      {deleting && (
        <ConfirmDialog
          title={`Archive ${deleting.name}?`}
          message="The item will leave active inventory, but its stock and audit history will be retained."
          confirmLabel="Archive item"
          onCancel={() => setDeleting(null)}
          onConfirm={async () => {
            await archiveItem.mutateAsync(deleting.id)
            setDeleting(null)
            notify('Item archived. Its history was retained.')
          }}
        />
      )}
      {restoring && (
        <ConfirmDialog
          title={`Restore ${restoring.name}?`}
          message="The item will return to active inventory with its existing quantity and complete history."
          confirmLabel="Restore item"
          onCancel={() => setRestoring(null)}
          onConfirm={async () => {
            await restoreItem.mutateAsync(restoring.id)
            setRestoring(null)
            notify('Item restored to active inventory.')
          }}
        />
      )}
    </>
  )
}

function FilterChips({
  params,
  onRemove,
}: {
  params: URLSearchParams
  onRemove: (key: string) => void
}) {
  const labels: Record<string, string> = {
    search: 'Search',
    category: 'Category',
    status: 'Status',
    supplier: 'Supplier',
    location: 'Location',
    minQuantity: 'Min',
    maxQuantity: 'Max',
  }
  return (
    <div className="filter-chips">
      {Object.entries(labels).map(([key, label]) =>
        params.get(key) ? (
          <button key={key} onClick={() => onRemove(key)}>
            {label}: {params.get(key)} <X size={11} />
          </button>
        ) : null,
      )}
    </div>
  )
}

function positiveNumber(value: string | null, fallback: number) {
  const parsed = Number(value)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback
}
function optionalNumber(value: string | null) {
  if (value === null || value === '') return undefined
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : undefined
}
