import { X } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import {
  type TidpOwnerDto,
} from './api'

interface TidpMobileDrawerProps {
  isOpen: boolean
  onClose: () => void
  data?: {
    owners: TidpOwnerDto[]
  }
  onNavigate: (path: { id: string; name: string; type: 'owner' | 'discipline' }[]) => void
}

export function TidpMobileDrawer({
  isOpen, onClose, data, onNavigate,
}: TidpMobileDrawerProps) {
  if (!isOpen) return null

  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-black/50 z-40"
        onClick={onClose}
        aria-hidden
      />

      {/* Drawer */}
      <div className="fixed left-0 top-0 bottom-0 w-64 bg-card border-r border-border z-50 shadow-lg overflow-y-auto">
        <div className="flex items-center justify-between p-4 border-b border-border sticky top-0 bg-card">
          <h2 className="font-semibold">TIDP Structure</h2>
          <Button
            variant="ghost"
            size="sm"
            onClick={onClose}
            className="p-1 h-auto"
          >
            <X className="h-4 w-4" />
          </Button>
        </div>

        <div>
          {data?.owners && data.owners.length > 0 ? (
            data.owners.map((owner) => (
              <button
                key={owner.id}
                onClick={() => {
                  onNavigate([{ id: owner.id, name: owner.folderName, type: 'owner' }])
                  onClose()
                }}
                className="w-full text-left px-4 py-3 text-sm hover:bg-accent border-b border-border transition-colors"
              >
                <div className="font-medium">{owner.folderName}</div>
                {owner.ownerName && (
                  <div className="text-xs text-muted-foreground">{owner.ownerName}</div>
                )}
              </button>
            ))
          ) : (
            <div className="p-4 text-sm text-muted-foreground">No TIDP folder uploaded</div>
          )}
        </div>
      </div>
    </>
  )
}
