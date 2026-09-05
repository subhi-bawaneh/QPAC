import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Dialog, DialogFooter } from '@/shared/ui/dialog'
import { Button } from '@/shared/ui/button'
import { Checkbox } from '@/shared/ui/checkbox'
import { Progress } from '@/shared/ui/progress'
import { api, apiErrorMessage } from '@/shared/api/client'
import { folderKeys } from './api'

interface SyncStep {
  foldersUpserted: number
  filesUpserted: number
  filesDownloaded: number
  nextFolderDriveId: string | null
  done: boolean
}

/**
 * Walks the Drive tree one folder per request.
 *
 * Sync is chunked for the same reason imports are — shared hosting has no
 * background workers — so the browser drives the loop and each step reports what it
 * found. Closing this dialog stops it; re-running continues from the top and
 * upserts, so nothing is duplicated.
 */
export function SyncDriveDialog({ open, onClose, projectId }: {
  open: boolean
  onClose: () => void
  projectId: string
}) {
  const queryClient = useQueryClient()
  const [downloadFiles, setDownloadFiles] = useState(false)
  const [running, setRunning] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [totals, setTotals] = useState({ steps: 0, folders: 0, files: 0, downloaded: 0, done: false })

  const run = async () => {
    setRunning(true)
    setError(null)
    setTotals({ steps: 0, folders: 0, files: 0, downloaded: 0, done: false })

    let next: string | null = null
    try {
      // Bounded so a server that never reports done cannot spin forever.
      for (let step = 0; step < 500; step++) {
        const { data }: { data: SyncStep } = await api.post(
          `/api/projects/${projectId}/drive/sync`,
          { startFolderDriveId: next, downloadFiles },
        )

        next = data.nextFolderDriveId
        setTotals((current) => ({
          steps: current.steps + 1,
          folders: current.folders + data.foldersUpserted,
          files: current.files + data.filesUpserted,
          downloaded: current.downloaded + data.filesDownloaded,
          done: data.done,
        }))

        if (data.done) {
          void queryClient.invalidateQueries({ queryKey: folderKeys.tree(projectId) })
          void queryClient.invalidateQueries({ queryKey: ['folders', 'detail'] })
          return
        }
      }
      throw new Error('The sync did not finish after 500 folders')
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The sync failed'))
    } finally {
      setRunning(false)
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Sync from Google Drive"
      description="Reads the configured Drive folder and mirrors its structure here."
    >
      <div className="space-y-4">
        <label className="flex items-start gap-3 rounded-md border p-3 text-sm">
          <Checkbox
            checked={downloadFiles}
            disabled={running}
            onCheckedChange={(checked) => setDownloadFiles(checked === true)}
            aria-label="Download file contents"
          />
          <span>
            <span className="font-medium">Download the workbooks too</span>
            <span className="mt-1 block text-xs text-muted-foreground">
              Without this, files are listed but their contents are fetched later, when
              you import one. Downloading everything up front is slower but makes imports
              instant.
            </span>
          </span>
        </label>

        {totals.steps > 0 ? (
          <div className="space-y-2">
            <Progress
              value={totals.steps}
              max={totals.done ? totals.steps : totals.steps + 1}
              label="Folders visited"
            />
            <p className="text-xs text-muted-foreground">
              {totals.folders.toLocaleString('en-GB')} folder(s),{' '}
              {totals.files.toLocaleString('en-GB')} file(s)
              {downloadFiles ? `, ${totals.downloaded.toLocaleString('en-GB')} downloaded` : ''}
            </p>
          </div>
        ) : null}

        {totals.done ? (
          <p className="rounded-md bg-emerald-100 px-3 py-2 text-sm text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200">
            Sync complete.
          </p>
        ) : null}

        {error ? (
          <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {error}
          </p>
        ) : null}

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={running}>
            {totals.done ? 'Close' : 'Cancel'}
          </Button>
          {!totals.done ? (
            <Button onClick={() => void run()} disabled={running}>
              {running ? 'Syncing…' : 'Start sync'}
            </Button>
          ) : null}
        </DialogFooter>
      </div>
    </Dialog>
  )
}
