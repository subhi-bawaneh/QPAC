import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/client'
import type {
  DraftDocument, DraftDocumentEdit, DraftRowState, PagedResult,
  PromoteDiff, PromoteResult, RollbackResult,
} from '@/shared/api/types'

export interface DraftFilters {
  state?: DraftRowState
  isDuplicate?: boolean
  search?: string
  page: number
  pageSize: number
}

export const draftKeys = {
  list: (folderFileId: string, filters: DraftFilters) =>
    ['drafts', folderFileId, filters] as const,
  diff: (folderFileId: string) => ['drafts', 'diff', folderFileId] as const,
}

export function useDraftDocuments(folderFileId: string, filters: DraftFilters) {
  return useQuery({
    queryKey: draftKeys.list(folderFileId, filters),
    queryFn: async () => {
      const { data } = await api.get<PagedResult<DraftDocument>>('/api/drafts/documents', {
        params: {
          folderFileId,
          state: filters.state,
          isDuplicate: filters.isDuplicate,
          search: filters.search || undefined,
          page: filters.page,
          pageSize: filters.pageSize,
        },
      })
      return data
    },
    // Keeps the previous page visible while the next one loads, so the table
    // does not blank out on every keystroke of the search box.
    placeholderData: (previous) => previous,
  })
}

/** Everything a draft edit or promote touches, refetched together. */
function useDraftInvalidation(folderFileId: string) {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: ['drafts'] })
    void queryClient.invalidateQueries({ queryKey: draftKeys.diff(folderFileId) })
  }
}

export function useUpdateDraftDocument(folderFileId: string) {
  const invalidate = useDraftInvalidation(folderFileId)
  return useMutation({
    mutationFn: async (input: { id: string; edit: DraftDocumentEdit }) => {
      // The endpoint is a full replace: every editable field travels, so an
      // omitted one would be cleared rather than left alone.
      const { data } = await api.put<DraftDocument>(`/api/drafts/documents/${input.id}`, input.edit)
      return data
    },
    onSuccess: invalidate,
  })
}

export function useBulkUpdateDraftDocuments(folderFileId: string) {
  const invalidate = useDraftInvalidation(folderFileId)
  return useMutation({
    mutationFn: async (input: { ids: string[]; fields: Record<string, string> }) => {
      const { data } = await api.put<number>('/api/drafts/documents/bulk', input)
      return data
    },
    onSuccess: invalidate,
  })
}

export function usePromoteDiff(folderFileId: string, enabled: boolean) {
  return useQuery({
    queryKey: draftKeys.diff(folderFileId),
    enabled,
    queryFn: async () => {
      const { data } = await api.get<PromoteDiff>('/api/drafts/promote-diff', {
        params: { folderFileId },
      })
      return data
    },
  })
}

export function usePromote(folderFileId: string) {
  const invalidate = useDraftInvalidation(folderFileId)
  return useMutation({
    mutationFn: async (input: { deleteMissing: boolean }) => {
      const { data } = await api.post<PromoteResult>('/api/drafts/promote', {
        folderFileId,
        deleteMissing: input.deleteMissing,
      })
      return data
    },
    onSuccess: invalidate,
  })
}

export function useRollbackPromote(folderFileId: string) {
  const invalidate = useDraftInvalidation(folderFileId)
  return useMutation({
    mutationFn: async (promoteBatchId: string) => {
      const { data } = await api.post<RollbackResult>(
        `/api/drafts/promote/${promoteBatchId}/rollback`,
      )
      return data
    },
    onSuccess: invalidate,
  })
}
