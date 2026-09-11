import { X } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Badge } from '@/shared/ui/badge'
import { Spinner } from '@/shared/ui/spinner'
import { apiErrorMessage } from '@/shared/api/client'
import { formatDate } from '@/shared/lib/utils'
import { HistoryPanel } from '@/features/audit/HistoryPanel'
import { StatusBadge } from './StatusBadge'
import { useTrackerDocument } from './api'

/** Side panel: the document's computed row plus every Aconex revision behind it. */
export function DocumentDrawer({ documentId, onClose }: {
  documentId: string | null
  onClose: () => void
}) {
  const document = useTrackerDocument(documentId)

  if (!documentId) return null

  const row = document.data?.row

  return (
    <div className="fixed inset-0 z-40 flex justify-end">
      <div className="absolute inset-0 bg-black/40" role="presentation" onClick={onClose} aria-hidden />

      <aside
        role="dialog"
        aria-modal="true"
        aria-label="Document detail"
        className="relative z-10 flex h-full w-full max-w-2xl flex-col border-l border-border bg-card"
      >
        <div className="flex items-start justify-between border-b border-border px-5 py-4">
          <div className="min-w-0">
            <p className="truncate font-mono text-xs">{row?.documentNumber ?? '…'}</p>
            <h2 className="mt-1 truncate text-sm font-semibold">{row?.title ?? 'Loading'}</h2>
          </div>
          <Button variant="ghost" size="sm" onClick={onClose} aria-label="Close">
            <X className="h-4 w-4" aria-hidden />
          </Button>
        </div>

        <div className="flex-1 space-y-6 overflow-y-auto px-5 py-4">
          {document.isPending ? <Spinner /> : null}
          {document.isError ? (
            <p className="text-sm text-destructive">{apiErrorMessage(document.error)}</p>
          ) : null}

          {row ? (
            <>
              <section>
                <h3 className="mb-2 text-xs font-semibold uppercase text-muted-foreground">Current status</h3>
                <dl className="grid grid-cols-2 gap-3 text-sm">
                  <Field label="Status"><StatusBadge status={row.status} /></Field>
                  <Field label="Aconex status">{row.aconexStatus ?? '—'}</Field>
                  <Field label="Revision">{row.revision ?? '—'}</Field>
                  <Field label="Submissions">{row.submissionsCount ?? '—'}</Field>
                  <Field label="Submission date">{formatDate(row.submissionDate)}</Field>
                  <Field label="Date modified">{formatDate(row.dateModified)}</Field>
                  <Field label="Transmittal">{row.transmittal ?? '—'}</Field>
                  <Field label="Author">{row.author ?? '—'}</Field>
                </dl>
              </section>

              <section>
                <h3 className="mb-2 text-xs font-semibold uppercase text-muted-foreground">Schedule</h3>
                <dl className="grid grid-cols-2 gap-3 text-sm">
                  <Field label="Planned start">{formatDate(row.plannedStart)}</Field>
                  <Field label="Planned finish">{formatDate(row.plannedFinish)}</Field>
                  <Field label="Actual start">{formatDate(row.actualStart)}</Field>
                  <Field label="Actual finish">{formatDate(row.actualFinish)}</Field>
                  <Field label="Activity ID">{row.activityId ?? '—'}</Field>
                  <Field label="Delivery milestone">{formatDate(row.deliveryMilestone)}</Field>
                </dl>
              </section>

              <section>
                <h3 className="mb-2 text-xs font-semibold uppercase text-muted-foreground">
                  Revision history ({document.data?.revisions.length ?? 0})
                </h3>

                {document.data?.revisions.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    No Aconex history for this document number.
                  </p>
                ) : (
                  <ul className="divide-y divide-border text-sm">
                    {document.data?.revisions.map((revision) => (
                      <li key={revision.id} className="flex items-start justify-between gap-3 py-2">
                        <div className="min-w-0">
                          <p className="flex items-center gap-2">
                            <span className="font-medium">Rev {revision.revision}</span>
                            <StatusBadge status={revision.status} />
                            {revision.isLatest ? <Badge tone="info">Latest</Badge> : null}
                            {/* Terminated revisions are excluded from every computed column. */}
                            {revision.isTerminated ? <Badge tone="danger">Terminated</Badge> : null}
                          </p>
                          <p className="truncate text-xs text-muted-foreground">{revision.aconexStatus}</p>
                          {revision.transmittalIn ? (
                            <p className="text-xs text-muted-foreground">{revision.transmittalIn}</p>
                          ) : null}
                        </div>
                        <p className="shrink-0 text-xs text-muted-foreground">
                          {formatDate(revision.dateModified)}
                        </p>
                      </li>
                    ))}
                  </ul>
                )}
              </section>

              <section>
                <h3 className="mb-2 text-xs font-semibold uppercase text-muted-foreground">
                  Who changed this
                </h3>
                <HistoryPanel entity="Document" entityId={documentId} />
              </section>
            </>
          ) : null}
        </div>
      </aside>
    </div>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="mt-0.5">{children}</dd>
    </div>
  )
}
