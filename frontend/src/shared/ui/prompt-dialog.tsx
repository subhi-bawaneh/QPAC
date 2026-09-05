import { useEffect, useState } from 'react'
import { Dialog, DialogFooter } from './dialog'
import { Button } from './button'
import { Input } from './input'

/** Asks for one value. Replaces window.prompt, which cannot be styled or tested. */
export function PromptDialog({ open, title, description, label, initialValue = '', confirmLabel = 'Save', onCancel, onConfirm, pending }: {
  open: boolean
  title: string
  description?: string
  label: string
  initialValue?: string
  confirmLabel?: string
  pending?: boolean
  onCancel: () => void
  onConfirm: (value: string) => void
}) {
  const [value, setValue] = useState(initialValue)

  useEffect(() => {
    if (open) setValue(initialValue)
  }, [open, initialValue])

  return (
    <Dialog open={open} onClose={onCancel} title={title} description={description}>
      <form
        className="space-y-4"
        onSubmit={(event) => {
          event.preventDefault()
          if (value.trim()) onConfirm(value.trim())
        }}
      >
        <label className="block space-y-1.5">
          <span className="text-sm font-medium">{label}</span>
          <Input value={value} autoFocus aria-label={label} onChange={(e) => setValue(e.target.value)} />
        </label>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={onCancel} disabled={pending}>Cancel</Button>
          <Button type="submit" disabled={pending || value.trim().length === 0}>
            {pending ? 'Working…' : confirmLabel}
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  )
}

/** Confirms a destructive action. Replaces window.confirm. */
export function ConfirmDialog({ open, title, description, confirmLabel = 'Delete', onCancel, onConfirm, pending }: {
  open: boolean
  title: string
  description: string
  confirmLabel?: string
  pending?: boolean
  onCancel: () => void
  onConfirm: () => void
}) {
  return (
    <Dialog open={open} onClose={onCancel} title={title} description={description}>
      <DialogFooter>
        <Button variant="outline" onClick={onCancel} disabled={pending}>Cancel</Button>
        <Button variant="destructive" onClick={onConfirm} disabled={pending}>
          {pending ? 'Working…' : confirmLabel}
        </Button>
      </DialogFooter>
    </Dialog>
  )
}
