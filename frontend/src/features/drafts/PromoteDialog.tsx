import { useMemo, useState } from 'react'
import { Dialog, DialogFooter } from '@/shared/ui/dialog'
import { Checkbox } from '@/shared/ui/checkbox'
import { Button } from '@/shared/ui/button'
import { Spinner } from '@/shared/ui/spinner'
import { Tabs, TabPanel, type TabItem } from '@/shared/ui/tabs'
import { apiErrorMessage } from '@/shared/api/client'
import { formatDate } from '@/shared/lib/utils'
import type { PromoteResult, PromoteRow } from '@/shared/api/types'
import { usePromote, usePromoteDiff } from './api'

export function PromoteDialog({ open, onClose, folderFileId }: {
  open: boolean
  onClose: () => void
  folderFileId: string
}) {
  const [tab, setTab] = useState('added')
  const [deleteMissing, setDeleteMissing] = useState(false)
  const [result, setResult] = useState<PromoteResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  const diff = usePromoteDiff(folderFileId, open)
  const promote = usePromote(folderFileId)

  const tabs = useMemo<TabItem[]>(() => [
    { id: 'added', label: 'Added', count: diff.data?.added },
    { id: 'modified', label: 'Modified', count: diff.data?.modified },
    { id: 'deleted', label: 'Deleted', count: diff.data?.deleted },
    { id: 'conflicts', label: 'Conflicts', count: diff.data?.conflicts, tone: 'danger' },
  ], [diff.data])

  const onPromote = async () => {
    setError(null)
    try {
      setResult(await promote.mutateAsync({ deleteMissing }))
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The promote failed'))
    }
  }



  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Promote to Live"
      description={
        diff.data?.importedAt
          ? `Draft imported ${formatDate(diff.data.importedAt)}. Conflicts are never written.`
          : 'Conflicts are never written; the rest of the file still promotes.'
      }
    >
      <div className="space-y-4">
        {diff.isPending ? <Spinner label="Working out what would change…" /> : null}
        {diff.isError ? (
          <p className="text-sm text-destructive">{apiErrorMessage(diff.error)}</p>
        ) : null}

        {diff.data && !result ? (
          <>
            <Tabs items={tabs} active={tab} onChange={setTab} />

            <div className="max-h-64 overflow-y-auto">
              <TabPanel id="added" active={tab}><RowList rows={diff.data.addedRows} empty="Nothing new." /></TabPanel>
              <TabPanel id="modified" active={tab}><RowList rows={diff.data.modifiedRows} empty="No changes." showChanges /></TabPanel>
              <TabPanel id="deleted" active={tab}>
                <RowList rows={diff.data.deletedRows} empty="Nothing would be removed." />
              </TabPanel>
              <TabPanel id="conflicts" active={tab}>
                <RowList rows={diff.data.conflictRows} empty="No conflicts." showReason />
              </TabPanel>
            </div>

            {diff.data.deleted > 0 ? (
              <label className="flex items-start gap-3 rounded-md border bg-muted/50 px-3 py-2.5 text-sm">
                <Checkbox
                  className="mt-0.5"
                  checked={deleteMissing}
                  aria-label="Delete missing Live rows"
                  onCheckedChange={(checked) => setDeleteMissing(checked === true)}
                />
                <span>
                  Also delete the {diff.data.deleted.toLocaleString('en-GB')} Live row(s) this file
                  owns that the draft no longer contains.
                </span>
              </label>
            ) : null}

            {diff.data.conflicts > 0 ? (
              <p className="rounded-md bg-amber-100 px-3 py-2 text-sm text-amber-900 dark:bg-amber-900/40 dark:text-amber-100">
                {diff.data.conflicts.toLocaleString('en-GB')} row(s) will be skipped and left marked
                Conflict in the draft.
              </p>
            ) : null}
          </>
        ) : null}

        {result ? (
          <div className="space-y-2 rounded-md bg-muted px-3 py-3 text-sm">
            <p className="font-medium">Promoted</p>
            <ul className="text-muted-foreground">
              <li>Added: {result.added.toLocaleString('en-GB')}</li>
              <li>Updated: {result.updated.toLocaleString('en-GB')}</li>
              <li>Deleted: {result.deleted.toLocaleString('en-GB')}</li>
              <li>Skipped: {result.skipped.toLocaleString('en-GB')}</li>
              <li>Conflicts: {result.conflicts.toLocaleString('en-GB')}</li>
            </ul>
            {result.recalculationRequired ? (
              <p className="text-xs">
                The reports rebuild on their own; the worker has been asked to recalculate.
              </p>
            ) : null}
          </div>
        ) : null}

        {error ? (
          <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {error}
          </p>
        ) : null}

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={promote.isPending}>
            {result ? 'Close' : 'Cancel'}
          </Button>

          {!result ? (
            <Button
              onClick={() => void onPromote()}
              disabled={promote.isPending || diff.isPending || !diff.data}
            >
              {promote.isPending ? 'Promoting…' : 'Promote'}
            </Button>
          ) : null}
        </DialogFooter>
      </div>
    </Dialog>
  )
}

function RowList({ rows, empty, showChanges, showReason }: {
  rows: PromoteRow[]
  empty: string
  showChanges?: boolean
  showReason?: boolean
}) {
  if (rows.length === 0) {
    return <p className="px-1 py-4 text-sm text-muted-foreground">{empty}</p>
  }

  return (
    <ul className="divide-y divide-border text-sm">
      {rows.map((row) => (
        <li key={`${row.documentNumber}-${row.draftId ?? row.liveId}`} className="py-2">
          <p className="font-mono text-xs">{row.documentNumber}</p>
          {row.title ? <p className="text-muted-foreground">{row.title}</p> : null}

          {showReason && row.reason ? (
            <p className="mt-1 text-xs text-destructive">{row.reason}</p>
          ) : null}

          {showChanges && row.changes.length ? (
            <ul className="mt-1 space-y-0.5 text-xs text-muted-foreground">
              {row.changes.map((change) => (
                <li key={change.field}>
                  <span className="font-medium text-foreground">{change.field}</span>{' '}
                  <span className="line-through">{change.oldValue || '—'}</span>{' → '}
                  <span>{change.newValue || '—'}</span>
                </li>
              ))}
            </ul>
          ) : null}
        </li>
      ))}
    </ul>
  )
}
