import { LogOut } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { useAuth } from '@/shared/auth/useAuth'
import { ThemeMenu } from './ThemeMenu'

export function Topbar() {
  const { user, logout } = useAuth()

  return (
    <header className="flex h-14 items-center justify-between border-b border-border bg-card px-6">
      <div className="text-sm text-muted-foreground">
        {user?.roles.length ? user.roles.join(' · ') : 'Signed in'}
      </div>

      <div className="flex items-center gap-3">
        <ThemeMenu />

        <span className="text-sm">{user?.fullName || user?.email}</span>

        <Button variant="outline" size="sm" onClick={() => void logout()}>
          <LogOut className="h-4 w-4" aria-hidden />
          Sign out
        </Button>
      </div>
    </header>
  )
}
