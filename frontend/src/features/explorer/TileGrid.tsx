import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { FolderPlus, Upload } from 'lucide-react'
import type { FolderFileSummary, FolderNode } from '@/shared/api/types'
import { FolderTile } from './FolderTile'
import { FileTile } from './FileTile'

export type GridItem =
  | { kind: 'folder'; folder: FolderNode }
  | { kind: 'file'; file: FolderFileSummary }

export interface GridSelection {
  kind: 'folder' | 'file'
  id: string
}

// Folders first, then files — the order Drive uses, and the order a person expects
// when they navigate with the arrow keys.
export function TileGrid({
  folders, files, importingFileIds, selection, onSelect, onOpen, onMenu, onDropFiles, canUpload,
}: {
  folders: FolderNode[]
  files: FolderFileSummary[]
  importingFileIds: ReadonlySet<string>
  selection: GridSelection | null
  onSelect: (item: GridSelection | null) => void
  onOpen: (item: GridItem) => void
  onMenu: (item: GridItem, event: React.MouseEvent) => void
  onDropFiles: (files: File[]) => void
  canUpload: boolean
}) {
  const items: GridItem[] = useMemo(() => [
    ...folders.map((folder) => ({ kind: 'folder' as const, folder })),
    ...files.map((file) => ({ kind: 'file' as const, file })),
  ], [folders, files])

  const [dragging, setDragging] = useState(false)
  const container = useRef<HTMLDivElement>(null)

  const idOf = (item: GridItem) => (item.kind === 'folder' ? item.folder.id : item.file.id)
  const indexOfSelection = items.findIndex(
    (item) => selection !== null && item.kind === selection.kind && idOf(item) === selection.id,
  )

  const move = useCallback((delta: number) => {
    if (items.length === 0) return
    const next = Math.min(Math.max(indexOfSelection + delta, 0), items.length - 1)
    const item = items[next]
    onSelect({ kind: item.kind, id: idOf(item) })
  }, [indexOfSelection, items, onSelect])

  const onKeyDown = (event: React.KeyboardEvent) => {
    switch (event.key) {
      case 'ArrowRight': event.preventDefault(); move(1); break
      case 'ArrowLeft': event.preventDefault(); move(-1); break
      case 'ArrowDown': event.preventDefault(); move(4); break
      case 'ArrowUp': event.preventDefault(); move(-4); break
      case 'Enter':
        if (indexOfSelection >= 0) { event.preventDefault(); onOpen(items[indexOfSelection]) }
        break
      default: break
    }
  }

  // Drag-and-drop upload onto the grid. The counter guards against the dragleave
  // that fires when the pointer crosses a child element.
  const dragDepth = useRef(0)
  useEffect(() => { dragDepth.current = 0 }, [folders, files])

  if (items.length === 0) {
    return (
      <div
        className="flex min-h-[220px] flex-col items-center justify-center gap-2 rounded-lg
          border border-dashed border-border text-sm text-muted-foreground"
        onDragOver={(event) => event.preventDefault()}
        onDrop={(event) => {
          event.preventDefault()
          if (canUpload) onDropFiles(Array.from(event.dataTransfer.files))
        }}
      >
        <FolderPlus className="h-6 w-6" aria-hidden />
        <p>This folder is empty</p>
        {canUpload ? (
          <p className="flex items-center gap-1 text-xs">
            <Upload className="h-3 w-3" aria-hidden />
            Drop a workbook here
          </p>
        ) : null}
      </div>
    )
  }

  return (
    <div
      ref={container}
      role="grid"
      aria-label="Folder contents"
      tabIndex={0}
      onKeyDown={onKeyDown}
      onDragEnter={() => { dragDepth.current += 1; if (canUpload) setDragging(true) }}
      onDragLeave={() => { dragDepth.current -= 1; if (dragDepth.current <= 0) setDragging(false) }}
      onDragOver={(event) => event.preventDefault()}
      onDrop={(event) => {
        event.preventDefault()
        dragDepth.current = 0
        setDragging(false)
        if (canUpload) onDropFiles(Array.from(event.dataTransfer.files))
      }}
      className={`grid gap-3 rounded-lg p-3 outline-none
        [grid-template-columns:repeat(auto-fill,minmax(160px,1fr))]
        ${dragging ? 'ring-2 ring-primary ring-offset-2' : ''}`}
    >
      {items.map((item) =>
        item.kind === 'folder' ? (
          <FolderTile
            key={`folder-${item.folder.id}`}
            folder={item.folder}
            selected={selection?.kind === 'folder' && selection.id === item.folder.id}
            onSelect={() => onSelect({ kind: 'folder', id: item.folder.id })}
            onOpen={() => onOpen(item)}
            onMenu={(event) => onMenu(item, event)}
          />
        ) : (
          <FileTile
            key={`file-${item.file.id}`}
            file={item.file}
            selected={selection?.kind === 'file' && selection.id === item.file.id}
            importing={importingFileIds.has(item.file.id)}
            onSelect={() => onSelect({ kind: 'file', id: item.file.id })}
            onOpen={() => onOpen(item)}
            onMenu={(event) => onMenu(item, event)}
          />
        ),
      )}
    </div>
  )
}
