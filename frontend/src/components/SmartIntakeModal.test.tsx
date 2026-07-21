import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { api } from '../api'
import type { AiInventoryDraft } from '../types'
import { SmartIntakeModal } from './SmartIntakeModal'

const draft: AiInventoryDraft = {
  name: 'MX Master 3S Wireless Mouse',
  sku: 'LOGI-MX3S',
  description: 'Logitech wireless mouse',
  category: 'Electronics',
  quantity: 25,
  reorderLevel: 5,
  purchasePrice: 72,
  sellingPrice: 99,
  supplier: '',
  location: 'Shelf A3',
  generatedFields: ['name', 'sku', 'description', 'category', 'quantity'],
  warnings: [],
}

afterEach(() => vi.restoreAllMocks())

it('creates only a preview before the user explicitly opens the ordinary form', async () => {
  const user = userEvent.setup()
  vi.spyOn(api, 'generateInventoryDraft').mockResolvedValue(draft)
  const createItem = vi.spyOn(api, 'createItem')
  const review = vi.fn()
  renderModal(review)

  const description = screen.getByLabelText(/Item description/)
  await user.type(
    description,
    'Add 25 Logitech MX Master mice at $72, sell $99, Shelf A3, reorder at five.',
  )
  await user.click(screen.getByRole('button', { name: /create draft/i }))

  expect(await screen.findByText('Nothing has been saved')).toBeVisible()
  expect(screen.getByText('MX Master 3S Wireless Mouse')).toBeVisible()
  expect(createItem).not.toHaveBeenCalled()
  await user.click(screen.getByRole('button', { name: /review and edit/i }))
  expect(review).toHaveBeenCalledWith(draft)
  expect(createItem).not.toHaveBeenCalled()
})

it('preserves the original description when extraction fails', async () => {
  const user = userEvent.setup()
  vi.spyOn(api, 'generateInventoryDraft').mockRejectedValue(
    new Error('AI extraction is temporarily unavailable. You can still enter the item manually.'),
  )
  renderModal(vi.fn())
  const original = 'Add twelve barcode scanners to warehouse equipment.'

  const description = screen.getByLabelText(/Item description/)
  await user.type(description, original)
  await user.click(screen.getByRole('button', { name: /create draft/i }))

  expect(await screen.findByRole('alert')).toHaveTextContent('temporarily unavailable')
  expect(description).toHaveValue(original)
})

function renderModal(onReview: (value: AiInventoryDraft) => void) {
  const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <SmartIntakeModal onClose={vi.fn()} onReview={onReview} />
    </QueryClientProvider>,
  )
}
