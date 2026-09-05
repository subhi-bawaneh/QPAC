import { describe, expect, it } from 'vitest'
import { navigation, visibleNavigation } from '../navigation'
import { Permissions } from '@/shared/auth/permissions'

describe('visibleNavigation', () => {
  it('always shows the dashboard, which needs no permission', () => {
    expect(visibleNavigation([]).map((item) => item.label)).toEqual(['Dashboard'])
  })

  it('hides every entry the user has no permission for', () => {
    const labels = visibleNavigation([Permissions.reportsView]).map((item) => item.label)

    expect(labels).toContain('Tracker')
    expect(labels).not.toContain('Users')
    expect(labels).not.toContain('Baseline')
  })

  it('shows everything to a user holding every permission', () => {
    const all = Object.values(Permissions)

    expect(visibleNavigation(all)).toHaveLength(navigation.length)
  })
})
