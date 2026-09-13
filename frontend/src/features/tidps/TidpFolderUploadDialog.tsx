import { useState } from 'react'
import { AlertCircle, Loader2 } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Dialog } from '@/shared/ui/dialog'
import { buildManifestFromHandle } from './manifestBuilder'

const REQUIRED_FOLDER_NAME = '02.TIDPs'

interface TidpFolderUploadDialogProps {
  open: boolean
  onClose: () => void
  onFolderSelected: (folderName: string, files: FileList) => Promise<void>
  isLoading: boolean
}

export function TidpFolderUploadDialog({
  open,
  onClose,
  onFolderSelected,
  isLoading,
}: TidpFolderUploadDialogProps) {
  const [error, setError] = useState<string | null>(null)

  const handleShowDirectoryPicker = async () => {
    try {
      setError(null)

      // Try the modern File System Access API first
      if ('showDirectoryPicker' in window) {
        try {
          const dirHandle = await (window as any).showDirectoryPicker()
          const folderName = dirHandle.name

          if (folderName !== REQUIRED_FOLDER_NAME) {
            setError(`Please select the folder named ${REQUIRED_FOLDER_NAME}`)
            return
          }

          // Build manifest and collect files
          const { manifest, files } = await buildManifestFromHandle(dirHandle, folderName)

          if (manifest.files.length === 0) {
            setError('No .xlsx or .xlsm files found in the folder')
            return
          }

          // Convert files Map to FileList-like structure
          const filesList = Array.from(files.values())
          const dataTransfer = new DataTransfer()
          filesList.forEach((f) => {
            dataTransfer.items.add(f)
          })

          await onFolderSelected(folderName, dataTransfer.files)
          onClose()
        } catch (err: any) {
          if (err.name === 'AbortError') {
            // User cancelled the picker
            return
          }
          throw err
        }
      } else {
        // Fallback to webkitdirectory
        handleWebkitDirectoryPicker()
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to access directory')
    }
  }

  const handleWebkitDirectoryPicker = () => {
    const input = document.createElement('input')
    input.type = 'file'
    input.webkitdirectory = true
    input.multiple = true

    input.onchange = async () => {
      try {
        setError(null)

        if (!input.files || input.files.length === 0) {
          return
        }

        // Get the root folder name from the first file's webkit path
        const firstPath = (input.files[0] as any).webkitRelativePath || ''
        const folderName = firstPath.split('/')[0]

        if (folderName !== REQUIRED_FOLDER_NAME) {
          setError(`Please select the folder named ${REQUIRED_FOLDER_NAME}`)
          return
        }

        await onFolderSelected(folderName, input.files)
        onClose()
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to process folder')
      }
    }

    input.click()
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Upload TIDP Folder"
      description={
        <>
          Select the <code className="text-xs font-mono">{REQUIRED_FOLDER_NAME}</code> folder from your computer.
          Only .xlsx and .xlsm files will be uploaded.
        </>
      }
    >
      {error && (
        <div className="flex items-start gap-2 rounded-md border border-destructive bg-destructive/10 p-3 text-sm text-destructive">
          <AlertCircle className="h-5 w-5 flex-shrink-0 mt-0.5" />
          <span>{error}</span>
        </div>
      )}

      <div className="space-y-3">
        <Button
          onClick={handleShowDirectoryPicker}
          disabled={isLoading}
          className="w-full"
        >
          {isLoading && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
          {isLoading ? 'Uploading...' : 'Select Folder'}
        </Button>
        <p className="text-xs text-muted-foreground">
          {typeof window !== 'undefined' && 'showDirectoryPicker' in window
            ? 'Uses native folder picker (Chrome/Edge) or falls back to folder upload'
            : 'Uses folder upload (webkitdirectory)'}
        </p>
      </div>
    </Dialog>
  )
}
