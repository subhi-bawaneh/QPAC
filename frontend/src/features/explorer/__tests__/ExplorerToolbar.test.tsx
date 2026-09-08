import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ExplorerToolbar, type ToolbarPermissions } from '../ExplorerToolbar'
import type { FolderNode } from '@/shared/api/types'

const folder = (overrides: Partial<FolderNode> = {}): FolderNode => ({
  id: 'folder-1',
  parentId: null,
  name: 'Structural',
  path: 'Structural',
  target: 'Draft',
  driveFolderId: null,
  lastSyncedAt: null,
  isCompany: false,
  authorId: null,
  authorName: null,
  childCount: 0,
  fileCount: 1,
  hasNewerDraft: false,
  ...overrides,
})

const all: ToolbarPermissions = {
  canManage: true, canAssignTarget: true, canPromote: true, canSync: true,
}

function renderToolbar(
  permissions: Partial<ToolbarPermissions> = {},
  folderOverrides: Partial<FolderNode> = {},
) {
  render(
    <ExplorerToolbar
      folder={folder(folderOverrides)}
      breadcrumb={[{ id: 'folder-1', name: 'Structural' }]}
      permissions={{ ...all, ...permissions }}
      driveStatus={{
        isRunning: false,
        lastRunStartedAt: null,
        lastRunFinishedAt: null,
        lastRunError: null,
        nextRunAt: null,
        queuedImports: 0,
      }}
      syncing={false}
      onNavigate={vi.fn()}
      onUpload={vi.fn()}
      onSync={vi.fn()}
      onNewFolder={vi.fn()}
      onSetTarget={vi.fn()}
      onCompany={vi.fn()}
      onConvert={vi.fn()}
    />,
  )
}

describe('ExplorerToolbar', () => {
  it('offers Convert only with both permissions', () => {
    renderToolbar()
    expect(screen.getByRole('button', { name: /convert to live/i })).toBeInTheDocument()
  })

  it('hides Convert without drafts.promote', () => {
    renderToolbar({ canPromote: false })
    expect(screen.queryByRole('button', { name: /convert to live/i })).not.toBeInTheDocument()
  })

  it('hides Convert without folders.assignTarget', () => {
    renderToolbar({ canAssignTarget: false })
    expect(screen.queryByRole('button', { name: /convert to live/i })).not.toBeInTheDocument()
  })

  it('hides Convert for a folder that is already Live', () => {
    renderToolbar({}, { target: 'Live' })
    expect(screen.queryByRole('button', { name: /convert to live/i })).not.toBeInTheDocument()
  })

  it('hides New folder inside a Drive folder', () => {
    renderToolbar({}, { driveFolderId: 'drive-123' })
    expect(screen.queryByRole('button', { name: /new folder/i })).not.toBeInTheDocument()
  })

  it('offers New folder in a manual folder', () => {
    renderToolbar()
    expect(screen.getByRole('button', { name: /new folder/i })).toBeInTheDocument()
  })

  it('hides Sync now without drive.sync', () => {
    renderToolbar({ canSync: false })
    expect(screen.queryByRole('button', { name: /sync now/i })).not.toBeInTheDocument()
  })

  it('reports the Drive state in a pill', () => {
    renderToolbar()
    expect(screen.getByText('Drive in sync')).toBeInTheDocument()
  })
})
