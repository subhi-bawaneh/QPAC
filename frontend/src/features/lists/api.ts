import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/client'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import type {
  PicklistField, PicklistGroup, PicklistItem, StatusMappingRow, UnifiedStatus,
} from '@/shared/api/types'

export const listKeys = {
  picklists: (includeDeleted: boolean) => ['picklists', QPAC_PROJECT_ID, includeDeleted] as const,
  statusMappings: (includeDeleted: boolean) =>
    ['status-mappings', QPAC_PROJECT_ID, includeDeleted] as const,
}

export function usePicklists(includeDeleted: boolean) {
  return useQuery({
    queryKey: listKeys.picklists(includeDeleted),
    queryFn: async () => {
      const { data } = await api.get<PicklistGroup[]>(
        `/api/projects/${QPAC_PROJECT_ID}/picklists`, { params: { includeDeleted } })
      return data
    },
  })
}

export function useStatusMappings(includeDeleted: boolean) {
  return useQuery({
    queryKey: listKeys.statusMappings(includeDeleted),
    queryFn: async () => {
      const { data } = await api.get<StatusMappingRow[]>(
        `/api/projects/${QPAC_PROJECT_ID}/status-mappings`, { params: { includeDeleted } })
      return data
    },
  })
}

// Both views of a list share one cache key prefix, so a write refreshes whichever
// of them is on screen.
function useListInvalidation() {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: ['picklists'] })
    void queryClient.invalidateQueries({ queryKey: ['status-mappings'] })
  }
}

export function useCreatePicklistItem() {
  const invalidate = useListInvalidation()
  return useMutation({
    mutationFn: async (input: {
      field: PicklistField
      code: string
      description: string
      sortOrder?: number | null
    }) => {
      const { data } = await api.post<PicklistItem | { item: PicklistItem; restored: boolean }>(
        `/api/projects/${QPAC_PROJECT_ID}/picklists`, { sortOrder: null, ...input })
      return data
    },
    onSuccess: invalidate,
  })
}

export function useUpdatePicklistItem() {
  const invalidate = useListInvalidation()
  return useMutation({
    mutationFn: async (input: { id: string; code: string; description: string; sortOrder: number }) => {
      const { data } = await api.put<PicklistItem>(`/api/picklists/${input.id}`, input)
      return data
    },
    onSuccess: invalidate,
  })
}

export function useDeletePicklistItem() {
  const invalidate = useListInvalidation()
  return useMutation({
    mutationFn: async (id: string) => { await api.delete(`/api/picklists/${id}`) },
    onSuccess: invalidate,
  })
}

export function useRestorePicklistItem() {
  const invalidate = useListInvalidation()
  return useMutation({
    mutationFn: async (id: string) => { await api.post(`/api/picklists/${id}/restore`) },
    onSuccess: invalidate,
  })
}

export function useReorderPicklist() {
  const invalidate = useListInvalidation()
  return useMutation({
    mutationFn: async (input: { field: PicklistField; ids: string[] }) => {
      await api.put(
        `/api/projects/${QPAC_PROJECT_ID}/picklists/${input.field}/order`, { ids: input.ids })
    },
    onSuccess: invalidate,
  })
}

export function useCreateStatusMapping() {
  const invalidate = useListInvalidation()
  return useMutation({
    mutationFn: async (input: { aconexStatus: string; status: UnifiedStatus; isLegacy: boolean }) => {
      const { data } = await api.post(
        `/api/projects/${QPAC_PROJECT_ID}/status-mappings`, input)
      return data
    },
    onSuccess: invalidate,
  })
}

export function useUpdateStatusMapping() {
  const invalidate = useListInvalidation()
  return useMutation({
    mutationFn: async (input: {
      id: string
      aconexStatus: string
      status: UnifiedStatus
      isLegacy: boolean
    }) => {
      const { data } = await api.put<StatusMappingRow>(`/api/status-mappings/${input.id}`, input)
      return data
    },
    onSuccess: invalidate,
  })
}

export function useDeleteStatusMapping() {
  const invalidate = useListInvalidation()
  return useMutation({
    mutationFn: async (id: string) => { await api.delete(`/api/status-mappings/${id}`) },
    onSuccess: invalidate,
  })
}

export function useRestoreStatusMapping() {
  const invalidate = useListInvalidation()
  return useMutation({
    mutationFn: async (id: string) => { await api.post(`/api/status-mappings/${id}/restore`) },
    onSuccess: invalidate,
  })
}
