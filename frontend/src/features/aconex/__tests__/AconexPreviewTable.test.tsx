import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { AconexPreviewTable } from '../AconexPreviewTable'
import type { AconexPreview } from '@/shared/api/types'

const preview: AconexPreview = {
  files: [
    { fileName: 'export-march.xlsx', rowsRead: 25246, rowsNew: 25246, rowsDuplicate: 0, error: null },
    { fileName: 'export-april.xlsx', rowsRead: 25300, rowsNew: 54, rowsDuplicate: 25246, error: null },
  ],
  totalRowsRead: 50546,
  totalRowsNew: 25300,
  totalRowsDuplicate: 25246,
}

describe('AconexPreviewTable', () => {
  // This is the only decision an operator cannot make without the numbers: they export
  // periodically and cannot remember what was already loaded.
  it('shows read, new and already-held per file', () => {
    render(<AconexPreviewTable preview={preview} />)

    expect(screen.getByText('export-april.xlsx')).toBeInTheDocument()
    expect(screen.getByText('54')).toBeInTheDocument()
    expect(screen.getAllByText('25,246').length).toBeGreaterThan(0)
  })

  it('says plainly when an upload would change nothing', () => {
    render(
      <AconexPreviewTable
        preview={{
          files: [{ fileName: 'again.xlsx', rowsRead: 25246, rowsNew: 0, rowsDuplicate: 25246, error: null }],
          totalRowsRead: 25246,
          totalRowsNew: 0,
          totalRowsDuplicate: 25246,
        }}
      />,
    )

    expect(screen.getByText(/Uploading changes nothing/)).toBeInTheDocument()
  })

  it('surfaces a file the server could not read', () => {
    render(
      <AconexPreviewTable
        preview={{
          files: [{ fileName: 'wrong.xlsx', rowsRead: 0, rowsNew: 0, rowsDuplicate: 0, error: "No 'Aconex History' sheet" }],
          totalRowsRead: 0,
          totalRowsNew: 0,
          totalRowsDuplicate: 0,
        }}
      />,
    )

    expect(screen.getByText("No 'Aconex History' sheet")).toBeInTheDocument()
  })
})
