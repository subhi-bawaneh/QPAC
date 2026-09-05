import { Route, Routes } from 'react-router-dom'
import { AppLayout } from './layout/AppLayout'
import { RequireAuth, RequirePermission } from '@/shared/auth/RequireAuth'
import { Permissions } from '@/shared/auth/permissions'
import { LoginPage } from '@/features/auth/LoginPage'
import { DashboardPage } from '@/features/dashboard/DashboardPage'
import { PlaceholderPage } from '@/features/placeholder/PlaceholderPage'
import { FolderExplorerPage } from '@/features/folders/FolderExplorerPage'

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route index element={<DashboardPage />} />

          <Route element={<RequirePermission permission={Permissions.reportsView} />}>
            <Route path="tidps" element={<FolderExplorerPage />} />
            <Route
              path="midp"
              element={<PlaceholderPage title="MIDP" phase="6.4" description="The master document grid with filters and export." />}
            />
            <Route
              path="tracker"
              element={<PlaceholderPage title="Tracker" phase="6.4" description="Server-paged tracker with a document drawer showing every revision." />}
            />
            <Route
              path="summary"
              element={<PlaceholderPage title="Summary" phase="6.5" description="Dashboard, Corporate Summary, Baseline Summary and EVM — the engines are done and verified against Tracker.xlsx." />}
            />
            <Route
              path="findings"
              element={<PlaceholderPage title="Control Findings" phase="6.5" description="The four anomaly reports, with export." />}
            />
          </Route>

          <Route element={<RequirePermission permission={Permissions.baselineManage} />}>
            <Route
              path="baseline"
              element={<PlaceholderPage title="Baseline" phase="6.4" description="Baseline activities, import and replace, used/unused packages." />}
            />
          </Route>

          <Route element={<RequirePermission permission={Permissions.listsManage} />}>
            <Route
              path="lists"
              element={<PlaceholderPage title="Lists" phase="6.4" description="Picklists and the Aconex status mapping." />}
            />
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
