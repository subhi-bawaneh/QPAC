import { useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Upload } from 'lucide-react'
import { Spinner } from '@/shared/ui/spinner'
import { Badge } from '@/shared/ui/badge'
import { Button } from '@/shared/ui/button'
import { InitialUploadState } from '@/shared/ui/InitialUploadState'
import { ReplaceDialog } from '@/shared/ui/ReplaceDialog'
import { useAuth } from '@/shared/auth/useAuth'
import { Permissions } from '@/shared/auth/permissions'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import { formatDate, formatNumber } from '@/shared/lib/utils'
import {
  useDeleteTidp, useReplacePreview, useReplaceTidp, useTidpFiles, useUploadTidp,
} from './api'

// A plain list of the uploaded TIDP workbooks. Stage 5 replaces this with the
// two-level explorer; until the upload route exists there is nothing to add here.
export function TidpFilesPage() {
  const { can } = useAuth()
  const canUpload = can(Permissions.filesManage)

  const files = useTidpFiles(QPAC_PROJECT_ID)
  const upload = useUploadTidp(QPAC_PROJECT_ID)
  const replace = useReplaceTidp()
  const remove = useDeleteTidp()

  // Which file a confirmation is about, and what it will do to it.
  const [pending, setPending] = useState<{ id: string; action: 'replace' | 'delete' } | null>(null)
  const preview = useReplacePreview(pending?.id ?? null)
  const replacement = useRef<File | null>(null)
  const uploadInput = useRef<HTMLInputElement>(null)
  const replaceInput = useRef<HTMLInputElement>(null)

  async function confirm() {
    if (!pending) return
    if (pending.action === 'delete') {
      await remove.mutateAsync(pending.id)
    } else if (replacement.current) {
      await replace.mutateAsync({ tidpFileId: pending.id, file: replacement.current })
    }
    replacement.current = null
    setPending(null)
  }

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold">TIDPs</h1>
          <p className="text-sm text-muted-foreground">
            Every uploaded TIDP workbook, grouped by discipline.
          </p>
        </div>
        {canUpload && files.data && files.data.length > 0 ? (
          <Button onClick={() => uploadInput.current?.click()} disabled={upload.isPending}>
            <Upload className="h-4 w-4" aria-hidden />
            {upload.isPending ? 'Uploading…' : 'Upload TIDP'}
          </Button>
        ) : null}
      </header>

      <input
        ref={uploadInput}
        type="file"
        accept=".xlsx"
        className="hidden"
        onChange={(event) => {
          const file = event.target.files?.[0]
          if (file) void upload.mutateAsync(file)
          event.target.value = ''
        }}
      />

      <input
        ref={replaceInput}
        type="file"
        accept=".xlsx"
        className="hidden"
        onChange={(event) => {
          const file = event.target.files?.[0]
          if (file && pending) replacement.current = file
          event.target.value = ''
        }}
      />

      {files.isPending ? (
        <Spinner />
      ) : files.data && files.data.length > 0 ? (
        <div className="overflow-x-auto rounded-md border border-border bg-card">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-border text-left text-xs uppercase tracking-wide text-muted-foreground">
                <th className="px-4 py-2">Discipline</th>
                <th className="px-4 py-2">File</th>
                <th className="px-4 py-2 text-right">Documents</th>
                <th className="px-4 py-2 text-right">Edited</th>
                <th className="px-4 py-2">Uploaded</th>
                <th className="px-4 py-2">Status</th>
                {canUpload ? <th className="px-4 py-2" /> : null}
              </tr>
            </thead>
            <tbody>
              {files.data.map((file) => (
                <tr key={file.id} className="border-b border-border last:border-0">
                  <td className="px-4 py-2">{file.disciplineName}</td>
                  <td className="px-4 py-2">
                    <Link className="text-primary hover:underline" to={`/tidps/${file.id}`}>
                      {file.fileName}
                    </Link>
                  </td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(file.documentCount)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(file.editedCount)}</td>
                  <td className="px-4 py-2">{formatDate(file.uploadedAt)}</td>
                  <td className="px-4 py-2">
                    {file.status === 'Failed' ? (
                      <Badge tone="danger">Failed</Badge>
                    ) : file.status === 'Importing' ? (
                      <Badge tone="warning">Importing</Badge>
                    ) : (
                      <Badge tone="success">Imported</Badge>
                    )}
                  </td>
                  {canUpload ? (
                    <td className="px-4 py-2 text-right">
                      <div className="flex justify-end gap-2">
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => {
                            setPending({ id: file.id, action: 'replace' })
                            replaceInput.current?.click()
                          }}
                        >
                          Replace
                        </Button>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setPending({ id: file.id, action: 'delete' })}
                        >
                          Delete
                        </Button>
                      </div>
                    </td>
                  ) : null}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <InitialUploadState
          title="No TIDP workbook yet"
          description="Upload one TIDP workbook per discipline. Its rows become the register, and engineers edit them here from then on."
          canUpload={canUpload}
          pending={upload.isPending}
          onSelect={(file) => void upload.mutateAsync(file)}
        />
      )}

      <ReplaceDialog
        open={pending !== null}
        action={pending?.action ?? 'replace'}
        preview={preview.data ?? null}
        pending={replace.isPending || remove.isPending}
        onCancel={() => {
          replacement.current = null
          setPending(null)
        }}
        onConfirm={confirm}
      />
    </div>
  )
}
