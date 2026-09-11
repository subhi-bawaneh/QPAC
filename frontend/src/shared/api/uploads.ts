import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/client'
import type { UploadAccepted } from '@/shared/api/types'

// Baseline and picklists are single-file uploads with no preview: the baseline replaces
// wholesale (its outgoing rows are dumped to the audit log first, server-side) and the
// picklists upsert, leaving soft-deleted codes deleted. Neither is a decision the
// operator needs numbers to make, which is why only Aconex has a preview.
function useFileUpload(url: string, invalidates: string[]) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (file: File) => {
      const body = new FormData()
      body.append('file', file, file.name)
      const { data } = await api.post<UploadAccepted>(url, body)
      return data
    },
    onSuccess: () => {
      for (const key of [...invalidates, 'imports']) {
        void queryClient.invalidateQueries({ queryKey: [key] })
      }
    },
  })
}

export function useUploadBaseline(projectId: string) {
  return useFileUpload(
    `/api/projects/${projectId}/baseline/upload`,
    ['baseline', 'tracker', 'summary', 'findings'])
}

export function useUploadPicklists(projectId: string) {
  return useFileUpload(`/api/projects/${projectId}/picklists/upload`, ['picklists', 'lists'])
}
