import { useEffect, useState } from 'react'
import { Dialog } from '@/shared/ui/dialog'
import { Button } from '@/shared/ui/button'
import { Input } from '@/shared/ui/input'
import { apiErrorMessage } from '@/shared/api/client'
import type { DraftDocument, DraftDocumentEdit } from '@/shared/api/types'
import { useUpdateDraftDocument } from './api'
import { toEdit } from './draftEdit'

const numberingFields: { key: keyof DraftDocumentEdit; label: string }[] = [
  { key: 'f01Project', label: 'Project' },
  { key: 'f02Originator', label: 'Originator' },
  { key: 'f03Contract', label: 'Contract' },
  { key: 'f04DocType', label: 'Document type' },
  { key: 'f05Discipline', label: 'Discipline' },
  { key: 'f06Zone', label: 'Zone' },
  { key: 'f07Building', label: 'Building' },
  { key: 'f08ADrawingType', label: 'Drawing type' },
  { key: 'f08BLevel', label: 'Level' },
  { key: 'f08CSequence', label: 'Sequence' },
]

const detailFields: { key: keyof DraftDocumentEdit; label: string }[] = [
  { key: 'title', label: 'Title' },
  { key: 'corporateDiscipline', label: 'Corporate discipline' },
  { key: 'packageName', label: 'Package name' },
  { key: 'activityId', label: 'Activity ID' },
  { key: 'scopeArea', label: 'Scope area' },
  { key: 'authoringSoftware', label: 'Authoring software' },
  { key: 'exchangeFormat', label: 'Exchange format' },
  { key: 'scale', label: 'Scale' },
  { key: 'classificationCode', label: 'Classification code' },
  { key: 'extractedFromModel', label: 'Extracted from model' },
]

export function DraftRowEditor({ row, folderFileId, onClose }: {
  row: DraftDocument | null
  folderFileId: string
  onClose: () => void
}) {
  const [edit, setEdit] = useState<DraftDocumentEdit | null>(null)
  const [error, setError] = useState<string | null>(null)
  const update = useUpdateDraftDocument(folderFileId)

  useEffect(() => {
    setEdit(row ? toEdit(row) : null)
    setError(null)
  }, [row])

  if (!row || !edit) return null

  const set = (key: keyof DraftDocumentEdit, value: string) =>
    setEdit((current) => (current ? { ...current, [key]: value } : current))

  const onSave = async () => {
    setError(null)
    try {
      await update.mutateAsync({ id: row.id, edit })
      onClose()
    } catch (caught) {
      setError(apiErrorMessage(caught, 'Could not save the row'))
    }
  }

  return (
    <Dialog
      open
      onClose={onClose}
      title={row.documentNumber}
      description="The document number is rebuilt from the eight fields below — it is never typed."
    >
      <div className="max-h-[60vh] space-y-4 overflow-y-auto pr-1">
        <section>
          <h3 className="mb-2 text-xs font-semibold uppercase text-muted-foreground">Details</h3>
          <div className="grid gap-3 sm:grid-cols-2">
            {detailFields.map((field) => (
              <label key={field.key} className="space-y-1 text-sm">
                <span className="text-xs text-muted-foreground">{field.label}</span>
                <Input
                  value={edit[field.key] ?? ''}
                  aria-label={field.label}
                  onChange={(event) => set(field.key, event.target.value)}
                />
              </label>
            ))}
          </div>
        </section>

        <section>
          <h3 className="mb-2 text-xs font-semibold uppercase text-muted-foreground">
            Numbering
          </h3>
          <div className="grid gap-3 sm:grid-cols-3">
            {numberingFields.map((field) => (
              <label key={field.key} className="space-y-1 text-sm">
                <span className="text-xs text-muted-foreground">{field.label}</span>
                <Input
                  value={edit[field.key] ?? ''}
                  aria-label={field.label}
                  onChange={(event) => set(field.key, event.target.value)}
                />
              </label>
            ))}
          </div>
        </section>

        {error ? (
          <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {error}
          </p>
        ) : null}
      </div>

      <div className="mt-4 flex justify-end gap-2">
        <Button variant="outline" onClick={onClose} disabled={update.isPending}>Cancel</Button>
        <Button onClick={() => void onSave()} disabled={update.isPending}>
          {update.isPending ? 'Saving…' : 'Save row'}
        </Button>
      </div>
    </Dialog>
  )
}
