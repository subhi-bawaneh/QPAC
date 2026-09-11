import { formatDate, formatNumber } from '@/shared/lib/utils'

export function StatusBar({ totalRows, loadedRows, discipline, lastImportedAt, loadingMore }: {
  totalRows: number
  loadedRows: number
  discipline: string
  lastImportedAt: string | null
  loadingMore: boolean
}) {
  return (
    <div className="flex flex-wrap items-center gap-4 border-t border-border bg-muted px-3 py-1.5 text-xs text-muted-foreground">
      <span>Rows: {formatNumber(totalRows)}</span>
      <span>
        Loaded: {formatNumber(loadedRows)}
        {loadingMore ? ' — loading…' : ''}
      </span>
      <span>Discipline: {discipline || '—'}</span>
      {lastImportedAt ? <span>Imported {formatDate(lastImportedAt)}</span> : null}
    </div>
  )
}
