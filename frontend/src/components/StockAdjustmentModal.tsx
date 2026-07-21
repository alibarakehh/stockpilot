import { useCallback, useMemo, useRef, useState, type FormEvent } from 'react'
import { useDialogAccessibility } from '../features/ui/useDialogAccessibility'
import type { InventoryItem, MovementType } from '../types'

interface StockAdjustmentModalProps {
  item: InventoryItem
  onClose: () => void
  onSave: (input: {
    requestId: string
    change: number
    type: MovementType
    reason: string
    version: number
  }) => Promise<void>
}

const options: { value: Exclude<MovementType, 'OpeningBalance'>; label: string; help: string }[] = [
  {
    value: 'Receipt',
    label: 'Receive stock',
    help: 'A supplier delivery or purchase order arrived',
  },
  { value: 'Issue', label: 'Issue stock', help: 'Stock was used, sold, or transferred out' },
  { value: 'Damage', label: 'Record damage', help: 'Stock is no longer usable' },
  { value: 'Return', label: 'Customer return', help: 'Usable stock returned to inventory' },
  { value: 'Correction', label: 'Count correction', help: 'Correct a physical-count discrepancy' },
]

export function StockAdjustmentModal({ item, onClose, onSave }: StockAdjustmentModalProps) {
  const requestId = useRef(crypto.randomUUID())
  const [type, setType] = useState<Exclude<MovementType, 'OpeningBalance'>>('Receipt')
  const [quantity, setQuantity] = useState(1)
  const [direction, setDirection] = useState<'add' | 'remove'>('add')
  const [reason, setReason] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const isDirty = type !== 'Receipt' || quantity !== 1 || reason.length > 0
  const requestClose = useCallback(() => {
    if (!isDirty || window.confirm('Discard this unrecorded stock movement?')) onClose()
  }, [isDirty, onClose])
  const dialog = useDialogAccessibility<HTMLFormElement>(requestClose)

  const removesStock =
    type === 'Issue' || type === 'Damage' || (type === 'Correction' && direction === 'remove')
  const change = removesStock ? -Math.abs(quantity) : Math.abs(quantity)
  const resultingQuantity = item.quantity + change
  const selected = useMemo(() => options.find((option) => option.value === type)!, [type])

  function selectType(next: Exclude<MovementType, 'OpeningBalance'>) {
    setType(next)
    if (next === 'Issue' || next === 'Damage') setDirection('remove')
    if (next === 'Receipt' || next === 'Return') setDirection('add')
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (resultingQuantity < 0) {
      setError(`Only ${item.quantity} units are available.`)
      return
    }
    setSaving(true)
    setError('')
    try {
      await onSave({ requestId: requestId.current, change, type, reason, version: item.version })
    } catch (problem) {
      setError(problem instanceof Error ? problem.message : 'Could not update stock.')
      setSaving(false)
    }
  }

  return (
    <div
      className="modal-backdrop"
      onMouseDown={(event) => event.target === event.currentTarget && requestClose()}
    >
      <form
        ref={dialog}
        className="modal adjustment-modal"
        onSubmit={submit}
        role="dialog"
        aria-modal="true"
        aria-labelledby="adjustment-title"
      >
        <header>
          <div>
            <span className="eyebrow">STOCK MOVEMENT</span>
            <h2 id="adjustment-title">Update {item.name}</h2>
          </div>
          <button className="icon-button" type="button" onClick={requestClose} aria-label="Close">
            ×
          </button>
        </header>

        <div className="stock-context">
          <div>
            <span>Current stock</span>
            <strong>{item.quantity}</strong>
          </div>
          <span className="stock-arrow">→</span>
          <div className={resultingQuantity < 0 ? 'invalid' : ''}>
            <span>After movement</span>
            <strong>{resultingQuantity}</strong>
          </div>
          <div>
            <span>Reorder level</span>
            <strong>{item.reorderLevel}</strong>
          </div>
        </div>

        <label>
          Movement type
          <select
            value={type}
            onChange={(event) =>
              selectType(event.target.value as Exclude<MovementType, 'OpeningBalance'>)
            }
            data-dialog-initial-focus
          >
            {options.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
          <small>{selected.help}</small>
        </label>

        {type === 'Correction' && (
          <fieldset className="direction-control">
            <legend>Correction direction</legend>
            <button
              type="button"
              className={direction === 'add' ? 'active' : ''}
              aria-pressed={direction === 'add'}
              onClick={() => setDirection('add')}
            >
              + Add
            </button>
            <button
              type="button"
              className={direction === 'remove' ? 'active' : ''}
              aria-pressed={direction === 'remove'}
              onClick={() => setDirection('remove')}
            >
              − Remove
            </button>
          </fieldset>
        )}

        <div className="adjustment-fields">
          <label>
            Quantity
            <input
              type="number"
              min="1"
              max="1000000"
              value={quantity}
              onChange={(event) => setQuantity(Math.max(1, Number(event.target.value)))}
              required
            />
          </label>
          <label>
            Reason or reference
            <input
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              minLength={2}
              maxLength={300}
              placeholder="e.g. PO-1048 or cycle count"
              required
            />
          </label>
        </div>

        {error && (
          <div className="form-error" role="alert">
            {error}
          </div>
        )}
        <footer>
          <button className="button secondary" type="button" onClick={requestClose}>
            Cancel
          </button>
          <button
            className="button primary"
            disabled={saving || resultingQuantity < 0}
            type="submit"
          >
            {saving ? 'Recording…' : 'Record movement'}
          </button>
        </footer>
      </form>
    </div>
  )
}
