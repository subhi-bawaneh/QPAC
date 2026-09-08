import { useEffect, useRef } from 'react'
import type { WorkbookColumn } from '@/shared/api/types'

// The inline editor: an input laid over the cell. Enter and Tab commit, Esc cancels,
// and a validation error from the API keeps the editor open with the message on it.
export function CellEditor({ column, value, error, onChange, onCommit, onCancel }: {
  column: WorkbookColumn
  value: string
  error: string | null
  onChange: (value: string) => void
  onCommit: (move: 'down' | 'right' | 'none') => void
  onCancel: () => void
}) {
  const input = useRef<HTMLInputElement>(null)

  useEffect(() => {
    input.current?.focus()
    input.current?.select()
  }, [])

  return (
    <div className="absolute inset-0 z-20">
      <input
        ref={input}
        aria-label={`Edit ${column.title}`}
        title={error ?? undefined}
        value={value}
        inputMode={column.kind === 'int' ? 'numeric' : 'text'}
        onChange={(event) => onChange(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') { event.preventDefault(); onCommit('down') }
          else if (event.key === 'Tab') { event.preventDefault(); onCommit('right') }
          else if (event.key === 'Escape') { event.preventDefault(); onCancel() }
          else event.stopPropagation()
        }}
        className={`h-full w-full border-2 px-1 text-xs outline-none
          ${error ? 'border-destructive bg-destructive/5' : 'border-[#217346] bg-background'}`}
      />
      {error ? (
        <p role="alert" className="absolute left-0 top-full z-30 w-56 rounded-md bg-destructive px-2 py-1 text-xs text-destructive-foreground shadow-lg">
          {error}
        </p>
      ) : null}
    </div>
  )
}
