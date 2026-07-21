import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, it, vi } from 'vitest'
import type { AiInventoryDraft } from '../types'
import { ItemModal } from './ItemModal'

const draft: AiInventoryDraft = {
  name: 'MX Master 3S Wireless Mouse',
  sku: 'LOGI-MX3S',
  description: 'Logitech wireless mouse',
  category: 'Electronics',
  quantity: 25,
  reorderLevel: 5,
  purchasePrice: 72,
  sellingPrice: 99,
  supplier: 'Logitech',
  location: 'Shelf A3',
  generatedFields: [
    'name',
    'sku',
    'description',
    'category',
    'quantity',
    'reorderLevel',
    'purchasePrice',
    'sellingPrice',
    'supplier',
    'location',
  ],
  warnings: [],
}

it('keeps AI suggestions editable and saves only after explicit review', async () => {
  const user = userEvent.setup()
  const onSave = vi.fn().mockResolvedValue(undefined)

  render(
    <ItemModal
      categories={['Electronics']}
      currencyCode="USD"
      draft={draft}
      onClose={vi.fn()}
      onSave={onSave}
    />,
  )

  expect(screen.getByRole('heading', { name: 'Review AI-assisted draft' })).toBeVisible()
  expect(screen.getByText(/Nothing is saved until/i)).toBeVisible()
  expect(screen.getByLabelText('Item name')).toHaveValue(draft.name)
  expect(screen.getByLabelText('Item name').closest('label')).toHaveAttribute(
    'data-ai-suggested',
    'true',
  )
  expect(onSave).not.toHaveBeenCalled()

  const name = screen.getByLabelText('Item name')
  await user.clear(name)
  await user.type(name, 'Reviewed MX Master 3S')
  await user.click(screen.getByRole('button', { name: 'Add item' }))

  await waitFor(() =>
    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({
        name: 'Reviewed MX Master 3S',
        quantity: 25,
        sku: 'LOGI-MX3S',
      }),
    ),
  )
})
