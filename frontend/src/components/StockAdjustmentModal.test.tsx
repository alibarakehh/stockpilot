import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, it, vi } from 'vitest'
import type { InventoryItem } from '../types'
import { StockAdjustmentModal } from './StockAdjustmentModal'

const item: InventoryItem = {
  id: 'item-1',
  name: 'USB-C Dock',
  sku: 'TECH-1002',
  category: 'Electronics',
  description: '',
  location: 'A-02',
  supplier: 'Northstar Tech',
  quantity: 4,
  reorderLevel: 10,
  purchasePrice: 119,
  sellingPrice: 169,
  inventoryValue: 476,
  currencyCode: 'USD',
  lifecycleStatus: 'Active',
  procurementStatus: 'None',
  status: 'LowStock',
  isArchived: false,
  deletedAtUtc: null,
  version: 3,
  createdAtUtc: '2026-07-21T10:00:00Z',
  updatedAtUtc: '2026-07-21T10:00:00Z',
}

it('previews the resulting balance and submits an attributable issue', async () => {
  const user = userEvent.setup()
  const onSave = vi.fn().mockResolvedValue(undefined)
  render(<StockAdjustmentModal item={item} onClose={vi.fn()} onSave={onSave} />)

  await user.selectOptions(screen.getByLabelText(/^Movement type/), 'Issue')
  fireEvent.change(screen.getByLabelText('Quantity'), { target: { value: '3' } })
  await user.type(screen.getByLabelText('Reason or reference'), 'E2E customer shipment')

  const preview = screen.getByText('After movement').closest('div')
  expect(preview).not.toBeNull()
  expect(within(preview!).getByText('1')).toBeVisible()
  await user.click(screen.getByRole('button', { name: 'Record movement' }))

  await waitFor(() =>
    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({
        change: -3,
        type: 'Issue',
        reason: 'E2E customer shipment',
        version: 3,
      }),
    ),
  )
})

it('prevents a previewed movement from making stock negative', async () => {
  const user = userEvent.setup()
  const onSave = vi.fn()
  render(<StockAdjustmentModal item={item} onClose={vi.fn()} onSave={onSave} />)

  await user.selectOptions(screen.getByLabelText(/^Movement type/), 'Damage')
  fireEvent.change(screen.getByLabelText('Quantity'), { target: { value: '5' } })
  await user.type(screen.getByLabelText('Reason or reference'), 'Damaged shipment')

  const preview = screen.getByText('After movement').closest('div')
  expect(preview).not.toBeNull()
  expect(within(preview!).getByText('-1')).toBeVisible()
  expect(screen.getByRole('button', { name: 'Record movement' })).toBeDisabled()
  expect(onSave).not.toHaveBeenCalled()
})
