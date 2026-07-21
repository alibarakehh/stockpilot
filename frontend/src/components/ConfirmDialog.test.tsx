import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, it, vi } from 'vitest'
import { ConfirmDialog } from './ConfirmDialog'

it('traps keyboard focus, closes with Escape, and restores prior focus', async () => {
  const user = userEvent.setup()
  const launcher = document.createElement('button')
  launcher.textContent = 'Open dialog'
  document.body.appendChild(launcher)
  launcher.focus()
  const cancel = vi.fn()
  const view = render(
    <ConfirmDialog
      title="Archive item?"
      message="History will be retained."
      confirmLabel="Archive item"
      onCancel={cancel}
      onConfirm={vi.fn()}
    />,
  )

  const confirm = screen.getByRole('button', { name: 'Archive item' })
  const cancelButton = screen.getByRole('button', { name: 'Cancel' })
  await waitFor(() => expect(confirm).toHaveFocus())
  await user.tab()
  expect(cancelButton).toHaveFocus()
  await user.tab({ shift: true })
  expect(confirm).toHaveFocus()
  await user.keyboard('{Escape}')
  expect(cancel).toHaveBeenCalledOnce()

  view.unmount()
  expect(launcher).toHaveFocus()
  launcher.remove()
})
