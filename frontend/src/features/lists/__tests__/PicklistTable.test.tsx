import { describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { PicklistTable } from '../PicklistTable'
import type { PicklistItem } from '@/shared/api/types'

const post = vi.fn()
const put = vi.fn()
const del = vi.fn()

vi.mock('@/shared/api/client', async () => {
  const actual = await vi.importActual<typeof import('@/shared/api/client')>('@/shared/api/client')
  return {
    ...actual,
    api: {
      get: vi.fn(),
      post: (...args: unknown[]) => post(...args),
      put: (...args: unknown[]) => put(...args),
      delete: (...args: unknown[]) => del(...args),
    },
  }
})

const item = (code: string, overrides: Partial<PicklistItem> = {}): PicklistItem => ({
  id: `id-${code}`,
  field: 'Author',
  code,
  description: `${code} description`,
  sortOrder: 1,
  isDeleted: false,
  deletedAt: null,
  ...overrides,
})

function renderTable(items: PicklistItem[], canManage = true, showDeleted = false) {
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <PicklistTable field="Author" items={items} canManage={canManage} showDeleted={showDeleted} />
    </QueryClientProvider>,
  )
}

describe('PicklistTable', () => {
  it('adds an item from the row at the top', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue({ data: {} })
    renderTable([item('AFCO')])

    await user.type(screen.getByLabelText(/new author code/i), 'ACME')
    await user.click(screen.getByRole('button', { name: /add item/i }))

    await waitFor(() => expect(post).toHaveBeenCalledWith(
      expect.stringContaining('/picklists'),
      expect.objectContaining({ field: 'Author', code: 'ACME' }),
    ))
  })

  it('edits a row in place', async () => {
    const user = userEvent.setup()
    put.mockResolvedValue({ data: {} })
    renderTable([item('AFCO')])

    await user.click(screen.getByRole('button', { name: /edit AFCO/i }))
    const codeInput = screen.getByLabelText('Code')
    await user.clear(codeInput)
    await user.type(codeInput, 'AFCO-2')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(put).toHaveBeenCalledWith(
      '/api/picklists/id-AFCO',
      expect.objectContaining({ code: 'AFCO-2' }),
    ))
  })

  it('confirms before deleting, and says what a delete means', async () => {
    const user = userEvent.setup()
    del.mockResolvedValue({ data: {} })
    renderTable([item('AFCO')])

    await user.click(screen.getByRole('button', { name: /delete AFCO/i }))
    expect(screen.getByText(/will not bring it back/i)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Delete' }))
    await waitFor(() => expect(del).toHaveBeenCalledWith('/api/picklists/id-AFCO'))
  })

  it('offers Restore on a deleted row when deleted rows are shown', () => {
    renderTable([item('TKE', { isDeleted: true, deletedAt: '2026-09-08T00:00:00' })], true, true)

    expect(screen.getByText('Deleted')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /restore/i })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /delete TKE/i })).not.toBeInTheDocument()
  })

  it('is read-only without lists.manage', () => {
    renderTable([item('AFCO')], false)

    expect(screen.queryByRole('button', { name: /add item/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /edit AFCO/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /delete AFCO/i })).not.toBeInTheDocument()
    expect(screen.getByText('AFCO')).toBeInTheDocument()
  })

  it('reorders with the arrow buttons', async () => {
    const user = userEvent.setup()
    put.mockResolvedValue({ data: {} })
    renderTable([item('AFCO'), item('DOKA', { sortOrder: 2 })])

    await user.click(screen.getByRole('button', { name: /move DOKA up/i }))

    await waitFor(() => expect(put).toHaveBeenCalledWith(
      expect.stringContaining('/picklists/Author/order'),
      { ids: ['id-DOKA', 'id-AFCO'] },
    ))
  })

  it('drops the description column for a single-column list', () => {
    render(
      <QueryClientProvider client={new QueryClient()}>
        <PicklistTable field="Scale" items={[]} canManage showDeleted={false} />
      </QueryClientProvider>,
    )

    expect(screen.queryByLabelText(/new scale description/i)).not.toBeInTheDocument()
    expect(screen.getByLabelText(/new scale code/i)).toBeInTheDocument()
  })
})
