import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/client'
import type { Workbook } from '@/shared/api/types'
import { rowToPayload } from './columns'
import type { WorkbookColumn } from '@/shared/api/types'

export const PAGE_SIZE = 500

export const workbookKeys = {
  sheet: (fileId: string, sheet: string) => ['workbook', fileId, sheet] as const,
}

// The sheet is paged as the user scrolls; the header block and column list
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

/** With the Draft layer gone there is one row to edit: the document itself. */
export function saveRowUrl(rowId: string): string {
  return `/api/documents/${rowId}`
}

export function useSaveRow(fileId: string, sheet: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: {
      rowId: string
      columns: WorkbookColumn[]
      cells: (string | null)[]
    }) => {
      const { data } = await api.put(
        saveRowUrl(input.rowId),
        rowToPayload(input.columns, input.cells),
      )
      return data
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: workbookKeys.sheet(fileId, sheet) })
    },
  })
}
