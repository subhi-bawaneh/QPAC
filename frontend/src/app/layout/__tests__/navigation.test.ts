import { describe, expect, it } from 'vitest'
import { navigation, visibleNavigation } from '../navigation'
import { Permissions } from '@/shared/auth/permissions'

describe('visibleNavigation', () => {
  it('shows nothing to a user with no permissions', () => {
    // The Dashboard is the landing page and needs reports.view like every report
    // it now contains (decision D8).
    expect(visibleNavigation([])).toEqual([])
  })

  it('hides every entry the user has no permission for', () => {
    const labels = visibleNavigation([Permissions.reportsView]).map((item) => item.label)

    expect(labels).toContain('Dashboard')
    expect(labels).toContain('Tracker')
    expect(labels).not.toContain('Users')
    expect(labels).not.toContain('Baseline')
    expect(labels).not.toContain('Control Findings')
    expect(labels).not.toContain('Summary')
  })

  it('shows everything to a user holding every permission', () => {
    const all = Object.values(Permissions)

    expect(visibleNavigation(all)).toHaveLength(navigation.length)
  })
})
