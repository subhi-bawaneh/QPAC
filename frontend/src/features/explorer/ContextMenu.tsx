import type { ReactNode } from 'react'
import { DropdownMenu, DropdownMenuContent, DropdownMenuTrigger } from '@/shared/ui/dropdown-menu'

export interface MenuAnchor {
  x: number
  y: number
}

// Right-click opens the existing DropdownMenu at the pointer: a zero-size trigger is
// parked at the coordinates and the menu anchors to it. Reusing the dropdown keeps
// this to no new package.
export function ContextMenu({ anchor, onClose, children }: {
  anchor: MenuAnchor | null
  onClose: () => void
  children: ReactNode
}) {
  return (
    <DropdownMenu open={anchor !== null} onOpenChange={(open) => { if (!open) onClose() }}>
      <DropdownMenuTrigger asChild>
        <span
          aria-hidden
          style={{
            position: 'fixed',
            left: anchor?.x ?? 0,
            top: anchor?.y ?? 0,
            width: 1,
            height: 1,
          }}
        />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start">{children}</DropdownMenuContent>
    </DropdownMenu>
  )
}
