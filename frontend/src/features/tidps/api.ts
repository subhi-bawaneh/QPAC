import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/client'
import type { DeleteResult, ReplacePreview, TidpFile, UploadAccepted } from '@/shared/api/types'

// Every uploaded TIDP workbook, with its discipline and row counts. The explorer
// groups by discipline client-side; disciplines with no file still appear there, so
// the grouping is done where the discipline list is known rather than here.
export function useTidpFiles(projectId: string) {
  return useQuery({
    queryKey: ['tidp-files', projectId],
    queryFn: async () => {
      const { data } = await api.get<TidpFile[]>(`/api/projects/${projectId}/tidp-files`)
      return data
    },
  })
}

export function useReplacePreview(tidpFileId: string | null) {
  return useQuery({
    queryKey: ['tidp-files', 'replace-preview', tidpFileId],
    enabled: tidpFileId !== null,
    queryFn: async () => {
      const { data } = await api.get<ReplacePreview>(`/api/tidp-files/${tidpFileId}/replace-preview`)
      return data
    },
  })
}

export function useUploadTidp(projectId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (file: File) => {
      const body = new FormData()
      body.append('file', file, file.name)
      const { data } = await api.post<UploadAccepted>(
        `/api/projects/${projectId}/tidp-files`, body)
      return data
    },
    onSuccess: () => invalidate(queryClient),
  })
}

export function useReplaceTidp() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ tidpFileId, file }: { tidpFileId: string; file: File }) => {
      const body = new FormData()
      body.append('file', file, file.name)
      const { data } = await api.put<UploadAccepted>(`/api/tidp-files/${tidpFileId}`, body)
      return data
    },
    onSuccess: () => invalidate(queryClient),
  })
}

export function useDeleteTidp() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (tidpFileId: string) => {
      const { data } = await api.delete<DeleteResult>(`/api/tidp-files/${tidpFileId}`)
      return data
    },
    onSuccess: () => invalidate(queryClient),
  })
}

function invalidate(queryClient: ReturnType<typeof useQueryClient>) {
  for (const key of ['tidp-files', 'imports', 'documents', 'tracker', 'summary', 'findings']) {
    void queryClient.invalidateQueries({ queryKey: [key] })
  }
}
