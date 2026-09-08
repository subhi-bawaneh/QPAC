import { NavLink } from 'react-router-dom'
import { cn } from '@/shared/lib/utils'
import { useAuth } from '@/shared/auth/useAuth'
import { visibleNavigation } from './navigation'

export function Sidebar() {
  const { user } = useAuth()
  const items = visibleNavigation(user?.permissions ?? [])

  return (
    <nav aria-label="Main" className="flex h-full w-56 shrink-0 flex-col border-r border-border bg-card">
      <div className="border-b border-border px-5 py-4">
        <p className="text-sm font-semibold">DIP</p>
        <p className="text-xs text-muted-foreground">Qiddiya Performing Arts Center</p>
      </div>

      <ul className="flex-1 space-y-1 overflow-y-auto p-3">
        {items.map((item) => (
          <li key={item.to}>
            <NavLink
              to={item.to}
              end={item.to === '/'}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-3 rounded-md px-3 py-2 text-sm transition',
                  isActive ? 'bg-primary text-primary-foreground' : 'text-foreground hover:bg-muted',
                )
              }
            >
              <item.icon className="h-4 w-4 shrink-0" aria-hidden />
              <span className="flex-1">{item.label}</span>
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  )
}
