import { useRef, useState } from 'react'
import { Dialog } from '@/shared/ui/dialog'
import { Button } from '@/shared/ui/button'
import { apiErrorMessage } from '@/shared/api/client'
import { useUploadFile } from './api'

export function UploadDialog({ open, onClose, folderId, folderName, projectId }: {
  open: boolean
  onClose: () => void
  folderId: string
  folderName: string
  projectId: string
}) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [error, setError] = useState<string | null>(null)
  const upload = useUploadFile(projectId)

  const onSubmit = async () => {
    const file = inputRef.current?.files?.[0]
    if (!file) {
      setError('Choose a workbook first')
      return
    }

    setError(null)
    try {
      await upload.mutateAsync({ folderId, file })
      onClose()
    } catch (caught) {
      setError(apiErrorMessage(caught, 'Upload failed'))
    }
  }

  return (
    <Dialog open={open} onClose={onClose} title={`Upload to ${folderName}`}
      description="The workbook kind is detected from its contents on upload.">
      <div className="space-y-4">
        <input
          ref={inputRef}
          type="file"
          accept=".xlsx,.xlsm"
          aria-label="Workbook"
          className="block w-full text-sm file:mr-3 file:rounded-md file:border-0 file:bg-primary
            file:px-3 file:py-2 file:text-sm file:text-primary-foreground"
        />

        {error ? (
          <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {error}
          </p>
        ) : null}

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose} disabled={upload.isPending}>Cancel</Button>
          <Button onClick={() => void onSubmit()} disabled={upload.isPending}>
            {upload.isPending ? 'Uploading…' : 'Upload'}
          </Button>
        </div>
      </div>
    </Dialog>
  )
}
