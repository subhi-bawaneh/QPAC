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
    <div role="tablist" className="flex gap-1 border-b border-border">
      {items.map((item) => (
        <button
          key={item.id}
          role="tab"
          type="button"
          aria-selected={item.id === active}
          onClick={() => onChange(item.id)}
          className={cn(
            'flex items-center gap-2 border-b-2 px-4 py-2 text-sm transition',
            item.id === active
              ? 'border-primary font-medium'
              : 'border-transparent text-muted-foreground hover:text-foreground',
          )}
        >
          {item.label}
          {item.count !== undefined ? (
            <span
              className={cn(
                'rounded-full px-2 py-0.5 text-xs tabular-nums',
                item.tone === 'danger' && item.count > 0
                  ? 'bg-destructive/15 text-destructive'
                  : 'bg-muted text-muted-foreground',
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
  return <div role="tabpanel">{children}</div>
}
