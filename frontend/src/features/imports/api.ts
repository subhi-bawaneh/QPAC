import { useCallback, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, apiErrorMessage } from '@/shared/api/client'
import type { DataTarget, ImportKind, RunImportStepResult, StartImportResult } from '@/shared/api/types'
import { folderKeys } from '@/features/folders/api'

export interface ImportProgress {
  processed: number
  total: number
  done: boolean
}

/**
 * Drives the chunked import protocol: start, then step until done.
 *
 * The API is chunked because shared hosting has no background workers, so the
 * progress bar is genuine — each step is a request that really processed rows.
 */
export function useImportRunner(projectId: string) {
  const queryClient = useQueryClient()
  const [progress, setProgress] = useState<ImportProgress | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isRunning, setIsRunning] = useState(false)

  const reset = useCallback(() => {
    setProgress(null)
    setError(null)
    setIsRunning(false)
  }, [])

  const run = useCallback(
    async (input: { folderFileId: string; folderId: string; kind: ImportKind; target: DataTarget }) => {
      setIsRunning(true)
      setError(null)
      setProgress({ processed: 0, total: 0, done: false })

      try {
        const { data: started } = await api.post<StartImportResult>('/api/imports/start', {
          projectId,
          folderFileId: input.folderFileId,
          kind: input.kind,
          target: input.target,
        })

        // Bounded so a server that never reports done cannot spin forever.
        for (let step = 0; step < 200; step++) {
          const { data } = await api.post<RunImportStepResult>(
            `/api/imports/${started.importBatchId}/step`,
          )
          setProgress({ processed: data.processed, total: data.total, done: data.done })
          if (data.done) {
            void queryClient.invalidateQueries({ queryKey: folderKeys.tree(projectId) })
            void queryClient.invalidateQueries({ queryKey: folderKeys.detail(input.folderId) })
            void queryClient.invalidateQueries({ queryKey: ['imports', projectId] })
            return data
          }
        }

        throw new Error('The import did not finish after 200 steps')
      } catch (caught) {
        setError(apiErrorMessage(caught, 'The import failed'))
        return null
      } finally {
        setIsRunning(false)
      }
    },
    [projectId, queryClient],
  )

  return { run, reset, progress, error, isRunning }
}
