import { Cloud, FileSpreadsheet, Loader2, Upload } from 'lucide-react'
import { cn, formatDate } from '@/shared/lib/utils'
import type { FolderFileSummary } from '@/shared/api/types'
import { ImportStateBadge } from './FolderBadges'

export function FileTile({ file, selected, importing, onSelect, onOpen, onMenu }: {
  file: FolderFileSummary
  selected: boolean
  importing: boolean
  onSelect: () => void
  onOpen: () => void
  onMenu: (event: React.MouseEvent) => void
}) {
  const SourceIcon = file.contentSource === 'Drive' ? Cloud : Upload

  return (
    <button
      type="button"
      role="gridcell"
      aria-label={`File ${file.name}`}
      aria-selected={selected}
      data-testid={`file-tile-${file.id}`}
      onClick={onSelect}
      onDoubleClick={onOpen}
      onContextMenu={onMenu}
      className={cn(
        'group flex h-[132px] w-full flex-col items-start gap-1 rounded-lg border border-border bg-card p-3 text-left',
        'transition-all hover:-translate-y-px hover:shadow-md',
        selected && 'ring-2 ring-primary',
      )}
    >
      <div className="flex w-full items-start justify-between">
        {importing ? (
          <Loader2
            role="status"
            aria-label={`Importing ${file.name}`}
            className="h-8 w-8 animate-spin text-primary"
          />
        ) : (
          <FileSpreadsheet className="h-8 w-8 text-emerald-600 dark:text-emerald-400" aria-hidden />
        )}

        <SourceIcon
          className="mt-1 h-3.5 w-3.5 text-muted-foreground"
          aria-label={file.contentSource === 'Drive' ? 'From Google Drive' : 'Uploaded'}
        />
      </div>

      <span className="line-clamp-2 break-all text-sm font-medium leading-snug">{file.name}</span>

      <span className="text-xs text-muted-foreground">{formatDate(file.contentModifiedAt)}</span>

      <div className="mt-auto flex w-full flex-wrap items-center gap-1">
        <ImportStateBadge state={importing ? 'NotImported' : file.state} />
        {file.hasNewerDraft ? (
          <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[11px] text-amber-900 dark:bg-amber-900/40 dark:text-amber-100">
            Newer draft
          </span>
        ) : null}
        {file.rowCount !== null ? (
          <span className="ml-auto text-xs tabular-nums text-muted-foreground">{file.rowCount}</span>
        ) : null}
      </div>
    </button>
  )
}
