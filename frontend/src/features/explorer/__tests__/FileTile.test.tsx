import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { FileTile } from '../FileTile'
import type { FolderFileSummary } from '@/shared/api/types'

const file = (overrides: Partial<FolderFileSummary> = {}): FolderFileSummary => ({
  id: 'file-1',
  name: 'TIDP-STL.xlsx',
  kind: 'Tidp',
  contentSource: 'Drive',
  contentModifiedAt: '2026-09-01T10:00:00',
  driveFileId: 'drive-1',
  driveModifiedAt: '2026-09-01T10:00:00',
  sizeBytes: 1024,
  state: 'NotImported',
  importError: null,
  lastImportedAt: null,
  rowCount: null,
  hasNewerDraft: false,
  effectiveLayer: 'Draft',
  ...overrides,
})

function renderTile(overrides: Partial<FolderFileSummary> = {}, importing = false) {
  render(
    <FileTile
      file={file(overrides)}
      selected={false}
      importing={importing}
      onSelect={vi.fn()}
      onOpen={vi.fn()}
      onMenu={vi.fn()}
    />,
  )
}

describe('FileTile', () => {
  it('spins while the worker is importing', () => {
    renderTile({}, true)

    expect(screen.getByRole('status', { name: /importing TIDP-STL.xlsx/i })).toBeInTheDocument()
  })

  it('shows the imported badge once the hub says the import finished', () => {
    renderTile({ state: 'Imported', rowCount: 1283 }, false)

    expect(screen.getByText('Imported')).toBeInTheDocument()
    expect(screen.getByText('1283')).toBeInTheDocument()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('names where the content came from', () => {
    renderTile({ contentSource: 'Upload' })

    expect(screen.getByLabelText('Uploaded')).toBeInTheDocument()
  })

  it('flags a file whose Drive draft is ahead of Live', () => {
    renderTile({ hasNewerDraft: true, state: 'Imported' })

    expect(screen.getByText('Newer draft')).toBeInTheDocument()
  })
})
