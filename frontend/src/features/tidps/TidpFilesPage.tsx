import { Link } from 'react-router-dom'
import { Spinner } from '@/shared/ui/spinner'
import { Badge } from '@/shared/ui/badge'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import { formatDate, formatNumber } from '@/shared/lib/utils'
import { useTidpFiles } from './api'

// A plain list of the uploaded TIDP workbooks. Stage 5 replaces this with the
// two-level explorer; until the upload route exists there is nothing to add here.
export function TidpFilesPage() {
  const files = useTidpFiles(QPAC_PROJECT_ID)

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-xl font-semibold">TIDPs</h1>
        <p className="text-sm text-muted-foreground">
          Every uploaded TIDP workbook, grouped by discipline.
        </p>
      </header>

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
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="rounded-md border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
          No TIDP workbook has been uploaded yet.
        </p>
      )}
    </div>
  )
}
