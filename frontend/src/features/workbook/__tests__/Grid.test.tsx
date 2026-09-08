import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Grid } from '../Grid'
import type { WorkbookColumn, WorkbookRow } from '@/shared/api/types'
import { documentColumnKeys, columnLetter } from '../columns'

// TanStack Virtual measures its scroller with offsetWidth/offsetHeight, and jsdom
// reports 0 for both — so without this the grid renders no rows at all.
beforeAll(() => {
  Object.defineProperty(HTMLElement.prototype, 'offsetWidth', { configurable: true, value: 1200 })
  Object.defineProperty(HTMLElement.prototype, 'offsetHeight', { configurable: true, value: 600 })
})

const columns: WorkbookColumn[] = documentColumnKeys.map((key, index) => ({
  letter: columnLetter(index),
  key,
  title: key.toUpperCase(),
  width: 120,
  editable: key !== 'documentNumber',
  kind: key.endsWith('DurationDays') ? 'int' : 'text',
}))

const rows: WorkbookRow[] = [1, 2, 3].map((n) => ({
  rowId: `row-${n}`,
  rowNumber: n,
  cells: documentColumnKeys.map((key) => (key === 'documentNumber' ? `DOC-000${n}` : `${key}-${n}`)),
  state: 'Unchanged',
}))

function renderGrid(overrides: Partial<Parameters<typeof Grid>[0]> = {}) {
  const onSave = vi.fn().mockResolvedValue(undefined)
  render(
    <Grid
      columns={columns}
      rows={rows}
      canEdit
      saving={false}
      saveError={null}
      onActiveChange={vi.fn()}
      onSave={onSave}
      onClearError={vi.fn()}
      onScrollEnd={vi.fn()}
      {...overrides}
    />,
  )
  return { onSave }
}

describe('workbook Grid', () => {
  it('renders the column letters from A to AH and the row numbers', () => {
    renderGrid()

    const letters = screen.getAllByRole('columnheader').map((header) => header.textContent)
    expect(letters).toHaveLength(34)
    expect(letters[0]).toBe('A')
    expect(letters[33]).toBe('AH')

    expect(screen.getAllByRole('rowheader').map((header) => header.textContent))
      .toEqual(['1', '2', '3'])
  })

  it('moves the active cell with the arrow keys', async () => {
    const user = userEvent.setup()
    const onActiveChange = vi.fn()
    renderGrid({ onActiveChange })

    screen.getByRole('grid').focus()
    await user.keyboard('{ArrowRight}{ArrowDown}')

    expect(onActiveChange).toHaveBeenLastCalledWith({ row: 1, column: 1 })
  })

  it('opens the editor on F2 for an editable column', async () => {
    const user = userEvent.setup()
    renderGrid()

    screen.getByRole('grid').focus()
    await user.keyboard('{ArrowRight}')
    await user.keyboard('{F2}')

    expect(screen.getByRole('textbox', { name: /edit TITLE/i })).toBeInTheDocument()
  })

  it('refuses to edit column A, which the server recomposes', async () => {
    const user = userEvent.setup()
    renderGrid()

    screen.getByRole('grid').focus()
    await user.keyboard('{F2}')

    expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
  })

  it('refuses to edit at all without the layer permission', async () => {
    const user = userEvent.setup()
    renderGrid({ canEdit: false })

    screen.getByRole('grid').focus()
    await user.keyboard('{ArrowRight}{F2}')

    expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
  })

  it('commits an edit on Enter and cancels on Escape', async () => {
    const user = userEvent.setup()
    const { onSave } = renderGrid()

    screen.getByRole('grid').focus()
    await user.keyboard('{ArrowRight}{F2}')
    await user.keyboard('NEW TITLE{Enter}')

    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({ rowId: 'row-1' }), 1, 'NEW TITLE',
    )

    await user.keyboard('{F2}')
    expect(screen.getByRole('textbox')).toBeInTheDocument()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
  })

  it('keeps the editor open and shows the message when the API rejects the value', async () => {
    const user = userEvent.setup()
    renderGrid({ saveError: 'Sequence must be four digits' })

    screen.getByRole('grid').focus()
    await user.keyboard('{ArrowRight}{F2}')

    expect(screen.getByRole('alert')).toHaveTextContent('Sequence must be four digits')
  })
})
