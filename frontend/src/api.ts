import type {
  AuthResponse,
  InventoryIntelligence,
  InventoryItem,
  InventoryMovement,
  InventorySummary,
  ItemInput,
  MovementType,
  PagedResult,
  StockStatus,
  User,
  UserRole,
  AiInventoryDraft,
  AiSmartIntakeAvailability,
} from './types'
import { UNAUTHORIZED_EVENT } from './features/auth/session'

const API_URL = (import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '')
const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS'])
let antiforgeryToken: string | null = null

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly code?: string,
    public readonly traceId?: string,
  ) {
    super(message)
  }
}

async function getAntiforgeryToken(): Promise<string> {
  if (antiforgeryToken) return antiforgeryToken

  const response = await fetch(`${API_URL}/api/auth/antiforgery`, {
    credentials: 'include',
  })
  if (!response.ok) throw new ApiError('Unable to establish a secure session.', response.status)

  const body = (await response.json()) as { requestToken: string }
  antiforgeryToken = body.requestToken
  return body.requestToken
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const method = (options.method ?? 'GET').toUpperCase()
  const headers = new Headers(options.headers)
  headers.set('Content-Type', 'application/json')
  if (!SAFE_METHODS.has(method)) headers.set('X-CSRF-TOKEN', await getAntiforgeryToken())
  const response = await fetch(`${API_URL}${path}`, {
    ...options,
    credentials: 'include',
    headers,
  })

  if (!response.ok) {
    const body = (await response.json().catch(() => null)) as {
      message?: string
      detail?: string
      title?: string
      errors?: Record<string, string[]>
      code?: string
      traceId?: string
    } | null
    if (response.status === 401 && path !== '/api/auth/login') {
      antiforgeryToken = null
      window.dispatchEvent(new Event(UNAUTHORIZED_EVENT))
    }
    const validationMessage = body?.errors ? Object.values(body.errors).flat()[0] : undefined
    throw new ApiError(
      validationMessage ??
        body?.detail ??
        body?.message ??
        body?.title ??
        `Request failed (${response.status})`,
      response.status,
      body?.code,
      body?.traceId,
    )
  }

  return response.status === 204 ? (undefined as T) : (response.json() as Promise<T>)
}

export const api = {
  login: async (email: string, password: string) => {
    const response = await request<AuthResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })
    antiforgeryToken = null
    return response
  },
  currentUser: () => request<User>('/api/auth/me'),
  logout: async () => {
    await request<void>('/api/auth/logout', { method: 'POST' })
    antiforgeryToken = null
  },

  inventory: (filters: {
    search?: string
    category?: string
    supplier?: string
    location?: string
    minQuantity?: number
    maxQuantity?: number
    status?: StockStatus | ''
    page?: number
    pageSize?: number
    sortBy?: string
    descending?: boolean
  }) => {
    const params = new URLSearchParams({
      pageSize: String(filters.pageSize ?? 10),
      page: String(filters.page ?? 1),
    })
    if (filters.search) params.set('search', filters.search)
    if (filters.category) params.set('category', filters.category)
    if (filters.supplier) params.set('supplier', filters.supplier)
    if (filters.location) params.set('location', filters.location)
    if (filters.minQuantity !== undefined) params.set('minQuantity', String(filters.minQuantity))
    if (filters.maxQuantity !== undefined) params.set('maxQuantity', String(filters.maxQuantity))
    if (filters.status) params.set('status', filters.status)
    if (filters.sortBy) params.set('sortBy', filters.sortBy)
    if (filters.descending !== undefined) params.set('descending', String(filters.descending))
    return request<PagedResult<InventoryItem>>(`/api/inventory?${params}`)
  },
  archivedInventory: (filters: { search?: string; page?: number; pageSize?: number } = {}) => {
    const params = new URLSearchParams({
      pageSize: String(filters.pageSize ?? 20),
      page: String(filters.page ?? 1),
    })
    if (filters.search) params.set('search', filters.search)
    return request<PagedResult<InventoryItem>>(`/api/inventory/archived?${params}`)
  },
  item: (id: string) => request<InventoryItem>(`/api/inventory/${id}`),
  summary: () => request<InventorySummary>('/api/inventory/summary'),
  categories: () => request<string[]>('/api/inventory/categories'),
  movements: (itemId?: string, page = 1, pageSize = 12) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (itemId) params.set('itemId', itemId)
    return request<PagedResult<InventoryMovement>>(`/api/inventory/movements?${params}`)
  },
  createItem: (item: ItemInput) =>
    request<InventoryItem>('/api/inventory', {
      method: 'POST',
      body: JSON.stringify(item),
    }),
  updateItem: (id: string, item: ItemInput) =>
    request<InventoryItem>(`/api/inventory/${id}`, {
      method: 'PUT',
      body: JSON.stringify(item),
    }),
  deleteItem: (id: string) => request<void>(`/api/inventory/${id}`, { method: 'DELETE' }),
  restoreItem: (id: string) =>
    request<InventoryItem>(`/api/inventory/${id}/restore`, { method: 'POST' }),
  adjustStock: (
    id: string,
    input: {
      requestId: string
      change: number
      type: MovementType
      reason: string
      version: number
    },
  ) =>
    request<InventoryItem>(`/api/inventory/${id}/stock`, {
      method: 'PATCH',
      body: JSON.stringify(input),
    }),
  insights: () => request<InventoryIntelligence>('/api/ai/insights'),
  aiDraftAvailability: () =>
    request<AiSmartIntakeAvailability>('/api/ai/inventory-draft/availability'),
  generateInventoryDraft: (description: string) =>
    request<AiInventoryDraft>('/api/ai/inventory-draft', {
      method: 'POST',
      body: JSON.stringify({ description }),
    }),
  users: () => request<User[]>('/api/users'),
  createUser: (input: { name: string; email: string; password: string; role: UserRole }) =>
    request<User>('/api/users', { method: 'POST', body: JSON.stringify(input) }),
  changeRole: (id: string, role: UserRole) =>
    request<User>(`/api/users/${id}/role`, {
      method: 'PATCH',
      body: JSON.stringify({ role }),
    }),
}
