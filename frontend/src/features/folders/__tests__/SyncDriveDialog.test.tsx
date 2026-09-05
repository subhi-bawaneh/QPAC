import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { SyncDriveDialog } from '../SyncDriveDialog'

const post = vi.fn()

vi.mock('@/shared/api/client', async () => {
  const actual = await vi.importActual<typeof import('@/shared/api/client')>('@/shared/api/client')
  return { ...actual, api: { post: (...args: unknown[]) => post(...args) } }
})

const user = userEvent.setup({ pointerEventsCheck: 0 })

function renderDialog() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
  return render(<SyncDriveDialog open onClose={vi.fn()} projectId="p1" />, { wrapper })
}

beforeEach(() => post.mockReset())

describe('SyncDriveDialog', () => {
  // Sync is chunked: each response says which folder to visit next, and the browser
  // drives the loop until done.
  it('follows nextFolderDriveId until the server says done', async () => {
    post
      .mockResolvedValueOnce({ data: { foldersUpserted: 2, filesUpserted: 0, filesDownloaded: 0, nextFolderDriveId: 'f2', done: false } })
      .mockResolvedValueOnce({ data: { foldersUpserted: 3, filesUpserted: 5, filesDownloaded: 0, nextFolderDriveId: 'f3', done: false } })
      .mockResolvedValueOnce({ data: { foldersUpserted: 0, filesUpserted: 7, filesDownloaded: 0, nextFolderDriveId: null, done: true } })

    renderDialog()
    await user.click(screen.getByRole('button', { name: /start sync/i }))

    await waitFor(() => expect(screen.getByText('Sync complete.')).toBeInTheDocument())
    expect(post).toHaveBeenCalledTimes(3)
    expect(post).toHaveBeenNthCalledWith(1, '/api/projects/p1/drive/sync', { startFolderDriveId: null, downloadFiles: false })
    expect(post).toHaveBeenNthCalledWith(2, '/api/projects/p1/drive/sync', { startFolderDriveId: 'f2', downloadFiles: false })
    expect(screen.getByText(/5 folder\(s\), 12 file\(s\)/)).toBeInTheDocument()
  })

  it('sends downloadFiles when the box is ticked', async () => {
    post.mockResolvedValue({ data: { foldersUpserted: 1, filesUpserted: 1, filesDownloaded: 1, nextFolderDriveId: null, done: true } })

    renderDialog()
    await user.click(screen.getByRole('checkbox', { name: /download file contents/i }))
    await user.click(screen.getByRole('button', { name: /start sync/i }))

    await waitFor(() => expect(post).toHaveBeenCalledWith(
      '/api/projects/p1/drive/sync',
      { startFolderDriveId: null, downloadFiles: true },
    ))
  })

  // A step that comes back unusable must stop the loop and say so, rather than
  // spinning or silently reporting success.
  it('stops and shows an alert when a step comes back unusable', async () => {
    post.mockResolvedValue({ data: undefined })

    renderDialog()
    await user.click(screen.getByRole('button', { name: /start sync/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/sync failed/i)
    expect(screen.queryByText('Sync complete.')).not.toBeInTheDocument()
    expect(post).toHaveBeenCalledTimes(1)
  })

  it('defaults to metadata only, so a first sync is quick', () => {
    renderDialog()

    expect(screen.getByRole('checkbox', { name: /download file contents/i })).not.toBeChecked()
  })
})
