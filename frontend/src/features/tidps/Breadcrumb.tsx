import { ChevronRight } from 'lucide-react'
import { cn } from '@/shared/lib/utils'

export interface Crumb {
  id: string | null
  label: string
}

// Two levels only: TIDPs, then a discipline. There is no arbitrary tree any more, so
// the breadcrumb is a statement of where you are rather than a scrolling path.
export function Breadcrumb({ crumbs, onNavigate }: {
  crumbs: Crumb[]
  onNavigate: (id: string | null) => void
}) {
  return (
    <nav aria-label="Breadcrumb">
      <ol className="flex items-center gap-1 text-sm">
        {crumbs.map((crumb, index) => {
          const isLast = index === crumbs.length - 1
          return (
            <li key={crumb.id ?? 'root'} className="flex items-center gap-1">
              {index > 0 ? (
                <ChevronRight className="h-4 w-4 text-muted-foreground" aria-hidden />
              ) : null}
              {isLast ? (
                <span aria-current="page" className="font-medium">{crumb.label}</span>
              ) : (
                <button
                  type="button"
                  onClick={() => onNavigate(crumb.id)}
                  className={cn(
                    'rounded px-1 text-muted-foreground hover:text-foreground',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  )}
                >
                  {crumb.label}
                </button>
              )}
            </li>
          )
        })}
      </ol>
    </nav>
  )
}
