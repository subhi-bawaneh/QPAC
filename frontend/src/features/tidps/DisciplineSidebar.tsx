import { cn, formatNumber } from '@/shared/lib/utils'
import { FolderMark } from './IconGrid'

export interface SidebarDiscipline {
  id: string
  corporateName: string
  fileCount: number
}

// The disciplines, always all of them. A discipline with no TIDP still appears with a
// zero: an empty discipline is exactly the thing an operator needs to notice, and
// hiding it would make the register look complete when it is not.
export function DisciplineSidebar({ disciplines, selectedId, onSelect }: {
  disciplines: SidebarDiscipline[]
  selectedId: string | null
  onSelect: (id: string | null) => void
}) {
  return (
    <nav aria-label="Disciplines" className="space-y-0.5">
      <button
        type="button"
        onClick={() => onSelect(null)}
        className={cn(
          'flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-sm',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          selectedId === null ? 'bg-selection/15 font-medium' : 'hover:bg-muted',
        )}
      >
        <FolderMark className="h-4 w-4" />
        All disciplines
      </button>

      {disciplines.map((discipline) => (
        <button
          key={discipline.id}
          type="button"
          onClick={() => onSelect(discipline.id)}
          className={cn(
            'flex w-full items-center justify-between gap-2 rounded-md px-2 py-1.5 text-left text-sm',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            selectedId === discipline.id ? 'bg-selection/15 font-medium' : 'hover:bg-muted',
          )}
        >
          <span className="flex min-w-0 items-center gap-2">
            <FolderMark className="h-4 w-4 shrink-0" />
            <span className="truncate">{discipline.corporateName}</span>
          </span>
          <span
            className={cn(
              'shrink-0 tabular-nums text-xs',
              discipline.fileCount === 0 ? 'text-muted-foreground/60' : 'text-muted-foreground',
            )}
          >
            {formatNumber(discipline.fileCount)}
          </span>
        </button>
      ))}
    </nav>
  )
}
