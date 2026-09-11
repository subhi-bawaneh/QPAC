import { Spinner } from '@/shared/ui/spinner'
import { Badge } from '@/shared/ui/badge'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import { formatDate } from '@/shared/lib/utils'
import { useAuditHistory } from './api'

// Every human change to one row, newest first. A Replaced or Deleted entry carries the
// whole outgoing row as JSON rather than a field diff, so it is shown as a disclosure
// rather than squeezed into the old-value column.
export function HistoryPanel({ entity, entityId }: { entity: string; entityId: string }) {
  const history = useAuditHistory(QPAC_PROJECT_ID, entity, entityId)

  if (history.isPending) return <Spinner />

  const items = history.data?.items ?? []
  if (items.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        Nobody has changed this row since it was imported.
      </p>
    )
  }

  return (
    <ol className="divide-y divide-border text-sm">
      {items.map((entry) => (
        <li key={entry.id} className="py-2">
          <div className="flex flex-wrap items-baseline gap-2">
            <Badge tone={entry.action === 'Deleted' || entry.action === 'Delete' ? 'danger' : 'neutral'}>
              {entry.action}
            </Badge>
            {entry.field ? <span className="font-medium">{entry.field}</span> : null}
            <span className="text-xs text-muted-foreground">
              {entry.userId} · {formatDate(entry.at)}
            </span>
          </div>

          {entry.field ? (
            <p className="mt-1 text-xs">
              <span className="line-through text-muted-foreground">{entry.oldValue || '—'}</span>
              {' → '}
              <span>{entry.newValue || '—'}</span>
            </p>
          ) : entry.oldValue && entry.oldValue.startsWith('{') ? (
            <details className="mt-1">
              <summary className="cursor-pointer text-xs text-muted-foreground">
                The row as it was
              </summary>
              <pre className="mt-1 max-h-48 overflow-auto rounded bg-muted p-2 text-[11px]">
                {entry.oldValue}
              </pre>
            </details>
          ) : (
            <p className="mt-1 text-xs text-muted-foreground">
              {entry.oldValue || entry.newValue || ''}
            </p>
          )}
        </li>
      ))}
    </ol>
  )
}
