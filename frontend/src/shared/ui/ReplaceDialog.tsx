import { Dialog, DialogFooter } from '@/shared/ui/dialog'
import { Button } from '@/shared/ui/button'
import { Spinner } from '@/shared/ui/spinner'
import { formatNumber } from '@/shared/lib/utils'

export interface ReplacePreview {
  tidpFileId: string
  fileName: string
  disciplineName: string
  rows: number
  editedRows: number
  uploadedAt: string
  uploadedBy: string
}

// Replacing or deleting a file destroys its rows, hand edits included. The dialog does
// not soften that — it states it, with the real count of edited rows rather than a
// warning that some might be lost, so the operator decides against a number.
export function ReplaceDialog({
  open,
  action,
  preview,
  pending,
  onCancel,
  onConfirm,
}: {
  open: boolean
  action: 'replace' | 'delete'
  preview: ReplacePreview | null
  pending?: boolean
  onCancel: () => void
  onConfirm: () => void
}) {
  const verb = action === 'replace' ? 'Replace' : 'Delete'

  return (
    <Dialog
      open={open}
      onClose={onCancel}
      title={`${verb} ${preview?.fileName ?? 'file'}?`}
      description={
        action === 'replace'
          ? 'The uploaded workbook becomes the whole content of this file.'
          : 'The file and every row it produced leave the register.'
      }
    >
      {preview === null ? (
        <Spinner />
      ) : (
        <div className="space-y-3 text-sm">
          <p>
            This will {action} <strong>{formatNumber(preview.rows)}</strong>{' '}
            {preview.rows === 1 ? 'row' : 'rows'} in {preview.disciplineName || 'this discipline'}.
          </p>

          {preview.editedRows > 0 ? (
            <p className="rounded-md border border-destructive/40 bg-destructive/5 p-3 text-destructive">
              <strong>{formatNumber(preview.editedRows)}</strong>{' '}
              {preview.editedRows === 1 ? 'row was' : 'rows were'} edited by hand and will be
              lost. A copy of every row is written to the audit log first.
            </p>
          ) : (
            <p className="text-muted-foreground">
              No row has been edited by hand. A copy of every row is written to the audit
              log first.
            </p>
          )}
        </div>
      )}

      <DialogFooter>
        <Button variant="outline" onClick={onCancel} disabled={pending}>
          Cancel
        </Button>
        <Button variant="destructive" onClick={onConfirm} disabled={pending || preview === null}>
          {pending ? 'Working…' : verb}
        </Button>
      </DialogFooter>
    </Dialog>
  )
}
