import { useRef, useState } from 'react'
import { Dialog } from '@/shared/ui/dialog'
import { Button } from '@/shared/ui/button'
import { apiErrorMessage } from '@/shared/api/client'
import { useUploadFile } from './api'

interface Progress {
  name: string
  state: 'pending' | 'done' | 'failed'
  detail?: string
}

// Multi-file: the API answers 202 per file and the worker imports them, so the
// dialog reports what was accepted rather than what was imported.
export function UploadDialog({ open, onClose, folderId, folderName, projectId, initialFiles }: {
  open: boolean
  onClose: () => void
  folderId: string
  folderName: string
  projectId: string
  initialFiles?: File[]
}) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [error, setError] = useState<string | null>(null)
  const [progress, setProgress] = useState<Progress[]>([])
  const upload = useUploadFile(projectId)

  const send = async (files: File[]) => {
    if (files.length === 0) {
      setError('Choose a workbook first')
      return
    }

    setError(null)
    setProgress(files.map((file) => ({ name: file.name, state: 'pending' })))

    let failures = 0
    for (const file of files) {
      try {
        const result = await upload.mutateAsync({ folderId, file })
        setProgress((current) => current.map((row) => row.name === file.name
          ? { ...row, state: 'done', detail: result.replaced ? 'replaced' : 'added' }
          : row))
      } catch (caught) {
        failures += 1
        const message = apiErrorMessage(caught, 'Upload failed')
        setProgress((current) => current.map((row) => row.name === file.name
          ? { ...row, state: 'failed', detail: message }
          : row))
      }
    }

    if (failures === 0) onClose()
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={`Upload to ${folderName}`}
      description="The workbook kind is detected from its name, and the import starts on its own."
    >
      <div className="space-y-4">
        <input
          ref={inputRef}
          type="file"
          multiple
          accept=".xlsx,.xlsm,.xls"
          aria-label="Workbooks"
          className="block w-full text-sm file:mr-3 file:rounded-md file:border-0 file:bg-primary
            file:px-3 file:py-2 file:text-sm file:text-primary-foreground"
        />

        {progress.length > 0 ? (
          <ul className="space-y-1 text-sm">
            {progress.map((row) => (
              <li key={row.name} className="flex items-center justify-between gap-2">
                <span className="truncate">{row.name}</span>
                <span className={row.state === 'failed' ? 'text-destructive' : 'text-muted-foreground'}>
                  {row.state === 'pending' ? 'Uploading…' : row.detail}
                </span>
              </li>
            ))}
          </ul>
        ) : null}

        {error ? (
          <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {error}
          </p>
        ) : null}

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose} disabled={upload.isPending}>Cancel</Button>
          <Button
            onClick={() => void send(initialFiles ?? Array.from(inputRef.current?.files ?? []))}
            disabled={upload.isPending}
          >
            {upload.isPending ? 'Uploading…' : 'Upload'}
          </Button>
        </div>
      </div>
    </Dialog>
  )
}
