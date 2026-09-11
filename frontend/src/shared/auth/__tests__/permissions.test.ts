import { describe, expect, it } from 'vitest'
import { Permissions, hasPermission } from '../permissions'

describe('hasPermission', () => {
  it('allows anything when the route names no permission', () => {
    expect(hasPermission([])).toBe(true)
  })

  it('allows a granted permission', () => {
    expect(hasPermission([Permissions.reportsView], Permissions.reportsView)).toBe(true)
  })

  it('denies a permission the user does not hold', () => {
    expect(hasPermission([Permissions.reportsView], Permissions.usersManage)).toBe(false)
  })

  it('matches the backend permission strings exactly', () => {
    // Dip.Application/Authorization/Permissions.cs — a typo here silently hides a page.
    expect(Object.values(Permissions)).toEqual(
      expect.arrayContaining([
        'users.manage', 'roles.manage', 'system.settings', 'project.settings',
        'import.run', 'files.manage', 'documents.edit',
        'reports.view', 'reports.export', 'lists.manage', 'baseline.manage',
      ]),
    )
  })
})
