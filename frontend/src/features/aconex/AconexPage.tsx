import { useRef, useState } from 'react'
import { Upload } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Badge } from '@/shared/ui/badge'
import { Spinner } from '@/shared/ui/spinner'
import { InitialUploadState } from '@/shared/ui/InitialUploadState'
import { useAuth } from '@/shared/auth/useAuth'
import { Permissions } from '@/shared/auth/permissions'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import { formatDate, formatNumber } from '@/shared/lib/utils'
import { apiErrorMessage } from '@/shared/api/client'
import { useAconexBatches, usePreviewAconex, useUploadAconex } from './api'
import { AconexPreviewTable } from './AconexPreviewTable'

// Aconex exports append. The operator exports periodically and cannot remember what was
// already loaded, so the page's job is to answer "what would this add" before it adds it.
export function AconexPage() {
  const { can } = useAuth()
  const canUpload = can(Permissions.filesManage)

  const batches = useAconexBatches(QPAC_PROJECT_ID)
  const preview = usePreviewAconex(QPAC_PROJECT_ID)
  const upload = useUploadAconex(QPAC_PROJECT_ID)

  const [chosen, setChosen] = useState<File[]>([])
  const input = useRef<HTMLInputElement>(null)

  function choose(files: FileList | null) {
    if (!files || files.length === 0) return
    const list = Array.from(files)
    setChosen(list)
    upload.reset()
    preview.mutate(list)
  }

  async function confirm() {
    await upload.mutateAsync(chosen)
    setChosen([])
    preview.reset()
  }

  const hasHistory = (batches.data?.length ?? 0) > 0

  if (batches.isPending) return <Spinner />

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold">Aconex history</h1>
          <p className="text-sm text-muted-foreground">
            Every export is appended. A line already held is dropped and counted, and
            nothing is ever deleted.
          </p>
        </div>

        {canUpload && hasHistory ? (
          <Button onClick={() => input.current?.click()} disabled={preview.isPending}>
            <Upload className="h-4 w-4" aria-hidden />
            Upload exports
          </Button>
        ) : null}
      </header>

      <input
        ref={input}
        type="file"
        accept=".xlsx"
        multiple
        className="hidden"
        onChange={(event) => {
          choose(event.target.files)
          event.target.value = ''
        }}
      />

      {!hasHistory && chosen.length === 0 ? (
        <InitialUploadState
          title="No Aconex history yet"
          description="Upload one or more Aconex exports. They are appended, so you can hand in the same file twice without doubling anything."
          canUpload={canUpload}
          pending={preview.isPending}
          onSelect={(file) => choose([file] as unknown as FileList)}
        />
      ) : null}

      {preview.isPending ? <Spinner /> : null}

      {preview.isError ? (
        <p className="text-sm text-destructive">{apiErrorMessage(preview.error)}</p>
      ) : null}

      {preview.data ? (
        <section className="space-y-3 rounded-md border border-border bg-card p-4">
          <h2 className="text-sm font-semibold">Before you upload</h2>
          <AconexPreviewTable preview={preview.data} />

          <div className="flex gap-2">
            <Button onClick={confirm} disabled={upload.isPending || preview.data.totalRowsNew === 0}>
              {upload.isPending ? 'Uploading…' : 'Upload'}
            </Button>
            <Button
              variant="outline"
              onClick={() => {
                setChosen([])
                preview.reset()
              }}
              disabled={upload.isPending}
            >
              Cancel
            </Button>
          </div>

          {upload.isError ? (
            <p className="text-sm text-destructive">{apiErrorMessage(upload.error)}</p>
          ) : null}
        </section>
      ) : null}

      {hasHistory ? (
        <section className="space-y-2">
          <h2 className="text-sm font-semibold">Recent uploads</h2>
          <div className="overflow-x-auto rounded-md border border-border bg-card">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border text-left text-xs uppercase tracking-wide text-muted-foreground">
                  <th className="px-4 py-2">File</th>
                  <th className="px-4 py-2">Uploaded</th>
                  <th className="px-4 py-2 text-right">Read</th>
                  <th className="px-4 py-2 text-right">New</th>
                  <th className="px-4 py-2 text-right">Already held</th>
                  <th className="px-4 py-2">Status</th>
                </tr>
              </thead>
              <tbody>
                {batches.data?.map((batch) => (
                  <tr key={batch.id} className="border-b border-border last:border-0">
                    <td className="px-4 py-2">{batch.fileName}</td>
                    <td className="px-4 py-2">{formatDate(batch.importedAt)}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{formatNumber(batch.rowsRead)}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{formatNumber(batch.rowsInserted)}</td>
                    <td className="px-4 py-2 text-right tabular-nums text-muted-foreground">
                      {formatNumber(batch.rowsDuplicate)}
                    </td>
                    <td className="px-4 py-2">
                      <Badge
                        tone={
                          batch.status === 'Completed'
                            ? 'success'
                            : batch.status === 'Failed'
                              ? 'danger'
                              : 'info'
                        }
                      >
                        {batch.status}
                      </Badge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ) : null}
    </div>
  )
}
