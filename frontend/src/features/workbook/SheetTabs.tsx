import { cn } from '@/shared/lib/utils'

// Excel's own tab strip: the active tab is white and joined to the sheet above it.
export function SheetTabs({ sheets, active, onChange }: {
  sheets: string[]
  active: string
  onChange: (sheet: string) => void
}) {
  return (
    <div role="tablist" aria-label="Sheets" className="flex items-end gap-0.5 border-t border-border bg-muted px-2 pt-1">
      {sheets.map((sheet) => (
        <button
          key={sheet}
          type="button"
          role="tab"
          aria-selected={sheet === active}
          onClick={() => onChange(sheet)}
          className={cn(
            'rounded-t-md border border-b-0 px-3 py-1 text-xs',
            sheet === active
              ? 'border-border bg-background font-medium text-foreground'
              : 'border-transparent bg-muted text-muted-foreground hover:text-foreground',
          )}
        >
          {sheet}
        </button>
      ))}
    </div>
  )
}
