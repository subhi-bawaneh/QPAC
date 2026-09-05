import { ClipboardList, FileSpreadsheet, Play, Trash2 } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Button } from '@/shared/ui/button'
import { formatDate, formatSize } from '@/shared/lib/utils'
import type { FolderFileSummary } from '@/shared/api/types'
import { FileKindBadge, ImportStateBadge } from './FolderBadges'

export function FileList({ files, canImport, canManage, isDraftFolder, onImport, onDelete }: {
  files: FolderFileSummary[]
  canImport: boolean
  canManage: boolean
  /** A Draft folder's imports land in a draft, so its files get a Review action. */
  isDraftFolder: boolean
  onImport: (file: FolderFileSummary) => void
  onDelete: (file: FolderFileSummary) => void
}) {
  if (files.length === 0) {
    return (
      <p className="px-5 py-6 text-sm text-muted-foreground">
        No files in this folder. Upload a workbook, or sync the folder from Drive.
      </p>
    )
  }

  return (
    <table className="w-full text-sm">
      <thead className="border-b border-border text-left text-xs uppercase text-muted-foreground">
        <tr>
          <th className="px-5 py-2 font-medium">File</th>
          <th className="px-5 py-2 font-medium">Kind</th>
          <th className="px-5 py-2 font-medium">State</th>
          <th className="px-5 py-2 font-medium">Source</th>
          <th className="px-5 py-2 text-right font-medium">Size</th>
          <th className="px-5 py-2 font-medium">Modified</th>
          <th className="px-5 py-2 text-right font-medium">Actions</th>
        </tr>
      </thead>
      <tbody>
        {files.map((file) => (
          <tr key={file.id} className="border-b border-border last:border-0">
            <td className="px-5 py-2">
              <span className="flex items-center gap-2">
                <FileSpreadsheet className="h-4 w-4 shrink-0 text-muted-foreground" aria-hidden />
                <span className="truncate">{file.name}</span>
              </span>
            </td>
            <td className="px-5 py-2"><FileKindBadge kind={file.kind} /></td>
            <td className="px-5 py-2"><ImportStateBadge state={file.state} /></td>
            <td className="px-5 py-2 text-muted-foreground">{file.source}</td>
            <td className="px-5 py-2 text-right tabular-nums text-muted-foreground">{formatSize(file.sizeBytes)}</td>
            <td className="px-5 py-2 text-muted-foreground">{formatDate(file.driveModifiedAt)}</td>
            <td className="px-5 py-2">
              <div className="flex justify-end gap-2">
                {isDraftFolder && file.state === 'Imported' ? (
                  <Button variant="outline" size="sm" asChild>
                    <Link to={`/drafts/${file.id}`}>
                      <ClipboardList aria-hidden />
                      Review draft
                    </Link>
                  </Button>
                ) : null}
                {canImport ? (
                  <Button size="sm" variant="outline" onClick={() => onImport(file)}>
                    <Play aria-hidden />
                    Import
                  </Button>
                ) : null}
                {canManage ? (
                  <Button
                    size="icon-sm"
                    variant="ghost"
                    aria-label={`Delete ${file.name}`}
                    onClick={() => onDelete(file)}
                  >
                    <Trash2 aria-hidden />
                  </Button>
                ) : null}
              </div>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
