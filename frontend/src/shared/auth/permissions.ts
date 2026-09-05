/** Mirrors Dip.Application/Authorization/Permissions.cs. */
export const Permissions = {
  usersManage: 'users.manage',
  rolesManage: 'roles.manage',
  systemSettings: 'system.settings',
  projectSettings: 'project.settings',
  foldersManage: 'folders.manage',
  foldersAssignTarget: 'folders.assignTarget',
  driveSync: 'drive.sync',
  importRun: 'import.run',
  draftsEdit: 'drafts.edit',
  draftsPromote: 'drafts.promote',
  documentsEditLive: 'documents.editLive',
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
