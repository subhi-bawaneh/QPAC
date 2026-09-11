import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/client'
import type { AconexPreview, AconexUploadAccepted, ImportBatchSummary } from '@/shared/api/types'

function formDataOf(files: File[]) {
  const body = new FormData()
  for (const file of files) body.append('files', file, file.name)
  return body
}

// Counts what each file would add, writing nothing. The files stay in the browser and
// are sent again to confirm — the server never holds a copy it would have to store.
export function usePreviewAconex(projectId: string) {
  return useMutation({
    mutationFn: async (files: File[]) => {
      const { data } = await api.post<AconexPreview>(
        `/api/projects/${projectId}/aconex/preview`, formDataOf(files))
      return data
    },
  })
}

export function useUploadAconex(projectId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (files: File[]) => {
      const { data } = await api.post<AconexUploadAccepted>(
        `/api/projects/${projectId}/aconex/upload`, formDataOf(files))
      return data
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['imports'] })
    },
  })
}

export function useAconexBatches(projectId: string) {
  return useQuery({
    queryKey: ['imports', projectId, 'AconexHistory'],
    queryFn: async () => {
      const { data } = await api.get<ImportBatchSummary[]>(
        `/api/projects/${projectId}/imports?kind=AconexHistory&take=25`)
      return data
    },
  })
}
