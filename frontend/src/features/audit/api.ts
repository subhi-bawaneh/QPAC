import { useQuery } from '@tanstack/react-query'
import { api } from '@/shared/api/client'
import type { AuditEntry, PagedResult } from '@/shared/api/types'

// One entity's history. The refactor plan deleted rollback and named the audit log as
// its replacement; this is the half that makes that true, because until something reads
// it the log answered "why is this drawing missing" to nobody.
export function useAuditHistory(projectId: string, entity: string, entityId: string | null) {
  return useQuery({
    queryKey: ['audit', projectId, entity, entityId],
    enabled: entityId !== null,
    queryFn: async () => {
      const { data } = await api.get<PagedResult<AuditEntry>>(
        `/api/projects/${projectId}/audit`,
        { params: { entity, entityId, pageSize: 100 } })
      return data
    },
  })
}
