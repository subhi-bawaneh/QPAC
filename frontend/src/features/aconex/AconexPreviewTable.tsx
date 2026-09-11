import { Badge } from '@/shared/ui/badge'
import { formatNumber } from '@/shared/lib/utils'
import type { AconexPreview } from '@/shared/api/types'

// The only preview left in the system. It exists because this is the only decision an
// operator cannot make without the numbers: they export from Aconex periodically and
// cannot remember what was already loaded.
export function AconexPreviewTable({ preview }: { preview: AconexPreview }) {
  return (
    <div className="space-y-3">
      <div className="overflow-x-auto rounded-md border border-border">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-border bg-muted/50 text-left text-xs uppercase tracking-wide text-muted-foreground">
              <th className="px-4 py-2">File</th>
              <th className="px-4 py-2 text-right">Lines read</th>
              <th className="px-4 py-2 text-right">New</th>
              <th className="px-4 py-2 text-right">Already held</th>
            </tr>
          </thead>
          <tbody>
            {preview.files.map((file) => (
              <tr key={file.fileName} className="border-b border-border last:border-0">
                <td className="px-4 py-2">
                  {file.fileName}
                  {file.error ? (
                    <p className="mt-1 text-xs text-destructive">{file.error}</p>
                  ) : null}
                </td>
                <td className="px-4 py-2 text-right tabular-nums">{formatNumber(file.rowsRead)}</td>
                <td className="px-4 py-2 text-right tabular-nums">
                  {file.rowsNew > 0 ? (
                    <Badge tone="success">{formatNumber(file.rowsNew)}</Badge>
                  ) : (
                    <span className="text-muted-foreground">0</span>
                  )}
                </td>
                <td className="px-4 py-2 text-right tabular-nums text-muted-foreground">
                  {formatNumber(file.rowsDuplicate)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <p className="text-sm">
        {preview.totalRowsNew === 0 ? (
          <span className="text-muted-foreground">
            Every line in {preview.files.length === 1 ? 'this file' : 'these files'} is already
            held. Uploading changes nothing.
          </span>
        ) : (
          <>
            <strong>{formatNumber(preview.totalRowsNew)}</strong> new{' '}
            {preview.totalRowsNew === 1 ? 'line' : 'lines'} out of{' '}
            {formatNumber(preview.totalRowsRead)}. Nothing already held is touched, and
            nothing is ever deleted.
          </>
        )}
      </p>
    </div>
  )
}
