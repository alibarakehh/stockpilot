import { zodResolver } from '@hookform/resolvers/zod'
import { Sparkles, X } from 'lucide-react'
import { useCallback, useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { itemInputSchema, type ItemFormValues } from '../features/inventory/validation'
import { useDialogAccessibility } from '../features/ui/useDialogAccessibility'
import type { AiInventoryDraft, InventoryItem, ItemInput } from '../types'

interface ItemModalProps {
  item?: InventoryItem
  draft?: AiInventoryDraft
  categories: string[]
  currencyCode: string
  onClose: () => void
  onSave: (input: ItemInput) => Promise<void>
}

function FieldError({ message }: { message?: string }) {
  return message ? <small className="field-error">{message}</small> : null
}

export function ItemModal({
  item,
  draft,
  categories,
  currencyCode,
  onClose,
  onSave,
}: ItemModalProps) {
  const {
    formState: { errors, isSubmitting, isDirty },
    handleSubmit,
    register,
    setError,
    setValue,
    watch,
  } = useForm<ItemFormValues>({
    resolver: zodResolver(itemInputSchema),
    defaultValues: {
      name: item?.name ?? draft?.name ?? '',
      sku: item?.sku ?? draft?.sku ?? '',
      category: item?.category ?? draft?.category ?? '',
      description: item?.description ?? draft?.description ?? '',
      location: item?.location ?? draft?.location ?? '',
      supplier: item?.supplier ?? draft?.supplier ?? '',
      quantity: item?.quantity ?? draft?.quantity ?? 0,
      reorderLevel: item?.reorderLevel ?? draft?.reorderLevel ?? 5,
      purchasePrice: item?.purchasePrice ?? draft?.purchasePrice ?? 0,
      sellingPrice: item?.sellingPrice ?? draft?.sellingPrice ?? 0,
      lifecycleStatus: item?.lifecycleStatus ?? 'Active',
      procurementStatus: item?.procurementStatus ?? 'None',
      version: item?.version,
    },
  })
  const lifecycleStatus = watch('lifecycleStatus')
  const suggested = (field: string) => draft?.generatedFields.includes(field) || undefined

  const requestClose = useCallback(() => {
    if (!isDirty || window.confirm('Discard your unsaved changes?')) onClose()
  }, [isDirty, onClose])
  const dialog = useDialogAccessibility<HTMLFormElement>(requestClose)

  useEffect(() => {
    const preventUnload = (event: BeforeUnloadEvent) => {
      if (!isDirty) return
      event.preventDefault()
    }
    window.addEventListener('beforeunload', preventUnload)
    return () => window.removeEventListener('beforeunload', preventUnload)
  }, [isDirty])

  useEffect(() => {
    if (lifecycleStatus === 'Discontinued') setValue('procurementStatus', 'None')
  }, [lifecycleStatus, setValue])

  const submit = handleSubmit(async (input) => {
    try {
      await onSave(input)
    } catch (reason) {
      setError('root.server', {
        message: reason instanceof Error ? reason.message : 'Could not save item.',
        type: 'server',
      })
    }
  })

  return (
    <div
      className="modal-backdrop"
      onMouseDown={(event) => event.target === event.currentTarget && requestClose()}
    >
      <form
        ref={dialog}
        aria-labelledby="item-modal-title"
        aria-modal="true"
        className="modal"
        noValidate
        onSubmit={submit}
        role="dialog"
      >
        <header>
          <div>
            <span className="eyebrow">INVENTORY ITEM</span>
            <h2 id="item-modal-title">
              {item ? 'Edit item details' : draft ? 'Review AI-assisted draft' : 'Add a new item'}
            </h2>
          </div>
          <button className="icon-button" type="button" onClick={requestClose} aria-label="Close">
            <X aria-hidden="true" size={18} />
          </button>
        </header>
        {draft && (
          <div className="ai-review-banner">
            <Sparkles aria-hidden="true" size={17} />
            <div>
              <strong>Review every highlighted suggestion</strong>
              <span>Nothing is saved until you select “Add item.” All fields remain editable.</span>
            </div>
          </div>
        )}
        <div className="form-grid">
          <label className="span-2" data-ai-suggested={suggested('name')}>
            Item name
            <input
              {...register('name')}
              aria-invalid={Boolean(errors.name)}
              data-dialog-initial-focus
              maxLength={160}
            />
            <FieldError message={errors.name?.message} />
          </label>
          <label data-ai-suggested={suggested('sku')}>
            SKU
            <input {...register('sku')} aria-invalid={Boolean(errors.sku)} maxLength={80} />
            <FieldError message={errors.sku?.message} />
          </label>
          <label data-ai-suggested={suggested('category')}>
            Category
            <input
              {...register('category')}
              aria-invalid={Boolean(errors.category)}
              list="categories"
              maxLength={100}
            />
            <datalist id="categories">
              {categories.map((category) => (
                <option key={category}>{category}</option>
              ))}
            </datalist>
            <FieldError message={errors.category?.message} />
          </label>
          <label data-ai-suggested={suggested('location')}>
            Storage location
            <input
              {...register('location')}
              aria-invalid={Boolean(errors.location)}
              maxLength={120}
              placeholder="e.g. A-01 or Floor 2"
            />
            <FieldError message={errors.location?.message} />
          </label>
          <label data-ai-suggested={suggested('supplier')}>
            Supplier
            <input
              {...register('supplier')}
              aria-invalid={Boolean(errors.supplier)}
              maxLength={160}
              placeholder="Optional supplier name"
            />
            <FieldError message={errors.supplier?.message} />
          </label>
          <label data-ai-suggested={suggested('quantity')}>
            Quantity
            <input
              {...register('quantity', { valueAsNumber: true })}
              aria-invalid={Boolean(errors.quantity)}
              min="0"
              readOnly={Boolean(item)}
              type="number"
            />
            {item && <small>Use “Update stock” to preserve the audit trail.</small>}
            <FieldError message={errors.quantity?.message} />
          </label>
          <label data-ai-suggested={suggested('reorderLevel')}>
            Reorder level
            <input
              {...register('reorderLevel', { valueAsNumber: true })}
              aria-invalid={Boolean(errors.reorderLevel)}
              min="0"
              type="number"
            />
            <FieldError message={errors.reorderLevel?.message} />
          </label>
          <label data-ai-suggested={suggested('purchasePrice')}>
            Purchase price ({currencyCode})
            <input
              {...register('purchasePrice', { valueAsNumber: true })}
              aria-invalid={Boolean(errors.purchasePrice)}
              min="0"
              step="0.01"
              type="number"
            />
            <FieldError message={errors.purchasePrice?.message} />
          </label>
          <label data-ai-suggested={suggested('sellingPrice')}>
            Selling price ({currencyCode})
            <input
              {...register('sellingPrice', { valueAsNumber: true })}
              aria-invalid={Boolean(errors.sellingPrice)}
              min="0"
              step="0.01"
              type="number"
            />
            <FieldError message={errors.sellingPrice?.message} />
          </label>
          <label>
            Lifecycle
            <select {...register('lifecycleStatus')}>
              <option value="Active">Active</option>
              <option value="Discontinued">Discontinued</option>
            </select>
            <small>Controls whether the product remains operational.</small>
          </label>
          <label>
            Procurement
            {lifecycleStatus === 'Discontinued' ? (
              <>
                <input {...register('procurementStatus')} type="hidden" />
                <select
                  disabled
                  value="None"
                  aria-label="Procurement disabled for discontinued item"
                >
                  <option value="None">No open order</option>
                </select>
              </>
            ) : (
              <select {...register('procurementStatus')}>
                <option value="None">No open order</option>
                <option value="Ordered">Ordered</option>
              </select>
            )}
            <small>Tracks an outstanding replenishment order separately from stock health.</small>
          </label>
          <label className="span-2" data-ai-suggested={suggested('description')}>
            Description
            <textarea
              {...register('description')}
              aria-invalid={Boolean(errors.description)}
              maxLength={1000}
              rows={3}
            />
            <FieldError message={errors.description?.message} />
          </label>
        </div>
        {errors.root?.server?.message && (
          <div className="form-error" role="alert">
            {errors.root.server.message}
          </div>
        )}
        <footer>
          <button className="button secondary" type="button" onClick={requestClose}>
            Cancel
          </button>
          <button className="button primary" disabled={isSubmitting} type="submit">
            {isSubmitting ? 'Saving…' : item ? 'Save changes' : 'Add item'}
          </button>
        </footer>
      </form>
    </div>
  )
}
