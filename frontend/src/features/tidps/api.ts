import { useQuery } from '@tanstack/react-query'
import { api } from '@/shared/api/client'
import type { TidpFile } from '@/shared/api/types'

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
