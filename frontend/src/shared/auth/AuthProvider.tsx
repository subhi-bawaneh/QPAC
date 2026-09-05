import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, setSessionExpiredHandler } from '@/shared/api/client'
import type { AuthResult, UserSummary } from '@/shared/api/types'
import { tokenStorage } from './tokenStorage'
import { hasPermission, type Permission } from './permissions'
import { AuthContext, type AuthContextValue } from './authContext'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserSummary | null>(null)
  // Start "restoring" whenever a refresh token survived the reload: rendering the
  // login page first would flash it in front of an already-signed-in user.
  const [isRestoring, setIsRestoring] = useState(() => tokenStorage.getRefreshToken() !== null)

  const logout = useCallback(async () => {
    const refreshToken = tokenStorage.getRefreshToken()
    tokenStorage.clear()
    setUser(null)
    if (refreshToken) {
      // Best effort: the local session is already gone either way.
      await api.post('/api/auth/logout', { refreshToken }).catch(() => undefined)
    }
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const { data } = await api.post<AuthResult>('/api/auth/login', { email, password })
    tokenStorage.save(data)
    setUser(data.user)
  }, [])

  useEffect(() => {
    setSessionExpiredHandler(() => {
      tokenStorage.clear()
      setUser(null)
    })
    return () => setSessionExpiredHandler(null)
  }, [])

  // Restore a session from the stored refresh token on first load.
  useEffect(() => {
    let cancelled = false
    const refreshToken = tokenStorage.getRefreshToken()
    if (!refreshToken) return

    const cached = tokenStorage.getCachedUser()
    if (cached) setUser(cached)

    void (async () => {
      try {
        const { data } = await api.post<AuthResult>('/api/auth/refresh', { refreshToken })
        if (cancelled) return
        tokenStorage.save(data)
        setUser(data.user)
      } catch {
        if (!cancelled) {
          tokenStorage.clear()
          setUser(null)
        }
      } finally {
        if (!cancelled) setIsRestoring(false)
      }
    })()

    return () => {
      cancelled = true
    }
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: user !== null,
      isRestoring,
      login,
      logout,
      can: (permission?: Permission) => hasPermission(user?.permissions ?? [], permission),
    }),
    [user, isRestoring, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
