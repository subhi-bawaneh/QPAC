import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/client'
import type { DataTarget, Workbook } from '@/shared/api/types'
import { rowToPayload } from './columns'
import type { WorkbookColumn } from '@/shared/api/types'

export const PAGE_SIZE = 500

export const workbookKeys = {
  sheet: (fileId: string, sheet: string) => ['workbook', fileId, sheet] as const,
}

// The sheet is paged as the user scrolls; the header block, column list and layer
// come back on every page and only the first one is kept.
export function useWorkbook(fileId: string, sheet: string) {
  return useInfiniteQuery({
    queryKey: workbookKeys.sheet(fileId, sheet),
    initialPageParam: 1,
    queryFn: async ({ pageParam }) => {
      const { data } = await api.get<Workbook>(
        `/api/folder-files/${fileId}/workbook`,
        { params: { sheet, page: pageParam, pageSize: PAGE_SIZE } },
      )
      return data
    },
    getNextPageParam: (last) => {
      const loaded = last.sheet.page * last.sheet.pageSize
      return loaded < last.sheet.totalRows ? last.sheet.page + 1 : undefined
    },
  })
}

/** The Draft layer edits the draft row; the Live layer edits the document. */
export function saveRowUrl(layer: DataTarget, rowId: string): string {
  return layer === 'Draft' ? `/api/drafts/documents/${rowId}` : `/api/documents/${rowId}`
}

export function useSaveRow(fileId: string, sheet: string, layer: DataTarget) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: {
      rowId: string
      columns: WorkbookColumn[]
      cells: (string | null)[]
    }) => {
      const { data } = await api.put(
        saveRowUrl(layer, input.rowId),
        rowToPayload(input.columns, input.cells),
      )
      return data
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: workbookKeys.sheet(fileId, sheet) })
    },
  })
}
