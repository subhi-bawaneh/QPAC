import {
  FolderTree, LayoutDashboard, ListChecks, Table2, FileSpreadsheet,
  CalendarRange, Settings, Users,
  type LucideIcon,
} from 'lucide-react'
import { Permissions, type Permission } from '@/shared/auth/permissions'

export interface NavItem {
  to: string
  label: string
  icon: LucideIcon
  /** Hidden entirely when the signed-in user lacks this (PLAN.md § 8). */
  permission?: Permission
}

// Control Findings folded into the Dashboard's tabs (decision D8), so the sidebar
// is one item shorter and the reports sit together.
export const navigation: NavItem[] = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, permission: Permissions.reportsView },
  { to: '/tidps', label: 'TIDPs', icon: FolderTree, permission: Permissions.reportsView },
  { to: '/midp', label: 'MIDP', icon: FileSpreadsheet, permission: Permissions.reportsView },
  { to: '/baseline', label: 'Baseline', icon: CalendarRange, permission: Permissions.baselineManage },
  { to: '/tracker', label: 'Tracker', icon: Table2, permission: Permissions.reportsView },
  { to: '/lists', label: 'Lists', icon: ListChecks, permission: Permissions.listsManage },
  { to: '/admin/users', label: 'Users', icon: Users, permission: Permissions.usersManage },
  { to: '/admin/settings', label: 'Settings', icon: Settings, permission: Permissions.projectSettings },
]

export function visibleNavigation(permissions: readonly string[]): NavItem[] {
  return navigation.filter((item) => !item.permission || permissions.includes(item.permission))
}
