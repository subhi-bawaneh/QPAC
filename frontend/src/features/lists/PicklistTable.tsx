import { useState } from 'react'
import { ArrowDown, ArrowUp, Check, Pencil, Plus, RotateCcw, Trash2, X } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Input } from '@/shared/ui/input'
import { Badge } from '@/shared/ui/badge'
import { Card, CardBody } from '@/shared/ui/card'
import { ConfirmDialog } from '@/shared/ui/prompt-dialog'
import { apiErrorMessage } from '@/shared/api/client'
import type { PicklistField, PicklistItem } from '@/shared/api/types'
import { codeOnlyFields, picklistLabels } from './listLabels'
import {
  useCreatePicklistItem, useDeletePicklistItem, useReorderPicklist,
  useRestorePicklistItem, useUpdatePicklistItem,
} from './api'

export function PicklistTable({ field, items, canManage, showDeleted }: {
  field: PicklistField
  items: PicklistItem[]
  canManage: boolean
  showDeleted: boolean
}) {
  const [newCode, setNewCode] = useState('')
  const [newDescription, setNewDescription] = useState('')
  const [editing, setEditing] = useState<{ id: string; code: string; description: string } | null>(null)
  const [pendingDelete, setPendingDelete] = useState<PicklistItem | null>(null)
  const [error, setError] = useState<string | null>(null)

  const create = useCreatePicklistItem()
  const update = useUpdatePicklistItem()
  const remove = useDeletePicklistItem()
  const restore = useRestorePicklistItem()
  const reorder = useReorderPicklist()

  const codeOnly = codeOnlyFields.has(field)
  const live = items.filter((item) => !item.isDeleted)

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

  const moveBy = (item: PicklistItem, delta: number) => {
    const order = live.map((row) => row.id)
    const from = order.indexOf(item.id)
    const to = from + delta
    if (from < 0 || to < 0 || to >= order.length) return
    order.splice(to, 0, ...order.splice(from, 1))
    void run(() => reorder.mutateAsync({ field, ids: order }), 'Could not reorder the list')
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
          <caption className="sr-only">{picklistLabels[field]}</caption>
          <thead className="border-b border-border text-left text-xs uppercase text-muted-foreground">
            <tr>
              <th className="w-12 px-4 py-2 font-medium">#</th>
              <th className="px-4 py-2 font-medium">Code</th>
              {codeOnly ? null : <th className="px-4 py-2 font-medium">Description</th>}
              <th className="w-40 px-4 py-2 text-right font-medium">
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>

          <tbody>
            {canManage ? (
              <tr className="border-b border-border bg-muted/40">
                <td className="px-4 py-2 text-muted-foreground">+</td>
                <td className="px-4 py-2">
                  <Input
                    aria-label={`New ${picklistLabels[field]} code`}
                    value={newCode}
                    onChange={(event) => setNewCode(event.target.value)}
                    className="h-8"
                  />
                </td>
                {codeOnly ? null : (
                  <td className="px-4 py-2">
                    <Input
                      aria-label={`New ${picklistLabels[field]} description`}
                      value={newDescription}
                      onChange={(event) => setNewDescription(event.target.value)}
                      className="h-8"
                    />
                  </td>
                )}
                <td className="px-4 py-2 text-right">
                  <Button
                    size="sm"
                    disabled={newCode.trim() === '' || create.isPending}
                    onClick={() => void (async () => {
                      const ok = await run(
                        () => create.mutateAsync({
                          field,
                          code: newCode.trim(),
                          description: newDescription.trim(),
                        }),
                        'Could not add the item',
                      )
                      if (ok) { setNewCode(''); setNewDescription('') }
                    })()}
                  >
                    <Plus aria-hidden />
                    Add item
                  </Button>
                </td>
              </tr>
            ) : null}

            {items.length === 0 ? (
              <tr>
                <td colSpan={4} className="px-4 py-6 text-sm text-muted-foreground">
                  Nothing in this list yet. Import the Picklists workbook, or add a value above.
                </td>
              </tr>
            ) : null}

            {items.map((item, index) => {
              const isEditing = editing?.id === item.id
              return (
                <tr
                  key={item.id}
                  className={`border-b border-border last:border-0 ${item.isDeleted ? 'text-muted-foreground' : ''}`}
                >
                  <td className="px-4 py-2 tabular-nums text-muted-foreground">{index + 1}</td>

                  <td className="px-4 py-2 font-mono text-xs">
                    {isEditing ? (
                      <Input
                        aria-label="Code"
                        value={editing.code}
                        onChange={(event) => setEditing({ ...editing, code: event.target.value })}
                        className="h-8"
                      />
                    ) : (
                      <span className="flex items-center gap-2">
                        {item.code}
                        {item.isDeleted ? <Badge tone="neutral">Deleted</Badge> : null}
                      </span>
                    )}
                  </td>

                  {codeOnly ? null : (
                    <td className="px-4 py-2">
                      {isEditing ? (
                        <Input
                          aria-label="Description"
                          value={editing.description}
                          onChange={(event) => setEditing({ ...editing, description: event.target.value })}
                          className="h-8"
                        />
                      ) : item.description}
                    </td>
                  )}

                  <td className="px-4 py-2">
                    <div className="flex items-center justify-end gap-1">
                      {!canManage ? null : item.isDeleted ? (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => void run(() => restore.mutateAsync(item.id), 'Could not restore the item')}
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
                                id: item.id,
                                code: editing.code.trim(),
                                description: editing.description.trim(),
                                sortOrder: item.sortOrder,
                              }), 'Could not save the item')
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
                            aria-label={`Move ${item.code} up`}
                            disabled={index === 0 || showDeleted}
                            onClick={() => moveBy(item, -1)}
                          >
                            <ArrowUp aria-hidden />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            aria-label={`Move ${item.code} down`}
                            disabled={index === items.length - 1 || showDeleted}
                            onClick={() => moveBy(item, 1)}
                          >
                            <ArrowDown aria-hidden />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            aria-label={`Edit ${item.code}`}
                            onClick={() => setEditing({
                              id: item.id, code: item.code, description: item.description,
                            })}
                          >
                            <Pencil aria-hidden />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            aria-label={`Delete ${item.code}`}
                            onClick={() => setPendingDelete(item)}
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
        title="Delete this value?"
        description={pendingDelete
          ? `'${pendingDelete.code}' stops being offered. Documents that already carry it keep it, and a re-import of the workbook will not bring it back.`
          : ''}
        confirmLabel="Delete"
        pending={remove.isPending}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => void (async () => {
          if (!pendingDelete) return
          await run(() => remove.mutateAsync(pendingDelete.id), 'Could not delete the item')
          setPendingDelete(null)
        })()}
      />
    </Card>
  )
}
