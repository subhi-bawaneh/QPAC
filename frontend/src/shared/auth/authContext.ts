import { createContext } from 'react'
import type { UserSummary } from '@/shared/api/types'
import type { Permission } from './permissions'

export interface AuthContextValue {
  user: UserSummary | null
  isAuthenticated: boolean
  isRestoring: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
  can: (permission?: Permission) => boolean
}

// Kept apart from the provider so the provider file exports only components,
// which is what keeps Fast Refresh working.
export const AuthContext = createContext<AuthContextValue | null>(null)
