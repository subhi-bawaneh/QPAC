import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { SummaryPage } from '../SummaryPage'
import { AuthContext, type AuthContextValue } from '@/shared/auth/authContext'
import { Permissions } from '@/shared/auth/permissions'
import type { UserSummary } from '@/shared/api/types'

vi.mock('@/shared/api/client', async () => {
  const actual = await vi.importActual<typeof import('@/shared/api/client')>('@/shared/api/client')
  return {
    ...actual,
    api: { get: vi.fn().mockResolvedValue({ data: null }), post: vi.fn() },
  }
})

// Recharts measures its container, which jsdom reports as 0x0.
vi.mock('@/features/reports/SCurveChart', () => ({ SCurveChart: () => <div data-testid="s-curve" /> }))
vi.mock('@/features/reports/SpiChart', () => ({ SpiChart: () => <div data-testid="spi" /> }))

function renderSummary() {
  const user: UserSummary = {
    id: '1', email: 'viewer@dip.test', fullName: 'Viewer',
    roles: ['Viewer'], permissions: [Permissions.reportsView], disciplineCodes: [],
  }
  const auth: AuthContextValue = {
    user,
    isAuthenticated: true,
    isRestoring: false,
    login: async () => undefined,
    logout: async () => undefined,
    can: (permission) => !permission || user.permissions.includes(permission),
  }

  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter>
        <AuthContext.Provider value={auth}>
          <SummaryPage />
        </AuthContext.Provider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('SummaryPage', () => {
  it('carries the four report tabs', () => {
    renderSummary()

    expect(screen.getByRole('heading', { name: 'Summary' })).toBeInTheDocument()
    expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual([
      'Overview', 'Corporate', 'Baseline', 'EVM',
    ])
  })

  it('opens on the overview tab', () => {
    renderSummary()

    expect(screen.getByRole('tab', { name: 'Overview' })).toHaveAttribute('aria-selected', 'true')
  })
})
