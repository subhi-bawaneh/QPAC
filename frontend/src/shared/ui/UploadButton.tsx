import { useRef } from 'react'
import { Upload } from 'lucide-react'
import { Button } from '@/shared/ui/button'

// The toolbar half of an upload. InitialUploadState is the empty-page half; both end
// up calling the same mutation, so the file input lives in one place.
export function UploadButton({
  label,
  accept = '.xlsx',
  pending,
  onSelect,
}: {
  label: string
  accept?: string
  pending?: boolean
  onSelect: (file: File) => void
}) {
  const input = useRef<HTMLInputElement>(null)

  return (
    <>
      <input
        ref={input}
        type="file"
        accept={accept}
        className="hidden"
        onChange={(event) => {
          const file = event.target.files?.[0]
          if (file) onSelect(file)
          event.target.value = ''
        }}
      />
      <Button onClick={() => input.current?.click()} disabled={pending}>
        <Upload className="h-4 w-4" aria-hidden />
        {pending ? 'Uploading…' : label}
      </Button>
    </>
  )
}
