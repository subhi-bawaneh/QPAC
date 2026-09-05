import { useState } from 'react'
import { Dialog } from '@/shared/ui/dialog'
import { Button } from '@/shared/ui/button'
import { Input } from '@/shared/ui/input'
import { apiErrorMessage } from '@/shared/api/client'
import { useBulkUpdateDraftDocuments } from './api'
import { buildBulkFields } from './draftEdit'

const fields = [
  { key: 'packageName', label: 'Package name' },
  { key: 'scopeArea', label: 'Scope area' },
  { key: 'authoringSoftware', label: 'Authoring software' },
  { key: 'exchangeFormat', label: 'Exchange format' },
  { key: 'scale', label: 'Scale' },
  { key: 'classificationCode', label: 'Classification code' },
  { key: 'corporateDiscipline', label: 'Corporate discipline' },
] as const

export function BulkEditDialog({ open, onClose, folderFileId, ids }: {
  open: boolean
  onClose: () => void
  folderFileId: string
  ids: string[]
}) {
  const [values, setValues] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [updated, setUpdated] = useState<number | null>(null)
  const bulk = useBulkUpdateDraftDocuments(folderFileId)

  const onApply = async () => {
    const payload = buildBulkFields(values)
    if (Object.keys(payload).length === 0) {
      setError('Fill in at least one field')
      return
    }

    setError(null)
    try {
      setUpdated(await bulk.mutateAsync({ ids, fields: payload }))
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The bulk update failed'))
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={`Edit ${ids.length.toLocaleString('en-GB')} row(s)`}
      description="A field left blank is untouched; a value replaces it on every selected row."
    >
      <div className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-2">
          {fields.map((field) => (
            <label key={field.key} className="space-y-1 text-sm">
              <span className="text-xs text-muted-foreground">{field.label}</span>
              <Input
                aria-label={field.label}
                value={values[field.key] ?? ''}
                onChange={(event) =>
                  setValues((current) => ({ ...current, [field.key]: event.target.value }))
                }
              />
            </label>
          ))}
        </div>

        {updated !== null ? (
          <p className="rounded-md bg-emerald-100 px-3 py-2 text-sm text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200">
            Updated {updated.toLocaleString('en-GB')} row(s).
          </p>
        ) : null}

        {error ? (
          <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {error}
          </p>
        ) : null}

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose} disabled={bulk.isPending}>
            {updated !== null ? 'Close' : 'Cancel'}
          </Button>
          {updated === null ? (
            <Button onClick={() => void onApply()} disabled={bulk.isPending}>
              {bulk.isPending ? 'Applying…' : 'Apply to selection'}
            </Button>
          ) : null}
        </div>
      </div>
    </Dialog>
  )
}
