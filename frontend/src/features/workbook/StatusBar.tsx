import { formatDate, formatNumber } from '@/shared/lib/utils'
import type { DataTarget } from '@/shared/api/types'

export function StatusBar({ totalRows, loadedRows, layer, lastImportedAt, loadingMore }: {
  totalRows: number
  loadedRows: number
  layer: DataTarget
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
      <span>Layer: {layer === 'Draft' ? 'DB1 Draft' : 'DB2 Live'}</span>
      {lastImportedAt ? <span>Imported {formatDate(lastImportedAt)}</span> : null}
    </div>
  )
}
