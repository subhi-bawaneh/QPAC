import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ListsPage } from '../ListsPage'
import { picklistFields } from '../listLabels'
import { AuthContext, type AuthContextValue } from '@/shared/auth/authContext'
import { Permissions } from '@/shared/auth/permissions'
import type { UserSummary } from '@/shared/api/types'

const get = vi.fn()

vi.mock('@/shared/api/client', async () => {
  const actual = await vi.importActual<typeof import('@/shared/api/client')>('@/shared/api/client')
  return { ...actual, api: { get: (...args: unknown[]) => get(...args), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }
})

function renderPage(permissions: string[] = [Permissions.listsManage]) {
  const user: UserSummary = {
    id: '1', email: 'admin@dip.test', fullName: 'Admin',
    roles: ['Admin'], permissions, disciplineCodes: [],
  }
  const auth: AuthContextValue = {
    user,
    isAuthenticated: true,
    isRestoring: false,
    login: async () => undefined,
    logout: async () => undefined,
    can: (permission) => !permission || permissions.includes(permission),
  }

  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <AuthContext.Provider value={auth}>
        <ListsPage />
      </AuthContext.Provider>
    </QueryClientProvider>,
  )
}

describe('ListsPage', () => {
  it('has a tab for every list in the workbook plus the status mapping — 18', async () => {
    get.mockResolvedValue({ data: [] })
    renderPage()

    const tabs = await screen.findAllByRole('tab')
    expect(tabs).toHaveLength(picklistFields.length + 1)
    expect(picklistFields).toHaveLength(17)
    expect(tabs.at(-1)?.textContent).toContain('Status mapping')
  })

  it('names the lists the way the workbook does', async () => {
    get.mockResolvedValue({ data: [] })
    renderPage()

    const labels = (await screen.findAllByRole('tab')).map((tab) => tab.textContent)
    expect(labels.some((label) => label?.includes('Corporate discipline'))).toBe(true)
    expect(labels.some((label) => label?.includes('Author'))).toBe(true)
    expect(labels.some((label) => label?.includes('Classification'))).toBe(true)
  })
})
