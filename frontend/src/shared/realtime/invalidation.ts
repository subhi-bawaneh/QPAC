import type { QueryClient } from '@tanstack/react-query'
import type { SyncEventName } from './syncHub'

// Which cached queries a hub event makes stale. Kept as data so the mapping is
// testable without a live connection.
export const invalidationMap: Record<SyncEventName, string[]> = {
  syncStarted: [],
  folderSynced: ['folders'],
  syncFinished: ['folders', 'drive-status'],
  fileQueued: ['folders'],
  fileImportStarted: ['folders'],
  fileImported: ['folders', 'imports', 'workbook', 'drafts'],
  fileFailed: ['folders', 'imports'],
  recalculationFinished: ['tracker', 'summary', 'findings', 'documents'],
}

export function invalidateFor(queryClient: QueryClient, event: SyncEventName) {
  for (const key of invalidationMap[event]) {
    void queryClient.invalidateQueries({ queryKey: [key] })
  }
}
