import { useMutation, useQuery } from '@tanstack/react-query'
import { api } from '../../api'

export const aiKeys = {
  all: ['ai'] as const,
  draftAvailability: () => [...aiKeys.all, 'inventory-draft-availability'] as const,
}

export function useAiDraftAvailability(enabled: boolean) {
  return useQuery({
    queryKey: aiKeys.draftAvailability(),
    queryFn: api.aiDraftAvailability,
    enabled,
    staleTime: 5 * 60_000,
  })
}

export function useGenerateInventoryDraft() {
  return useMutation({ mutationFn: api.generateInventoryDraft })
}
