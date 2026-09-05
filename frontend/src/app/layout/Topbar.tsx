import { LogOut, Moon, Sun } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Button } from '@/shared/ui/button'
import { useAuth } from '@/shared/auth/useAuth'

export function Topbar() {
  const { user, logout } = useAuth()
  const [dark, setDark] = useState(() => document.documentElement.classList.contains('dark'))

  useEffect(() => {
    document.documentElement.classList.toggle('dark', dark)
  }, [dark])

  return (
    <header className="flex h-14 items-center justify-between border-b border-border bg-card px-6">
      <div className="text-sm text-muted-foreground">
        {user?.roles.length ? user.roles.join(' · ') : 'Signed in'}
      </div>

      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="sm"
          onClick={() => setDark((value) => !value)}
          aria-label={dark ? 'Switch to light theme' : 'Switch to dark theme'}
        >
          {dark ? <Sun className="h-4 w-4" aria-hidden /> : <Moon className="h-4 w-4" aria-hidden />}
        </Button>

        <span className="text-sm">{user?.fullName || user?.email}</span>

        <Button variant="outline" size="sm" onClick={() => void logout()}>
          <LogOut className="h-4 w-4" aria-hidden />
          Sign out
        </Button>
      </div>
    </header>
  )
}
