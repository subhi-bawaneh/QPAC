import { useEffect, useState } from 'react'
import { Dialog } from '@/shared/ui/dialog'
import { Button } from '@/shared/ui/button'
import { Select } from '@/shared/ui/select'
import { Progress } from '@/shared/ui/progress'
import type { DataTarget, FolderFileSummary, ImportKind } from '@/shared/api/types'
import { useImportRunner } from './api'
import { defaultKindFor, importKinds } from './kinds'

export function ImportDialog({ open, onClose, file, folderId, folderTarget, projectId }: {
  open: boolean
  onClose: () => void
  file: FolderFileSummary | null
  folderId: string
  folderTarget: DataTarget
  projectId: string
}) {
  const [kind, setKind] = useState<ImportKind>('Tidp')
  const { run, reset, progress, error, isRunning } = useImportRunner(projectId)

  useEffect(() => {
    if (file) setKind(defaultKindFor(file.kind))
  }, [file])

  useEffect(() => {
    if (open) reset()
  }, [open, reset])

  if (!file) return null

  const finished = progress?.done === true

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={`Import ${file.name}`}
      description={`Rows land in the ${folderTarget} layer, which this folder's target decides.`}
    >
      <div className="space-y-4">
        <div className="space-y-1">
          <label className="text-sm font-medium" htmlFor="import-kind">Workbook kind</label>
          <Select
            id="import-kind"
            value={kind}
            disabled={isRunning || finished}
            onChange={(event) => setKind(event.target.value as ImportKind)}
          >
            {importKinds.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </Select>
        </div>

        {progress ? (
          <Progress value={progress.processed} max={Math.max(progress.total, progress.processed)} label="Rows" />
        ) : null}

        {error ? (
          <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {error}
          </p>
        ) : null}

        {finished ? (
          <p className="rounded-md bg-emerald-100 px-3 py-2 text-sm text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200">
            Imported {progress?.processed.toLocaleString('en-GB')} rows.
          </p>
        ) : null}

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose} disabled={isRunning}>
            {finished ? 'Close' : 'Cancel'}
          </Button>
          {!finished ? (
            <Button
              disabled={isRunning}
              onClick={() => void run({ folderFileId: file.id, folderId, kind, target: folderTarget })}
            >
              {isRunning ? 'Importing…' : 'Start import'}
            </Button>
          ) : null}
        </div>
      </div>
    </Dialog>
  )
}
