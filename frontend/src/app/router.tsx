import { Suspense, lazy } from 'react'
import { Route, Routes } from 'react-router-dom'
import { AppLayout } from './layout/AppLayout'
import { RequireAuth, RequirePermission } from '@/shared/auth/RequireAuth'
import { Permissions } from '@/shared/auth/permissions'
import { LoginPage } from '@/features/auth/LoginPage'
import { PlaceholderPage } from '@/features/placeholder/PlaceholderPage'
import { ExplorerPage } from '@/features/explorer/ExplorerPage'
import { DraftReviewPage } from '@/features/drafts/DraftReviewPage'
import { TrackerPage } from '@/features/tracker/TrackerPage'
import { MidpPage } from '@/features/midp/MidpPage'
import { BaselinePage } from '@/features/baseline/BaselinePage'
import { ListsPage } from '@/features/lists/ListsPage'
import { UsersPage } from '@/features/admin/UsersPage'
import { SettingsPage } from '@/features/admin/SettingsPage'
import { Spinner } from '@/shared/ui/spinner'

// Recharts is ~450 kB of the bundle and only the Dashboard needs it, so the landing
// route is split out: signing in no longer downloads a charting library up front.
const DashboardPage = lazy(async () => ({
  default: (await import('@/features/dashboard/DashboardPage')).DashboardPage,
}))

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route element={<RequirePermission permission={Permissions.reportsView} />}>
            <Route
              index
              element={
                <Suspense fallback={<Spinner label="Loading charts…" />}>
                  <DashboardPage />
                </Suspense>
              }
            />
            <Route path="tidps" element={<ExplorerPage />} />
            <Route path="drafts/:folderFileId" element={<DraftReviewPage />} />
            <Route path="midp" element={<MidpPage />} />
            <Route path="tracker" element={<TrackerPage />} />
          </Route>

          <Route element={<RequirePermission permission={Permissions.baselineManage} />}>
            <Route path="baseline" element={<BaselinePage />} />
          </Route>

          <Route element={<RequirePermission permission={Permissions.listsManage} />}>
            <Route path="lists" element={<ListsPage />} />
          </Route>

          <Route element={<RequirePermission permission={Permissions.usersManage} />}>
            <Route path="admin/users" element={<UsersPage />} />
          </Route>

          <Route element={<RequirePermission permission={Permissions.projectSettings} />}>
            <Route path="admin/settings" element={<SettingsPage />} />
          </Route>

          <Route path="*" element={<PlaceholderPage title="Not found" phase="—" description="That page does not exist." />} />
        </Route>
      </Route>
    </Routes>
  )
}
