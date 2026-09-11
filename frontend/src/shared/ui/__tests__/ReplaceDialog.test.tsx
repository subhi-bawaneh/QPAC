import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ReplaceDialog, type ReplacePreview } from '../ReplaceDialog'

const preview: ReplacePreview = {
  tidpFileId: 'f1',
  fileName: 'TIDP-STL.xlsx',
  disciplineName: 'Structural',
  rows: 1283,
  editedRows: 14,
  uploadedAt: '2026-09-01T10:00:00',
  uploadedBy: 'admin@dip.local',
}

describe('ReplaceDialog', () => {
  // The count is the whole point: an operator decides against a number, not against a
  // warning that some work "may" be lost.
  it('states how many rows and how many hand-edited rows will go', () => {
    render(
      <ReplaceDialog
        open
        action="replace"
        preview={preview}
        onCancel={vi.fn()}
        onConfirm={vi.fn()}
      />,
    )

    expect(screen.getByText('1,283')).toBeInTheDocument()
    expect(screen.getByText('14')).toBeInTheDocument()
    expect(screen.getByText(/edited by hand and will be\s+lost/)).toBeInTheDocument()
  })

  it('says plainly when nothing was edited by hand', () => {
    render(
      <ReplaceDialog
        open
        action="replace"
        preview={{ ...preview, editedRows: 0 }}
        onCancel={vi.fn()}
        onConfirm={vi.fn()}
      />,
    )

    expect(screen.getByText(/No row has been edited by hand/)).toBeInTheDocument()
  })

  it('confirms only on the destructive button', async () => {
    const onConfirm = vi.fn()
    const onCancel = vi.fn()
    render(
      <ReplaceDialog
        open
        action="delete"
        preview={preview}
        onCancel={onCancel}
        onConfirm={onConfirm}
      />,
    )

    screen.getByRole('button', { name: 'Cancel' }).click()
    expect(onConfirm).not.toHaveBeenCalled()
    expect(onCancel).toHaveBeenCalledOnce()

    screen.getByRole('button', { name: 'Delete' }).click()
    expect(onConfirm).toHaveBeenCalledOnce()
  })
})
