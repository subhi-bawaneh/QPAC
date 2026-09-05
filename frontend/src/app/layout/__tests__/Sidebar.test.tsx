import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { Sidebar } from '../Sidebar'
import { AuthContext, type AuthContextValue } from '@/shared/auth/authContext'
import { Permissions } from '@/shared/auth/permissions'
import type { UserSummary } from '@/shared/api/types'

function renderSidebar(permissions: string[]) {
  const user: UserSummary = {
    id: '1', email: 'viewer@dip.test', fullName: 'Test Viewer',
    roles: ['Viewer'], permissions, disciplineCodes: [],
  }
  const value: AuthContextValue = {
    user,
    isAuthenticated: true,
    isRestoring: false,
    login: async () => undefined,
    logout: async () => undefined,
    can: (permission) => !permission || permissions.includes(permission),
  }

  render(
    <MemoryRouter>
      <AuthContext.Provider value={value}>
        <Sidebar />
      </AuthContext.Provider>
    </MemoryRouter>,
  )
}

describe('Sidebar', () => {
  it('renders only the entries the user may see', () => {
    renderSidebar([Permissions.reportsView])

    expect(screen.getByRole('link', { name: /dashboard/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /tracker/i })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /users/i })).not.toBeInTheDocument()
  })

  it('gives an admin the admin entries', () => {
    renderSidebar([Permissions.reportsView, Permissions.usersManage, Permissions.projectSettings])

    expect(screen.getByRole('link', { name: /users/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /settings/i })).toBeInTheDocument()
  })
})
