import { Folder } from 'lucide-react'
import { cn } from '@/shared/lib/utils'
import type { FolderNode } from '@/shared/api/types'
import { TargetBadge } from './FolderBadges'

// A Drive-like tile. 160x132, two-line name clamp, the facts that decide what a
// person can do with the folder underneath it.
export function FolderTile({ folder, selected, onSelect, onOpen, onMenu }: {
  folder: FolderNode
  selected: boolean
  onSelect: () => void
  onOpen: () => void
  onMenu: (event: React.MouseEvent) => void
}) {
  return (
    <button
      type="button"
      role="gridcell"
      aria-label={`Folder ${folder.name}`}
      aria-selected={selected}
      data-testid={`folder-tile-${folder.id}`}
      onClick={onSelect}
      onDoubleClick={onOpen}
      onContextMenu={onMenu}
      className={cn(
        'group flex h-[132px] w-full flex-col items-start gap-1 rounded-lg border border-border bg-card p-3 text-left',
        'transition-all hover:-translate-y-px hover:shadow-md',
        selected && 'ring-2 ring-primary',
      )}
    >
      <div className="flex w-full items-start justify-between">
        <Folder
          className="h-8 w-8 fill-amber-200 text-amber-500 dark:fill-amber-500/30"
          aria-hidden
        />
        {folder.hasNewerDraft ? (
          <span
            title="Drive has newer data"
            className="mt-1 h-2 w-2 rounded-full bg-amber-500"
          />
        ) : null}
      </div>

      <span className="line-clamp-2 text-sm font-medium leading-snug">{folder.name}</span>

      {folder.authorName ? (
        <span className="truncate text-xs text-muted-foreground">{folder.authorName}</span>
      ) : null}

      <div className="mt-auto flex w-full items-center justify-between gap-2">
        <TargetBadge target={folder.target} />
        <span className="text-xs text-muted-foreground">
          {folder.fileCount} {folder.fileCount === 1 ? 'file' : 'files'}
        </span>
      </div>
    </button>
  )
}
