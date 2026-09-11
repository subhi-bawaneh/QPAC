import { useRef } from 'react'
import { Upload } from 'lucide-react'
import { Button } from '@/shared/ui/button'

// What a page shows before its file has ever been uploaded: one button, nothing else.
// A table with no rows and a full toolbar invites an operator to look for the data;
// this says plainly that the data has not arrived yet and who can bring it.
export function InitialUploadState({
  title,
  description,
  accept = '.xlsx',
  canUpload,
  pending,
  onSelect,
}: {
  title: string
  description: string
  accept?: string
  canUpload: boolean
  pending?: boolean
  onSelect: (file: File) => void
}) {
  const input = useRef<HTMLInputElement>(null)

  return (
    <div className="flex flex-col items-center justify-center gap-3 rounded-md border border-dashed border-border px-8 py-16 text-center">
      <h2 className="text-base font-semibold">{title}</h2>
      <p className="max-w-prose text-sm text-muted-foreground">{description}</p>

      {canUpload ? (
        <>
          <input
            ref={input}
            type="file"
            accept={accept}
            className="hidden"
            onChange={(event) => {
              const file = event.target.files?.[0]
              if (file) onSelect(file)
              // Clearing the value lets the same file be chosen twice in a row.
              event.target.value = ''
            }}
          />
          <Button className="mt-2" disabled={pending} onClick={() => input.current?.click()}>
            <Upload className="h-4 w-4" aria-hidden />
            {pending ? 'Uploading…' : 'Initial upload'}
          </Button>
        </>
      ) : (
        <p className="mt-2 text-xs text-muted-foreground">
          Only the super admin can upload the first file.
        </p>
      )}
    </div>
  )
}
