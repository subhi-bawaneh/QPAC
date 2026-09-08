import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { DashboardPage } from '../DashboardPage'
import { collectCompanies } from '../companies'
import { AuthContext, type AuthContextValue } from '@/shared/auth/authContext'
import { Permissions } from '@/shared/auth/permissions'
import type { FolderTreeNode, UserSummary } from '@/shared/api/types'

const group = (name: string, total: number, submitted: number, approved: number) => ({
  name, total, planned: total, submitted, approved, qualityApproved: approved, rejected: 0,
  underReview: submitted - approved, withdrawn: 0, totalRevisions: submitted, quality: 1,
  pvPending: 0, pvSub1: 0, pvSub2: 0, pvApproved: 0, plannedPercent: 0.5,
  evPending: 0, evSub1: 0, evSub2: 0, evApproved: 0, completedPercent: 0.25,
})

vi.mock('@/shared/api/client', async () => {
  const actual = await vi.importActual<typeof import('@/shared/api/client')>('@/shared/api/client')
  const get = vi.fn(async (url: string) => {
    if (url.endsWith('/summaries/corporate')) {
      return {
        data: {
          recalculationRequired: false,
          documentsWithoutSnapshot: 0,
          summary: {
            reportDate: '2026-09-08', currentWeek: '2026-09-07', startWeek: '2025-11-03', endWeek: '2027-01-04',
            weeks: [],
            disciplines: [group('Structural', 100, 40, 20)],
            authors: [group('AFCO', 60, 30, 10)],
            total: group('Total', 160, 70, 30),
          },
        },
      }
    }
    if (url.endsWith('/summaries/baseline')) {
      return {
        data: {
          recalculationRequired: false, documentsWithoutSnapshot: 0,
          summary: {
            total: { name: 'Total', total: 0, submitted: 0, approved: 0, cRevise: 0, dRejected: 0, underReview: 0 },
            disciplines: [], packages: [],
            packageStatuses: [{ status: 'Submitted', packages: 3, drawings: 12 }],
            totalPackages: 3, totalPackageDrawings: 12,
          },
        },
      }
    }
    if (url.endsWith('/summaries/evm')) {
      return {
        data: {
          recalculationRequired: false, documentsWithoutSnapshot: 0,
          summary: {
            reportDate: '2026-09-08',
            total: { name: 'Total', documents: 160, plannedValue: 10, earnedValue: 9, budgetAtCompletion: 100, schedulePerformanceIndex: 0.9, scheduleVariance: -1, plannedPercent: 0.1, earnedPercent: 0.09, costPerformanceIndex: null },
            disciplines: [],
          },
        },
      }
    }
    if (url.endsWith('/control-findings')) {
      return { data: { recalculationRequired: false, findings: { deliveredButUnplanned: [{}, {}], unplanned: [], unusedPackages: [{}], duplicates: [] } } }
    }
    if (url.endsWith('/drive/status')) {
      return { data: { isRunning: false, lastRunStartedAt: null, lastRunFinishedAt: '2026-09-08T10:00:00Z', lastRunError: null, nextRunAt: null, queuedImports: 2 } }
    }
    if (url.endsWith('/imports')) {
      return { data: [{ id: 'b1', projectId: 'p', kind: 'Tidp', target: 'Draft', folderFileId: null, fileName: 'TIDP-STL.xlsx', importedAt: '2026-09-08', importedBy: 'worker', rowsRead: 1283, rowsInserted: 1283, rowsUpdated: 0, rowsSkipped: 0, completed: true, log: null }] }
    }
    if (url.endsWith('/folders/tree')) {
      return { data: [] }
    }
    return { data: null }
  })
  return { ...actual, api: { get, post: vi.fn() } }
})

// Recharts measures its container, which jsdom reports as 0x0.
vi.mock('@/features/reports/SCurveChart', () => ({ SCurveChart: () => <div data-testid="s-curve" /> }))
vi.mock('@/features/reports/SpiChart', () => ({ SpiChart: () => <div data-testid="spi" /> }))
vi.mock('@/features/reports/ProgressChart', () => ({ ProgressChart: () => <div data-testid="progress" /> }))

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
  it('shows the tracker key figures once the corporate summary arrives', async () => {
    renderDashboard()

    expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeInTheDocument()
    const figures = await screen.findByRole('region', { name: 'Key figures' })
    expect(figures).toHaveTextContent('160')
    expect(figures).toHaveTextContent('70')
    expect(figures).toHaveTextContent('0.90')
  })

  it('links to the detailed pages and counts the control findings', async () => {
    renderDashboard()

    expect(await screen.findByRole('link', { name: /summary/i })).toHaveAttribute('href', '/summary')
    expect(screen.getByRole('link', { name: /open/i })).toHaveAttribute('href', '/findings')
    expect(screen.getByRole('link', { name: /tidps/i })).toHaveAttribute('href', '/tidps')
    expect(await screen.findByText('Delivered but unplanned')).toBeInTheDocument()
    expect(screen.getByText('Total').nextSibling).toHaveTextContent('3')
  })

  it('reports the pipeline state and the recent imports', async () => {
    renderDashboard()

    expect(await screen.findByText('Drive in sync')).toBeInTheDocument()
    expect(await screen.findByText('TIDP-STL.xlsx')).toBeInTheDocument()
  })
})

describe('collectCompanies', () => {
  it('finds company folders at any depth', () => {
    const node = (name: string, isCompany: boolean, children: FolderTreeNode[] = []): FolderTreeNode => ({
      id: name, parentId: null, name, path: name, target: 'Live', isCompany, authorName: null,
      fileCount: 0, hasNewerDraft: false, children,
    })
    const tree = [node('root', false, [node('TIDPs', false, [node('AFCO', true), node('NAP', true, [node('ST', false)])])])]

    expect(collectCompanies(tree).map((c) => c.name)).toEqual(['AFCO', 'NAP'])
  })
})
