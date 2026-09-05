import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { PromoteDialog } from '../PromoteDialog'
import type { PromoteDiff } from '@/shared/api/types'

const get = vi.fn()
const post = vi.fn()

vi.mock('@/shared/api/client', async () => {
  const actual = await vi.importActual<typeof import('@/shared/api/client')>('@/shared/api/client')
  return {
    ...actual,
    api: {
      get: (...args: unknown[]) => get(...args),
      post: (...args: unknown[]) => post(...args),
    },
  }
})

const diff: PromoteDiff = {
  folderFileId: 'file-1',
  importBatchId: 'batch-1',
  importedAt: '2026-09-05T08:00:00',
  added: 2,
  modified: 1,
  unchanged: 40,
  deleted: 3,
  conflicts: 1,
  maxRows: 200,
  addedRows: [
    { documentNumber: 'DOC-A', draftId: 'd1', liveId: null, title: 'First', changes: [], reason: null },
    { documentNumber: 'DOC-B', draftId: 'd2', liveId: null, title: 'Second', changes: [], reason: null },
  ],
  modifiedRows: [
    {
      documentNumber: 'DOC-C',
      draftId: 'd3',
      liveId: 'l3',
      title: 'Third',
      changes: [{ field: 'Title', oldValue: 'OLD TITLE', newValue: 'NEW TITLE' }],
      reason: null,
    },
  ],
  deletedRows: [
    { documentNumber: 'DOC-D', draftId: null, liveId: 'l4', title: 'Fourth', changes: [], reason: null },
  ],
  conflictRows: [
    {
      documentNumber: 'DOC-E',
      draftId: 'd5',
      liveId: 'l5',
      title: 'Fifth',
      changes: [],
      reason: 'A Live document with this number came from another file',
    },
  ],
}

function renderDialog() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )

  return render(<PromoteDialog open onClose={vi.fn()} folderFileId="file-1" />, { wrapper })
}

beforeEach(() => {
  get.mockReset()
  post.mockReset()
  get.mockResolvedValue({ data: diff })
  post.mockResolvedValue({
    data: {
      promoteBatchId: 'promote-1',
      folderFileId: 'file-1',
      added: 2,
      updated: 1,
      deleted: 0,
      skipped: 40,
      conflicts: 1,
      recalculationRequired: true,
    },
  })
})

describe('PromoteDialog', () => {
  it('counts each bucket of the diff on its tab', async () => {
    renderDialog()

    expect(await screen.findByRole('tab', { name: /added 2/i })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: /modified 1/i })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: /deleted 3/i })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: /conflicts 1/i })).toBeInTheDocument()
  })

  it('shows the old and new value of every changed field', async () => {
    const user = userEvent.setup()
    renderDialog()

    await user.click(await screen.findByRole('tab', { name: /modified/i }))

    expect(screen.getByText('Title')).toBeInTheDocument()
    expect(screen.getByText('OLD TITLE')).toBeInTheDocument()
    expect(screen.getByText('NEW TITLE')).toBeInTheDocument()
  })

  // A conflict is a row that will NOT be written; the reason is the whole point.
  it('explains each conflict and warns that those rows are skipped', async () => {
    const user = userEvent.setup()
    renderDialog()

    await user.click(await screen.findByRole('tab', { name: /conflicts/i }))

    expect(screen.getByText(/came from another file/i)).toBeInTheDocument()
    expect(screen.getByText(/will be skipped/i)).toBeInTheDocument()
  })

  it('offers deleteMissing only because this diff has rows to delete', async () => {
    renderDialog()

    expect(await screen.findByRole('checkbox')).not.toBeChecked()
    expect(screen.getByText(/also delete the 3 live row/i)).toBeInTheDocument()
  })

  it('hides deleteMissing when nothing would be deleted', async () => {
    get.mockResolvedValue({ data: { ...diff, deleted: 0, deletedRows: [] } })
    renderDialog()

    await screen.findByRole('tab', { name: /added 2/i })
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
  })

  // Promoting is the write; deleteMissing must travel exactly as the user set it.
  it('promotes with deleteMissing off by default and reports what happened', async () => {
    const user = userEvent.setup()
    renderDialog()

    await user.click(await screen.findByRole('button', { name: /^promote$/i }))

    await waitFor(() => expect(post).toHaveBeenCalledWith('/api/drafts/promote', {
      folderFileId: 'file-1',
      deleteMissing: false,
    }))

    expect(await screen.findByText('Promoted')).toBeInTheDocument()
    expect(screen.getByText(/reports are stale/i)).toBeInTheDocument()
  })

  it('sends deleteMissing when the user ticks it', async () => {
    const user = userEvent.setup()
    renderDialog()

    await user.click(await screen.findByRole('checkbox'))
    await user.click(screen.getByRole('button', { name: /^promote$/i }))

    await waitFor(() => expect(post).toHaveBeenCalledWith('/api/drafts/promote', {
      folderFileId: 'file-1',
      deleteMissing: true,
    }))
  })

  it('offers a rollback of the promote it just made', async () => {
    const user = userEvent.setup()
    renderDialog()

    await user.click(await screen.findByRole('button', { name: /^promote$/i }))
    const rollback = await screen.findByRole('button', { name: /roll back/i })

    post.mockResolvedValueOnce({
      data: { promoteBatchId: 'promote-1', removed: 2, restored: 1, reinserted: 0, recalculationRequired: true },
    })
    await user.click(rollback)

    await waitFor(() =>
      expect(post).toHaveBeenCalledWith('/api/drafts/promote/promote-1/rollback'))
    expect(await screen.findByText('Promote rolled back')).toBeInTheDocument()
  })
})
