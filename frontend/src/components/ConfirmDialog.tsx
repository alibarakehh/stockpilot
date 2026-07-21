import { useCallback, useState } from 'react'
import { useDialogAccessibility } from '../features/ui/useDialogAccessibility'

interface ConfirmDialogProps {
  title: string
  message: string
  confirmLabel: string
  onCancel: () => void
  onConfirm: () => Promise<void>
}

export function ConfirmDialog({
  title,
  message,
  confirmLabel,
  onCancel,
  onConfirm,
}: ConfirmDialogProps) {
  const [working, setWorking] = useState(false)
  const [error, setError] = useState('')
  const requestCancel = useCallback(() => {
    if (!working) onCancel()
  }, [onCancel, working])
  const dialog = useDialogAccessibility<HTMLElement>(requestCancel)

  async function confirm() {
    setWorking(true)
    setError('')
    try {
      await onConfirm()
    } catch (problem) {
      setError(problem instanceof Error ? problem.message : 'The action could not be completed.')
      setWorking(false)
    }
  }

  return (
    <div
      className="modal-backdrop"
      onMouseDown={(event) => event.target === event.currentTarget && !working && onCancel()}
    >
      <section
        ref={dialog}
        className="confirm-dialog"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="confirm-title"
        aria-describedby="confirm-message"
      >
        <span className="warning-mark">!</span>
        <h2 id="confirm-title">{title}</h2>
        <p id="confirm-message">{message}</p>
        {error && (
          <div className="form-error" role="alert">
            {error}
          </div>
        )}
        <div>
          <button className="button secondary" disabled={working} onClick={onCancel}>
            Cancel
          </button>
          <button
            data-dialog-initial-focus
            className="button destructive"
            disabled={working}
            onClick={() => void confirm()}
          >
            {working ? 'Working…' : confirmLabel}
          </button>
        </div>
      </section>
    </div>
  )
}
