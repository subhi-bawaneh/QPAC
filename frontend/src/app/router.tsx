import { Suspense, lazy } from 'react'
import { Route, Routes } from 'react-router-dom'
import { AppLayout } from './layout/AppLayout'
import { RequireAuth, RequirePermission } from '@/shared/auth/RequireAuth'
import { Permissions } from '@/shared/auth/permissions'
import { LoginPage } from '@/features/auth/LoginPage'
import { DashboardPage } from '@/features/dashboard/DashboardPage'
import { PlaceholderPage } from '@/features/placeholder/PlaceholderPage'
import { FolderExplorerPage } from '@/features/folders/FolderExplorerPage'
import { DraftReviewPage } from '@/features/drafts/DraftReviewPage'
import { TrackerPage } from '@/features/tracker/TrackerPage'
import { MidpPage } from '@/features/midp/MidpPage'
import { BaselinePage } from '@/features/baseline/BaselinePage'
import { ListsPage } from '@/features/lists/ListsPage'
import { ControlFindingsPage } from '@/features/findings/ControlFindingsPage'
import { Spinner } from '@/shared/ui/spinner'

// Recharts is ~450 kB of the bundle and only the Summary page needs it, so that
// route is split out: signing in no longer downloads a charting library.
const SummaryPage = lazy(async () => ({
  default: (await import('@/features/summary/SummaryPage')).SummaryPage,
}))

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route index element={<DashboardPage />} />

          <Route element={<RequirePermission permission={Permissions.reportsView} />}>
            <Route path="tidps" element={<FolderExplorerPage />} />
            <Route path="drafts/:folderFileId" element={<DraftReviewPage />} />
            <Route path="midp" element={<MidpPage />} />
            <Route path="tracker" element={<TrackerPage />} />
            <Route
              path="summary"
              element={
                <Suspense fallback={<Spinner label="Loading charts…" />}>
                  <SummaryPage />
                </Suspense>
              }
            />
            <Route path="findings" element={<ControlFindingsPage />} />
          </Route>

          <Route element={<RequirePermission permission={Permissions.baselineManage} />}>
            <Route path="baseline" element={<BaselinePage />} />
          </Route>

          <Route element={<RequirePermission permission={Permissions.listsManage} />}>
            <Route path="lists" element={<ListsPage />} />
          </Route>

          <Route element={<RequirePermission permission={Permissions.usersManage} />}>
            <Route
              path="admin/users"
              element={<PlaceholderPage title="Users" phase="6.6" description="Accounts, roles and discipline scoping." />}
            />
          </Route>

          <Route element={<RequirePermission permission={Permissions.projectSettings} />}>
            <Route
              path="admin/settings"
              element={<PlaceholderPage title="Settings" phase="6.6" description="Project settings: schedule mode, report date and progress weights." />}
            />
          </Route>

          <Route path="*" element={<PlaceholderPage title="Not found" phase="—" description="That page does not exist." />} />
        </Route>
      </Route>
    </Routes>
  )
}
