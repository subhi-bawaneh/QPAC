import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { DashboardPage } from '../DashboardPage'
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
vi.mock('../SCurveChart', () => ({ SCurveChart: () => <div data-testid="s-curve" /> }))
vi.mock('../SpiChart', () => ({ SpiChart: () => <div data-testid="spi" /> }))

function renderDashboard() {
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
          <DashboardPage />
        </AuthContext.Provider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('DashboardPage', () => {
  it('is the landing page and carries the five report tabs', () => {
    renderDashboard()

    expect(screen.getByRole('heading', { name: 'Dashboard' })).toBeInTheDocument()
    expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual([
      'Overview', 'Corporate', 'Baseline', 'Control Findings', 'EVM',
    ])
  })

  it('opens on the overview tab', () => {
    renderDashboard()

    expect(screen.getByRole('tab', { name: 'Overview' })).toHaveAttribute('aria-selected', 'true')
  })
})
