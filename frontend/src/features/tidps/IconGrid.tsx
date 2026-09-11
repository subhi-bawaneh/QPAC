import { useEffect, useRef } from 'react'
import { Folder, FileSpreadsheet, AlertTriangle, Loader2 } from 'lucide-react'
import { cn } from '@/shared/lib/utils'

export interface GridItem {
  id: string
  name: string
  /** The two lines under the name: a count and a date, or a count on its own. */
  detail: string
  secondary?: string
  kind: 'folder' | 'file'
  state?: 'ok' | 'importing' | 'failed'
}

// Large icons in a flowing grid, the way a file manager lays them out. Selection is a
// soft filled rectangle rather than a border, so it does not shift the tile by a pixel
// when it appears.
export function IconGrid({
  items,
  selectedId,
  onSelect,
  onOpen,
  emptyMessage,
}: {
  items: GridItem[]
  selectedId: string | null
  onSelect: (id: string) => void
  onOpen: (id: string) => void
  emptyMessage: string
}) {
  const container = useRef<HTMLDivElement>(null)

  // Arrow keys move the selection; Enter opens it. A file manager is operated from the
  // keyboard as often as from the mouse.
  useEffect(() => {
    const node = container.current
    if (!node) return

    function onKeyDown(event: KeyboardEvent) {
      if (items.length === 0) return
      const index = items.findIndex((item) => item.id === selectedId)

      if (event.key === 'ArrowRight' || event.key === 'ArrowDown') {
        event.preventDefault()
        onSelect(items[Math.min(index + 1, items.length - 1)]?.id ?? items[0].id)
      } else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') {
        event.preventDefault()
        onSelect(items[Math.max(index - 1, 0)]?.id ?? items[0].id)
      } else if (event.key === 'Enter' && selectedId) {
        event.preventDefault()
        onOpen(selectedId)
      }
    }

    node.addEventListener('keydown', onKeyDown)
    return () => node.removeEventListener('keydown', onKeyDown)
  }, [items, selectedId, onSelect, onOpen])

  if (items.length === 0) {
    return (
      <p className="rounded-md border border-dashed border-border px-8 py-16 text-center text-sm text-muted-foreground">
        {emptyMessage}
      </p>
    )
  }

  return (
    <div
      ref={container}
      role="listbox"
      aria-label="Files"
      tabIndex={0}
      className="grid grid-cols-[repeat(auto-fill,minmax(9rem,1fr))] gap-1 rounded-md p-2 outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      {items.map((item) => (
        <button
          key={item.id}
          type="button"
          role="option"
          aria-selected={item.id === selectedId}
          onClick={() => onSelect(item.id)}
          onDoubleClick={() => onOpen(item.id)}
          className={cn(
            'flex flex-col items-center gap-2 rounded-lg px-2 py-4 text-center transition-colors',
            item.id === selectedId
              ? 'bg-selection/15 ring-1 ring-selection/40'
              : 'hover:bg-muted',
          )}
        >
          <Tile item={item} />
          <span className="w-full truncate text-[13px] font-medium text-icon-label" title={item.name}>
            {item.name}
          </span>
          <span className="text-[11px] leading-tight text-muted-foreground">
            {item.detail}
            {item.secondary ? (
              <>
                <br />
                {item.secondary}
              </>
            ) : null}
          </span>
        </button>
      ))}
    </div>
  )
}

function Tile({ item }: { item: GridItem }) {
  if (item.kind === 'folder') {
    return (
      <span className="relative block h-14 w-16" aria-hidden>
        {/* The tab sits behind the body in the darker shade, which is what makes a flat
            icon read as a folder rather than as a rounded rectangle. */}
        <span className="absolute left-0 top-1 h-4 w-7 rounded-t-md bg-folder-shade" />
        <span className="absolute bottom-0 left-0 h-11 w-16 rounded-md rounded-tl-sm bg-folder" />
      </span>
    )
  }

  return (
    <span className="relative block h-14 w-16" aria-hidden>
      <FileSpreadsheet className="absolute inset-0 m-auto h-12 w-12 text-file" strokeWidth={1.25} />
      {item.state === 'failed' ? (
        <AlertTriangle className="absolute bottom-0 right-2 h-5 w-5 text-destructive" />
      ) : item.state === 'importing' ? (
        <Loader2 className="absolute bottom-0 right-2 h-5 w-5 animate-spin text-file-accent" />
      ) : null}
    </span>
  )
}

// Exported so the sidebar can use the same folder mark at a smaller size.
export function FolderMark({ className }: { className?: string }) {
  return <Folder className={cn('text-folder', className)} aria-hidden />
}
