import { useState } from 'react'
import { Check, Pencil, Plus, RotateCcw, Trash2, X } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Input } from '@/shared/ui/input'
import { Badge } from '@/shared/ui/badge'
import { Card, CardBody } from '@/shared/ui/card'
import { Checkbox } from '@/shared/ui/checkbox'
import { SelectItem, SimpleSelect } from '@/shared/ui/select'
import { ConfirmDialog } from '@/shared/ui/prompt-dialog'
import { apiErrorMessage } from '@/shared/api/client'
import { StatusBadge } from '@/features/tracker/StatusBadge'
import type { StatusMappingRow, UnifiedStatus } from '@/shared/api/types'
import {
  useCreateStatusMapping, useDeleteStatusMapping, useRestoreStatusMapping, useUpdateStatusMapping,
} from './api'

const unifiedStatuses: UnifiedStatus[] = ['Approved', 'Rejected', 'UnderReview', 'Withdrawn']

export function StatusMappingTable({ rows, canManage }: {
  rows: StatusMappingRow[]
  canManage: boolean
}) {
  const [newStatus, setNewStatus] = useState('')
  const [newUnified, setNewUnified] = useState<UnifiedStatus>('UnderReview')
  const [newLegacy, setNewLegacy] = useState(false)
  const [editing, setEditing] = useState<StatusMappingRow | null>(null)
  const [pendingDelete, setPendingDelete] = useState<StatusMappingRow | null>(null)
  const [error, setError] = useState<string | null>(null)

  const create = useCreateStatusMapping()
  const update = useUpdateStatusMapping()
  const remove = useDeleteStatusMapping()
  const restore = useRestoreStatusMapping()

  const run = async (action: () => Promise<unknown>, fallback: string) => {
    setError(null)
    try {
      await action()
      return true
    } catch (caught) {
      setError(apiErrorMessage(caught, fallback))
      return false
    }
  }

  return (
    <Card>
      <CardBody className="p-0">
        {error ? (
          <p role="alert" className="border-b border-border bg-destructive/10 px-4 py-2 text-sm text-destructive">
            {error}
          </p>
        ) : null}

        <table className="w-full text-sm">
          <caption className="sr-only">Status mapping</caption>
          <thead className="border-b border-border text-left text-xs uppercase text-muted-foreground">
            <tr>
              <th className="px-4 py-2 font-medium">Aconex status</th>
              <th className="px-4 py-2 font-medium">Unified status</th>
              <th className="px-4 py-2 font-medium">Legacy spelling</th>
              <th className="w-32 px-4 py-2 text-right font-medium">
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>

          <tbody>
            {canManage ? (
              <tr className="border-b border-border bg-muted/40">
                <td className="px-4 py-2">
                  <Input
                    aria-label="New Aconex status"
                    value={newStatus}
                    onChange={(event) => setNewStatus(event.target.value)}
                    className="h-8"
                  />
                </td>
                <td className="px-4 py-2">
                  <SimpleSelect
                    className="h-8"
                    label="New unified status"
                    value={newUnified}
                    onValueChange={(value) => setNewUnified(value as UnifiedStatus)}
                  >
                    {unifiedStatuses.map((status) => (
                      <SelectItem key={status} value={status}>{status}</SelectItem>
                    ))}
                  </SimpleSelect>
                </td>
                <td className="px-4 py-2">
                  <Checkbox
                    aria-label="New mapping is a legacy spelling"
                    checked={newLegacy}
                    onCheckedChange={(value) => setNewLegacy(value === true)}
                  />
                </td>
                <td className="px-4 py-2 text-right">
                  <Button
                    size="sm"
                    disabled={newStatus.trim() === '' || create.isPending}
                    onClick={() => void (async () => {
                      const ok = await run(() => create.mutateAsync({
                        aconexStatus: newStatus.trim(),
                        status: newUnified,
                        isLegacy: newLegacy,
                      }), 'Could not add the mapping')
                      if (ok) { setNewStatus(''); setNewLegacy(false) }
                    })()}
                  >
                    <Plus aria-hidden />
                    Add item
                  </Button>
                </td>
              </tr>
            ) : null}

            {rows.map((row) => {
              const isEditing = editing?.id === row.id
              return (
                <tr
                  key={row.id}
                  className={`border-b border-border last:border-0 ${row.isDeleted ? 'text-muted-foreground' : ''}`}
                >
                  <td className="px-4 py-2">
                    {isEditing ? (
                      <Input
                        aria-label="Aconex status"
                        value={editing.aconexStatus}
                        onChange={(event) => setEditing({ ...editing, aconexStatus: event.target.value })}
                        className="h-8"
                      />
                    ) : (
                      <span className="flex items-center gap-2">
                        {row.aconexStatus}
                        {row.isDeleted ? <Badge tone="neutral">Deleted</Badge> : null}
                      </span>
                    )}
                  </td>

                  <td className="px-4 py-2">
                    {isEditing ? (
                      <SimpleSelect
                        className="h-8"
                        label="Unified status"
                        value={editing.status}
                        onValueChange={(value) => setEditing({ ...editing, status: value as UnifiedStatus })}
                      >
                        {unifiedStatuses.map((status) => (
                          <SelectItem key={status} value={status}>{status}</SelectItem>
                        ))}
                      </SimpleSelect>
                    ) : <StatusBadge status={row.status} />}
                  </td>

                  <td className="px-4 py-2">
                    {isEditing ? (
                      <Checkbox
                        aria-label="Legacy spelling"
                        checked={editing.isLegacy}
                        onCheckedChange={(value) => setEditing({ ...editing, isLegacy: value === true })}
                      />
                    ) : row.isLegacy ? <Badge tone="neutral">Legacy spelling</Badge> : null}
                  </td>

                  <td className="px-4 py-2">
                    <div className="flex items-center justify-end gap-1">
                      {!canManage ? null : row.isDeleted ? (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => void run(() => restore.mutateAsync(row.id), 'Could not restore the mapping')}
                        >
                          <RotateCcw aria-hidden />
                          Restore
                        </Button>
                      ) : isEditing ? (
                        <>
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            aria-label="Save"
                            onClick={() => void (async () => {
                              const ok = await run(() => update.mutateAsync({
                                id: editing.id,
                                aconexStatus: editing.aconexStatus.trim(),
                                status: editing.status,
                                isLegacy: editing.isLegacy,
                              }), 'Could not save the mapping')
                              if (ok) setEditing(null)
                            })()}
                          >
                            <Check aria-hidden />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            aria-label="Cancel"
                            onClick={() => { setEditing(null); setError(null) }}
                          >
                            <X aria-hidden />
                          </Button>
                        </>
                      ) : (
                        <>
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            aria-label={`Edit ${row.aconexStatus}`}
                            onClick={() => setEditing(row)}
                          >
                            <Pencil aria-hidden />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            aria-label={`Delete ${row.aconexStatus}`}
                            onClick={() => setPendingDelete(row)}
                          >
                            <Trash2 aria-hidden />
                          </Button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </CardBody>

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Delete this mapping?"
        description={pendingDelete
          ? `'${pendingDelete.aconexStatus}' stops mapping to a unified status, so rows carrying it will report no status until it is restored or replaced.`
          : ''}
        confirmLabel="Delete"
        pending={remove.isPending}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => void (async () => {
          if (!pendingDelete) return
          await run(() => remove.mutateAsync(pendingDelete.id), 'Could not delete the mapping')
          setPendingDelete(null)
        })()}
      />
    </Card>
  )
}
