import { cn } from '@/shared/lib/utils'
import type { WorkbookColumn } from '@/shared/api/types'

export function Cell({ column, value, active, selected, frozen, left, onMouseDown, onDoubleClick, children }: {
  column: WorkbookColumn
  value: string | null
  active: boolean
  selected: boolean
  frozen: boolean
  left?: number
  onMouseDown: (event: React.MouseEvent) => void
  onDoubleClick: () => void
  children?: React.ReactNode
}) {
  return (
    <div
      role="gridcell"
      aria-label={column.title}
      aria-selected={active}
      data-editable={column.editable}
      onMouseDown={onMouseDown}
      onDoubleClick={onDoubleClick}
      style={{
        width: column.width,
        minWidth: column.width,
        ...(frozen ? { position: 'sticky', left, zIndex: 10 } : null),
      }}
      className={cn(
        'relative h-[22px] shrink-0 truncate border-b border-r border-[#d9d9d9] px-1 text-xs leading-[22px]',
        'dark:border-[#3a3a3a]',
        frozen && 'bg-background',
        selected && !active && 'bg-[#217346]/10',
        active && 'outline outline-2 -outline-offset-2 outline-[#217346]',
        !column.editable && 'text-muted-foreground',
      )}
      title={value ?? undefined}
    >
      {children ?? value}
    </div>
  )
}
