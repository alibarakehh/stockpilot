import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api'
import type { InventoryItem, PagedResult, User } from '../types'
import { Dashboard } from './Dashboard'

const viewer: User = {
  id: 'viewer',
  name: 'Vera Viewer',
  email: 'viewer@example.com',
  role: 'Viewer',
}
const admin: User = { ...viewer, id: 'admin', name: 'Amina Admin', role: 'Admin' }
const manager: User = { ...viewer, id: 'manager', name: 'Marco Manager', role: 'Manager' }
const item: InventoryItem = {
  id: 'item-1',
  name: 'Safety Gloves',
  sku: 'SAFE-1',
  category: 'Safety',
  description: 'Protective gloves',
  location: 'A-01',
  supplier: 'Safe Supply',
  quantity: 12,
  reorderLevel: 5,
  purchasePrice: 4,
  sellingPrice: 8,
  inventoryValue: 48,
  currencyCode: 'USD',
  lifecycleStatus: 'Active',
  procurementStatus: 'None',
  status: 'InStock',
  isArchived: false,
  deletedAtUtc: null,
  version: 1,
  createdAtUtc: '2026-07-20T10:00:00Z',
  updatedAtUtc: '2026-07-21T10:00:00Z',
}
const page: PagedResult<InventoryItem> = {
  items: [item],
  total: 1,
  page: 1,
  pageSize: 10,
  totalPages: 1,
}

beforeEach(() => {
  vi.spyOn(api, 'aiDraftAvailability').mockResolvedValue({
    available: true,
    provider: 'Fake',
    reason: null,
  })
})

afterEach(() => vi.restoreAllMocks())

describe('authenticated application routes', () => {
  it('gives a Viewer inventory access without management controls', async () => {
    vi.spyOn(api, 'inventory').mockResolvedValue(page)
    vi.spyOn(api, 'categories').mockResolvedValue(['Safety'])
    renderDashboard('/inventory', viewer)

    expect(await screen.findByRole('heading', { name: 'Inventory' })).toBeVisible()
    expect(await screen.findAllByText('Safety Gloves')).not.toHaveLength(0)
    expect(screen.queryByRole('button', { name: /add item/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /team/i })).not.toBeInTheDocument()
  })

  it('debounces search and sends it through the server query', async () => {
    const inventory = vi.spyOn(api, 'inventory').mockResolvedValue(page)
    vi.spyOn(api, 'categories').mockResolvedValue(['Safety'])
    const user = userEvent.setup()
    renderDashboard('/inventory', viewer)

    const search = await screen.findByRole('searchbox', { name: 'Search inventory' })
    await user.type(search, 'gloves')
    await waitFor(() =>
      expect(inventory).toHaveBeenCalledWith(expect.objectContaining({ search: 'gloves' })),
    )
  })

  it('gives a Manager operational controls without Admin capabilities', async () => {
    vi.spyOn(api, 'inventory').mockResolvedValue(page)
    vi.spyOn(api, 'categories').mockResolvedValue(['Safety'])
    renderDashboard('/inventory', manager)

    expect(await screen.findByRole('button', { name: /add item/i })).toBeVisible()
    expect(screen.queryByRole('tab', { name: /archive/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /team/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /archive safety gloves/i })).not.toBeInTheDocument()
  })

  it('keeps manual entry available when Smart Intake is not configured', async () => {
    vi.mocked(api.aiDraftAvailability).mockResolvedValue({
      available: false,
      provider: 'OpenAI',
      reason: 'AI Smart Intake requires a server-side provider key.',
    })
    vi.spyOn(api, 'inventory').mockResolvedValue(page)
    vi.spyOn(api, 'categories').mockResolvedValue(['Safety'])
    renderDashboard('/inventory', manager)

    expect(await screen.findByRole('button', { name: 'AI Smart Entry' })).toBeDisabled()
    expect(screen.getByText(/requires a server-side provider key/i)).toBeVisible()
    expect(screen.getByRole('button', { name: /add item/i })).toBeEnabled()
  })

  it('lets an Admin discover archived inventory', async () => {
    vi.spyOn(api, 'inventory').mockResolvedValue(page)
    vi.spyOn(api, 'categories').mockResolvedValue(['Safety'])
    const archived = vi
      .spyOn(api, 'archivedInventory')
      .mockResolvedValue({ ...page, items: [{ ...item, isArchived: true }] })
    const user = userEvent.setup()
    renderDashboard('/inventory', admin)

    await user.click(await screen.findByRole('button', { name: /archive/i }))
    await waitFor(() => expect(archived).toHaveBeenCalled())
    expect(await screen.findAllByRole('button', { name: /restore/i })).toHaveLength(2)
  })

  it('loads a dedicated item detail route and movement history', async () => {
    vi.spyOn(api, 'item').mockResolvedValue(item)
    vi.spyOn(api, 'movements').mockResolvedValue({
      items: [],
      total: 0,
      page: 1,
      pageSize: 50,
      totalPages: 0,
    })
    vi.spyOn(api, 'categories').mockResolvedValue(['Safety'])
    renderDashboard('/inventory/item-1', viewer)

    expect(await screen.findByRole('heading', { name: 'Safety Gloves' })).toBeVisible()
    expect(screen.getByText('Protective gloves')).toBeVisible()
    expect(screen.getByRole('link', { name: /back to inventory/i })).toHaveAttribute(
      'href',
      '/inventory',
    )
  })
})

function renderDashboard(path: string, user: User) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <MemoryRouter
      future={{ v7_relativeSplatPath: true, v7_startTransition: true }}
      initialEntries={[path]}
    >
      <QueryClientProvider client={queryClient}>
        <Dashboard user={user} onLogout={vi.fn()} />
      </QueryClientProvider>
    </MemoryRouter>,
  )
}
