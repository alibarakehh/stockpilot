import { zodResolver } from '@hookform/resolvers/zod'
import { ArrowRight, RotateCcw, Sparkles, X } from 'lucide-react'
import { useCallback } from 'react'
import { useForm } from 'react-hook-form'
import { useGenerateInventoryDraft } from '../features/ai/queries'
import { smartIntakeSchema, type SmartIntakeForm } from '../features/ai/validation'
import { useDialogAccessibility } from '../features/ui/useDialogAccessibility'
import type { AiInventoryDraft } from '../types'

export function SmartIntakeModal({
  onClose,
  onReview,
}: {
  onClose: () => void
  onReview: (draft: AiInventoryDraft) => void
}) {
  const generation = useGenerateInventoryDraft()
  const form = useForm<SmartIntakeForm>({
    resolver: zodResolver(smartIntakeSchema),
    defaultValues: { description: '' },
  })
  const requestClose = useCallback(() => {
    if (!form.formState.isDirty || window.confirm('Discard this Smart Intake description?'))
      onClose()
  }, [form.formState.isDirty, onClose])
  const dialog = useDialogAccessibility<HTMLFormElement>(requestClose)

  async function generate(input: SmartIntakeForm) {
    try {
      await generation.mutateAsync(input.description)
    } catch {
      // The mutation error remains visible and the original description stays editable.
    }
  }

  const draft = generation.data
  return (
    <div
      className="modal-backdrop"
      onMouseDown={(event) => event.target === event.currentTarget && requestClose()}
    >
      <form
        ref={dialog}
        aria-labelledby="smart-intake-title"
        aria-modal="true"
        className="modal smart-intake-modal"
        noValidate
        onSubmit={form.handleSubmit(generate)}
        role="dialog"
      >
        <header>
          <div>
            <span className="eyebrow">AI SMART INTAKE</span>
            <h2 id="smart-intake-title">Describe the inventory item</h2>
          </div>
          <button className="icon-button" type="button" onClick={requestClose} aria-label="Close">
            <X aria-hidden="true" size={18} />
          </button>
        </header>
        <div className="ai-safety-note">
          <Sparkles aria-hidden="true" size={18} />
          <p>
            <strong>AI creates a draft only.</strong> Review and edit every suggestion before the
            ordinary item form can save it.
          </p>
        </div>
        <label>
          Item description
          <textarea
            {...form.register('description', {
              onChange: () => generation.reset(),
            })}
            aria-invalid={Boolean(form.formState.errors.description)}
            data-dialog-initial-focus
            placeholder="Example: Add 25 Logitech MX Master mice under Electronics. Cost $72, sell $99, Shelf A3, reorder at five."
            rows={5}
          />
          <small>
            Include known quantity, category, prices, supplier, location, SKU, and reorder level.
          </small>
          {form.formState.errors.description?.message && (
            <small className="field-error">{form.formState.errors.description.message}</small>
          )}
        </label>
        {generation.error && (
          <div className="form-error" role="alert">
            {generation.error instanceof Error
              ? generation.error.message
              : 'AI extraction is temporarily unavailable. You can still enter the item manually.'}
          </div>
        )}
        {draft && <DraftPreview draft={draft} />}
        <footer>
          <button className="button secondary" type="button" onClick={requestClose}>
            Cancel
          </button>
          {draft ? (
            <>
              <button className="button secondary" type="button" onClick={() => generation.reset()}>
                <RotateCcw size={14} /> Try again
              </button>
              <button className="button primary" type="button" onClick={() => onReview(draft)}>
                Review and edit <ArrowRight size={15} />
              </button>
            </>
          ) : (
            <button className="button primary" disabled={generation.isPending} type="submit">
              <Sparkles size={15} /> {generation.isPending ? 'Creating draft…' : 'Create draft'}
            </button>
          )}
        </footer>
      </form>
    </div>
  )
}

function DraftPreview({ draft }: { draft: AiInventoryDraft }) {
  return (
    <section className="draft-preview" aria-live="polite">
      <div className="draft-preview-heading">
        <span className="eyebrow">DRAFT READY</span>
        <strong>Nothing has been saved</strong>
      </div>
      <dl>
        <div>
          <dt>Item</dt>
          <dd>{draft.name}</dd>
        </div>
        <div>
          <dt>SKU</dt>
          <dd>{draft.sku || 'Needs input'}</dd>
        </div>
        <div>
          <dt>Category</dt>
          <dd>{draft.category}</dd>
        </div>
        <div>
          <dt>Quantity</dt>
          <dd>{draft.quantity}</dd>
        </div>
        <div>
          <dt>Purchase / sale</dt>
          <dd>
            {draft.purchasePrice} / {draft.sellingPrice}
          </dd>
        </div>
        <div>
          <dt>Location</dt>
          <dd>{draft.location || 'Not provided'}</dd>
        </div>
      </dl>
      {draft.warnings.map((warning) => (
        <p className="draft-warning" key={warning}>
          {warning}
        </p>
      ))}
    </section>
  )
}
