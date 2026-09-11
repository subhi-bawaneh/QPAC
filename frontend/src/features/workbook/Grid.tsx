import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useVirtualizer } from '@tanstack/react-virtual'
import type { WorkbookColumn, WorkbookRow } from '@/shared/api/types'
import { Cell } from './Cell'
import { CellEditor } from './CellEditor'

export interface ActiveCell {
  row: number
  column: number
}

const ROW_HEIGHT = 22
const GUTTER_WIDTH = 56

// A spreadsheet, not a table: rows are virtualised (the MIDP sheet runs to 15,883),
// the column-letter row and the row-number gutter stay put, and column A is frozen
// because it is the number every other column composes.
export function Grid({
  columns, rows, canEdit, saving, saveError,
  onActiveChange, onSave, onClearError, onScrollEnd,
}: {
  columns: WorkbookColumn[]
  rows: WorkbookRow[]
  canEdit: boolean
  saving: boolean
  saveError: string | null
  onActiveChange: (cell: ActiveCell) => void
  onSave: (row: WorkbookRow, columnIndex: number, value: string) => Promise<void>
  onClearError: () => void
  onScrollEnd: () => void
}) {
  const scroller = useRef<HTMLDivElement>(null)
  const [active, setActive] = useState<ActiveCell>({ row: 0, column: 0 })
  const [anchor, setAnchor] = useState<ActiveCell>({ row: 0, column: 0 })
  const [editing, setEditing] = useState<{ cell: ActiveCell; value: string } | null>(null)

  const virtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => scroller.current,
    estimateSize: () => ROW_HEIGHT,
    overscan: 12,
  })

  useEffect(() => { onActiveChange(active) }, [active, onActiveChange])

  const move = useCallback((rowDelta: number, columnDelta: number, extend = false) => {
    setActive((current) => {
      const next = {
        row: Math.min(Math.max(current.row + rowDelta, 0), Math.max(rows.length - 1, 0)),
        column: Math.min(Math.max(current.column + columnDelta, 0), columns.length - 1),
      }
      if (!extend) setAnchor(next)
      virtualizer.scrollToIndex(next.row, { align: 'auto' })
      return next
    })
  }, [rows.length, columns.length, virtualizer])

  const selected = useCallback((rowIndex: number, columnIndex: number) => {
    const top = Math.min(anchor.row, active.row)
    const bottom = Math.max(anchor.row, active.row)
    const left = Math.min(anchor.column, active.column)
    const right = Math.max(anchor.column, active.column)
    return rowIndex >= top && rowIndex <= bottom && columnIndex >= left && columnIndex <= right
  }, [anchor, active])

  const copySelection = useCallback(() => {
    const top = Math.min(anchor.row, active.row)
    const bottom = Math.max(anchor.row, active.row)
    const left = Math.min(anchor.column, active.column)
    const right = Math.max(anchor.column, active.column)

    const tsv = rows.slice(top, bottom + 1)
      .map((row) => row.cells.slice(left, right + 1).map((cell) => cell ?? '').join('\t'))
      .join('\n')

    void navigator.clipboard?.writeText(tsv)
  }, [anchor, active, rows])

  const startEditing = useCallback((cell: ActiveCell, initial?: string) => {
    const column = columns[cell.column]
    if (!canEdit || !column?.editable) return
    const row = rows[cell.row]
    if (!row) return
    onClearError()
    setEditing({ cell, value: initial ?? row.cells[cell.column] ?? '' })
  }, [canEdit, columns, rows, onClearError])

  // Closing the editor hands focus back to the grid, so the next keystroke moves
  // the active cell instead of falling on the document.
  const stopEditing = useCallback(() => {
    setEditing(null)
    scroller.current?.focus()
  }, [])

  const commit = async (direction: 'down' | 'right' | 'none') => {
    if (!editing) return
    const row = rows[editing.cell.row]
    try {
      await onSave(row, editing.cell.column, editing.value)
    } catch {
      // The page keeps the editor open and shows the API's message on the cell.
      return
    }
    stopEditing()
    if (direction === 'down') move(1, 0)
    if (direction === 'right') move(0, 1)
  }

  const onKeyDown = (event: React.KeyboardEvent) => {
    if (editing) return

    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'c') {
      event.preventDefault()
      copySelection()
      return
    }

    switch (event.key) {
      case 'ArrowDown': event.preventDefault(); move(1, 0, event.shiftKey); break
      case 'ArrowUp': event.preventDefault(); move(-1, 0, event.shiftKey); break
      case 'ArrowRight': event.preventDefault(); move(0, 1, event.shiftKey); break
      case 'ArrowLeft': event.preventDefault(); move(0, -1, event.shiftKey); break
      case 'Home': event.preventDefault(); setActive((c) => ({ ...c, column: 0 })); break
      case 'End': event.preventDefault(); setActive((c) => ({ ...c, column: columns.length - 1 })); break
      case 'PageDown': event.preventDefault(); move(20, 0); break
      case 'PageUp': event.preventDefault(); move(-20, 0); break
      case 'F2': event.preventDefault(); startEditing(active); break
      case 'Enter': event.preventDefault(); startEditing(active); break
      default:
        if (event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey) {
          startEditing(active, event.key)
          event.preventDefault()
        }
        break
    }
  }

  const totalWidth = useMemo(
    () => columns.reduce((sum, column) => sum + column.width, GUTTER_WIDTH),
    [columns],
  )

  return (
    <div
      ref={scroller}
      role="grid"
      aria-label="Workbook"
      aria-rowcount={rows.length}
      aria-colcount={columns.length}
      tabIndex={0}
      onKeyDown={onKeyDown}
      onScroll={(event) => {
        const element = event.currentTarget
        if (element.scrollHeight - element.scrollTop - element.clientHeight < 400) onScrollEnd()
      }}
      className="relative max-h-[70vh] overflow-auto font-[Calibri,Segoe_UI,sans-serif] outline-none"
    >
      <div style={{ width: totalWidth }}>
        {/* Column letters */}
        <div role="row" className="sticky top-0 z-30 flex bg-[#f2f2f2] dark:bg-[#2b2b2b]">
          <div
            className="sticky left-0 z-40 h-[22px] shrink-0 border-b border-r border-[#d9d9d9] bg-[#f2f2f2] dark:border-[#3a3a3a] dark:bg-[#2b2b2b]"
            style={{ width: GUTTER_WIDTH }}
          />
          {columns.map((column, index) => (
            <div
              key={column.letter}
              role="columnheader"
              title={column.title}
              style={{
                width: column.width,
                minWidth: column.width,
                ...(index === 0 ? { position: 'sticky', left: GUTTER_WIDTH, zIndex: 35 } : null),
              }}
              className="h-[22px] shrink-0 border-b border-r border-[#d9d9d9] bg-[#f2f2f2] text-center text-xs
                font-medium leading-[22px] dark:border-[#3a3a3a] dark:bg-[#2b2b2b]"
            >
              {column.letter}
            </div>
          ))}
        </div>

        {/* Column titles */}
        <div role="row" className="sticky top-[22px] z-30 flex bg-[#f2f2f2] dark:bg-[#2b2b2b]">
          <div
            className="sticky left-0 z-40 h-[22px] shrink-0 border-b border-r border-[#d9d9d9] bg-[#f2f2f2] dark:border-[#3a3a3a] dark:bg-[#2b2b2b]"
            style={{ width: GUTTER_WIDTH }}
          />
          {columns.map((column, index) => (
            <div
              key={column.key}
              style={{
                width: column.width,
                minWidth: column.width,
                ...(index === 0 ? { position: 'sticky', left: GUTTER_WIDTH, zIndex: 35 } : null),
              }}
              className="h-[22px] shrink-0 truncate border-b border-r border-[#d9d9d9] bg-[#f2f2f2] px-1 text-[11px]
                font-semibold uppercase leading-[22px] text-muted-foreground dark:border-[#3a3a3a] dark:bg-[#2b2b2b]"
            >
              {column.title}
            </div>
          ))}
        </div>

        <div style={{ height: virtualizer.getTotalSize(), position: 'relative' }}>
          {virtualizer.getVirtualItems().map((virtualRow) => {
            const row = rows[virtualRow.index]
            return (
              <div
                key={row.rowId}
                role="row"
                aria-rowindex={row.rowNumber}
                className="absolute left-0 flex"
                style={{ top: virtualRow.start, height: ROW_HEIGHT, width: totalWidth }}
              >
                <div
                  role="rowheader"
                  // A row a person edited is marked, so hand-entered data is never
                  // mistaken for imported data — and so the count the replace dialog
                  // shows has something visible behind it.
                  title={row.state === 'Edited' ? 'Edited by hand' : undefined}
                  className="sticky left-0 z-10 h-[22px] shrink-0 border-b border-r border-[#d9d9d9] bg-[#f2f2f2]
                    text-center text-[11px] leading-[22px] text-muted-foreground dark:border-[#3a3a3a] dark:bg-[#2b2b2b]"
                  style={{ width: GUTTER_WIDTH }}
                >
                  {row.state === 'Edited' ? (
                    <span className="text-primary" aria-label="Edited by hand">•</span>
                  ) : null}
                  {row.rowNumber}
                </div>

                {columns.map((column, columnIndex) => {
                  const isActive = active.row === virtualRow.index && active.column === columnIndex
                  const isEditing = editing?.cell.row === virtualRow.index
                    && editing.cell.column === columnIndex

                  return (
                    <Cell
                      key={column.key}
                      column={column}
                      value={row.cells[columnIndex]}
                      active={isActive}
                      selected={selected(virtualRow.index, columnIndex)}
                      frozen={columnIndex === 0}
                      left={GUTTER_WIDTH}
                      onMouseDown={(event) => {
                        const cell = { row: virtualRow.index, column: columnIndex }
                        setActive(cell)
                        if (!event.shiftKey) setAnchor(cell)
                      }}
                      onDoubleClick={() => startEditing({ row: virtualRow.index, column: columnIndex })}
                    >
                      {isEditing ? (
                        <CellEditor
                          column={column}
                          value={editing.value}
                          error={saveError}
                          onChange={(value) => setEditing({ ...editing, value })}
                          onCommit={(where) => { if (!saving) void commit(where) }}
                          onCancel={() => { stopEditing(); onClearError() }}
                        />
                      ) : null}
                    </Cell>
                  )
                })}
              </div>
            )
          })}
        </div>
      </div>
    </div>
  )
}
