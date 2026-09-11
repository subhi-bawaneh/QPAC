/** Mirrors Dip.Application/Authorization/Permissions.cs. */
export const Permissions = {
  usersManage: 'users.manage',
  rolesManage: 'roles.manage',
  systemSettings: 'system.settings',
  projectSettings: 'project.settings',
  importRun: 'import.run',
  // Upload, replace and delete a source file. The super admin's alone.
  filesManage: 'files.manage',
  documentsEdit: 'documents.edit',
  reportsView: 'reports.view',
  reportsExport: 'reports.export',
  listsManage: 'lists.manage',
  baselineManage: 'baseline.manage',
} as const

export type Permission = (typeof Permissions)[keyof typeof Permissions]

export function hasPermission(granted: readonly string[], required?: Permission) {
  if (!required) return true
  return granted.includes(required)
}
