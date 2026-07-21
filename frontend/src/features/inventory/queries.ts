import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../api'
import type { ItemInput, MovementType, StockStatus } from '../../types'

export interface InventoryFilters {
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
}

export const inventoryKeys = {
  all: ['inventory'] as const,
  lists: () => [...inventoryKeys.all, 'list'] as const,
  list: (filters: InventoryFilters) => [...inventoryKeys.lists(), filters] as const,
  archived: (filters: { search?: string; page?: number; pageSize?: number }) =>
    [...inventoryKeys.all, 'archived', filters] as const,
  detail: (id: string) => [...inventoryKeys.all, 'detail', id] as const,
  categories: () => [...inventoryKeys.all, 'categories'] as const,
  summary: () => [...inventoryKeys.all, 'summary'] as const,
  movements: (itemId?: string, page = 1, pageSize = 12) =>
    [...inventoryKeys.all, 'movements', itemId ?? 'all', page, pageSize] as const,
  insights: () => [...inventoryKeys.all, 'insights'] as const,
}

export function useInventory(filters: InventoryFilters, enabled = true) {
  return useQuery({
    queryKey: inventoryKeys.list(filters),
    queryFn: () => api.inventory(filters),
    placeholderData: (previous) => previous,
    enabled,
  })
}

export function useArchivedInventory(
  filters: {
    search?: string
    page?: number
    pageSize?: number
  },
  enabled = true,
) {
  return useQuery({
    queryKey: inventoryKeys.archived(filters),
    queryFn: () => api.archivedInventory(filters),
    enabled,
  })
}

export function useInventoryItem(id: string) {
  return useQuery({ queryKey: inventoryKeys.detail(id), queryFn: () => api.item(id) })
}

export function useCategories() {
  return useQuery({ queryKey: inventoryKeys.categories(), queryFn: api.categories })
}

export function useInventorySummary() {
  return useQuery({ queryKey: inventoryKeys.summary(), queryFn: api.summary })
}

export function useMovements(itemId?: string, page = 1, pageSize = 12) {
  return useQuery({
    queryKey: inventoryKeys.movements(itemId, page, pageSize),
    queryFn: () => api.movements(itemId, page, pageSize),
    placeholderData: (previous) => previous,
  })
}

export function useInsights() {
  return useQuery({ queryKey: inventoryKeys.insights(), queryFn: api.insights })
}

function useInventoryMutation<TVariables>(mutationFn: (variables: TVariables) => Promise<unknown>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: inventoryKeys.all })
    },
  })
}

export function useCreateItem() {
  return useInventoryMutation((input: ItemInput) => api.createItem(input))
}

export function useUpdateItem() {
  return useInventoryMutation(({ id, input }: { id: string; input: ItemInput }) =>
    api.updateItem(id, input),
  )
}

export function useAdjustStock() {
  return useInventoryMutation(
    ({
      id,
      input,
    }: {
      id: string
      input: {
        requestId: string
        change: number
        type: MovementType
        reason: string
        version: number
      }
    }) => api.adjustStock(id, input),
  )
}

export function useArchiveItem() {
  return useInventoryMutation((id: string) => api.deleteItem(id))
}

export function useRestoreItem() {
  return useInventoryMutation((id: string) => api.restoreItem(id))
}
