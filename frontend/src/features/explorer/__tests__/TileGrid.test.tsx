import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TileGrid } from '../TileGrid'
import type { FolderFileSummary, FolderNode } from '@/shared/api/types'

const folder = (name: string, overrides: Partial<FolderNode> = {}): FolderNode => ({
  id: `folder-${name}`,
  parentId: null,
  name,
  path: name,
  target: 'Live',
  driveFolderId: null,
  lastSyncedAt: null,
  isCompany: false,
  authorId: null,
  authorName: null,
  childCount: 0,
  fileCount: 2,
  hasNewerDraft: false,
  ...overrides,
})

const file = (name: string, overrides: Partial<FolderFileSummary> = {}): FolderFileSummary => ({
  id: `file-${name}`,
  name,
  kind: 'Tidp',
  contentSource: 'Drive',
  contentModifiedAt: '2026-09-01T10:00:00',
  driveFileId: 'drive-1',
  driveModifiedAt: '2026-09-01T10:00:00',
  sizeBytes: 1024,
  state: 'Imported',
  importError: null,
  lastImportedAt: '2026-09-01T10:05:00',
  rowCount: 12,
  hasNewerDraft: false,
  effectiveLayer: 'Live',
  ...overrides,
})

function renderGrid(props: Partial<Parameters<typeof TileGrid>[0]> = {}) {
  const onOpen = vi.fn()
  const onMenu = vi.fn()
  const onSelect = vi.fn()

  render(
    <TileGrid
      folders={[folder('Structural'), folder('Electrical')]}
      files={[file('TIDP-STL.xlsx'), file('TIDP-ELE.xlsx')]}
      importingFileIds={new Set()}
      selection={null}
      canUpload
      onSelect={onSelect}
      onOpen={onOpen}
      onMenu={onMenu}
      onDropFiles={vi.fn()}
      {...props}
    />,
  )

  return { onOpen, onMenu, onSelect }
}

describe('TileGrid', () => {
  it('lists folders before files', () => {
    renderGrid()

    const labels = screen.getAllByRole('gridcell').map((tile) => tile.getAttribute('aria-label'))
    expect(labels).toEqual([
      'Folder Structural',
      'Folder Electrical',
      'File TIDP-STL.xlsx',
      'File TIDP-ELE.xlsx',
    ])
  })

  it('opens an item on double click', async () => {
    const user = userEvent.setup()
    const { onOpen } = renderGrid()

    await user.dblClick(screen.getByRole('gridcell', { name: 'Folder Structural' }))

    expect(onOpen).toHaveBeenCalledWith(
      expect.objectContaining({ kind: 'folder' }),
    )
  })

  it('raises the context menu with the item that was right-clicked', async () => {
    const user = userEvent.setup()
    const { onMenu } = renderGrid()

    await user.pointer({
      keys: '[MouseRight]',
      target: screen.getByRole('gridcell', { name: 'File TIDP-STL.xlsx' }),
    })

    expect(onMenu).toHaveBeenCalledWith(
      expect.objectContaining({ kind: 'file' }),
      expect.anything(),
    )
  })

  it('moves the selection with the arrow keys', async () => {
    const user = userEvent.setup()
    const { onSelect } = renderGrid({ selection: { kind: 'folder', id: 'folder-Structural' } })

    screen.getByRole('grid').focus()
    await user.keyboard('{ArrowRight}')

    expect(onSelect).toHaveBeenCalledWith({ kind: 'folder', id: 'folder-Electrical' })
  })

  it('says the folder is empty and invites a drop when uploads are allowed', () => {
    renderGrid({ folders: [], files: [] })

    expect(screen.getByText('This folder is empty')).toBeInTheDocument()
    expect(screen.getByText(/drop a workbook here/i)).toBeInTheDocument()
  })

  it('does not invite a drop without the permission', () => {
    renderGrid({ folders: [], files: [], canUpload: false })

    expect(screen.queryByText(/drop a workbook here/i)).not.toBeInTheDocument()
  })
})
