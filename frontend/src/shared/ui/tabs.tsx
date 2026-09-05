import type { ReactNode } from 'react'
import { cn } from '@/shared/lib/utils'

export interface TabItem {
  id: string
  label: string
  count?: number
  tone?: 'default' | 'danger'
}

export function Tabs({ items, active, onChange }: {
  items: TabItem[]
  active: string
  onChange: (id: string) => void
}) {
  return (
    <div role="tablist" className="inline-flex h-9 items-center justify-start gap-1 rounded-lg bg-muted p-1 text-muted-foreground">
      {items.map((item) => (
        <button
          key={item.id}
          role="tab"
          type="button"
          aria-selected={item.id === active}
          onClick={() => onChange(item.id)}
          className={cn(
            'inline-flex items-center gap-2 whitespace-nowrap rounded-md px-3 py-1 text-sm font-medium',
            'ring-offset-background transition-all focus-visible:outline-none focus-visible:ring-2',
            'focus-visible:ring-ring focus-visible:ring-offset-2',
            item.id === active
              ? 'bg-background text-foreground shadow'
              : 'hover:text-foreground',
          )}
        >
          {item.label}
          {item.count !== undefined ? (
            <span
              className={cn(
                'rounded-full px-1.5 py-0.5 text-[11px] tabular-nums',
                item.tone === 'danger' && item.count > 0
                  ? 'bg-destructive/15 text-destructive'
                  : 'bg-muted-foreground/15',
              )}
            >
              {item.count.toLocaleString('en-GB')}
            </span>
          ) : null}
        </button>
      ))}
    </div>
  )
}

export function TabPanel({ id, active, children }: { id: string; active: string; children: ReactNode }) {
  if (id !== active) return null
  return <div role="tabpanel" className="mt-4">{children}</div>
}
