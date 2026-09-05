import { useQuery } from '@tanstack/react-query'
import { api } from '@/shared/api/client'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import type { PagedResult, TrackerDocument, TrackerRow, UnifiedStatus } from '@/shared/api/types'

export interface TrackerFilters {
  discipline?: string
  status?: UnifiedStatus
  search?: string
  hasAconex?: boolean
  page: number
  pageSize: number
}

export function useTrackerRows(filters: TrackerFilters) {
  return useQuery({
    queryKey: ['tracker', QPAC_PROJECT_ID, filters],
    queryFn: async () => {
      const { data } = await api.get<PagedResult<TrackerRow>>(
        `/api/projects/${QPAC_PROJECT_ID}/tracker`,
        {
          params: {
            discipline: filters.discipline || undefined,
            status: filters.status,
            search: filters.search || undefined,
            hasAconex: filters.hasAconex,
            page: filters.page,
            pageSize: filters.pageSize,
          },
        },
      )
      return data
    },
    placeholderData: (previous) => previous,
  })
}

export function useTrackerDocument(documentId: string | null) {
  return useQuery({
    queryKey: ['tracker', 'document', documentId],
    enabled: documentId !== null,
    queryFn: async () => {
      const { data } = await api.get<TrackerDocument>(`/api/tracker/documents/${documentId}`)
      return data
    },
  })
}

/** The export endpoints return a file; the browser is handed a blob to save. */
export async function downloadReport(kind: string) {
  const response = await api.get<Blob>(
    `/api/projects/${QPAC_PROJECT_ID}/reports/${kind}/export`,
    { responseType: 'blob' },
  )

  const disposition = String(response.headers['content-disposition'] ?? '')
  const match = /filename=([^;]+)/i.exec(disposition)
  const fileName = match ? match[1].trim() : `${kind}.xlsx`

  const url = URL.createObjectURL(response.data)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}
