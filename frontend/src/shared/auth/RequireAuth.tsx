import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from './useAuth'
import { Spinner } from '@/shared/ui/spinner'
import { hasPermission, type Permission } from './permissions'

export function RequireAuth() {
  const { isAuthenticated, isRestoring } = useAuth()
  const location = useLocation()

  if (isRestoring) return <Spinner label="Restoring your session…" />
  if (!isAuthenticated) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  return <Outlet />
}

/** Guards a route behind a single permission; renders a plain notice rather than a 403. */
export function RequirePermission({ permission }: { permission: Permission }) {
  const { user } = useAuth()

  if (!hasPermission(user?.permissions ?? [], permission)) {
    return (
      <div className="rounded-lg border border-border bg-card p-8 text-center">
        <h2 className="text-lg font-semibold">Not available to your account</h2>
        <p className="mt-2 text-sm text-muted-foreground">
          This page needs the <code className="font-mono">{permission}</code> permission.
        </p>
      </div>
    )
  }
  return <Outlet />
}
