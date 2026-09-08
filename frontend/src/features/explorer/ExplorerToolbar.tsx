import { ChevronRight, CloudOff, FolderPlus, RefreshCw, Upload, Users } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Badge } from '@/shared/ui/badge'
import { SelectItem, SimpleSelect } from '@/shared/ui/select'
import type { DataTarget, DriveStatus, FolderNode } from '@/shared/api/types'
import { TargetBadge } from './FolderBadges'

export interface ToolbarPermissions {
  canManage: boolean
  canAssignTarget: boolean
  canPromote: boolean
  canSync: boolean
}

export function ExplorerToolbar({
  folder, breadcrumb, permissions, driveStatus, syncing,
  onNavigate, onUpload, onSync, onNewFolder, onSetTarget, onCompany, onConvert,
}: {
  folder: FolderNode | undefined
  breadcrumb: { id: string; name: string }[]
  permissions: ToolbarPermissions
  driveStatus: DriveStatus | undefined
  syncing: boolean
  onNavigate: (folderId: string) => void
  onUpload: () => void
  onSync: () => void
  onNewFolder: () => void
  onSetTarget: (target: DataTarget) => void
  onCompany: () => void
  onConvert: () => void
}) {
  // Creating inside a Drive folder is refused by the API (decision D6), so the
  // button is not offered there either.
  const canCreateHere = permissions.canManage && folder !== undefined && folder.driveFolderId === null
  const canConvert = permissions.canAssignTarget && permissions.canPromote
    && folder !== undefined && folder.target === 'Draft'

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-4 py-3">
      <nav aria-label="Breadcrumb" className="flex min-w-0 items-center gap-1 text-sm">
        {breadcrumb.map((crumb, index) => (
          <span key={crumb.id} className="flex min-w-0 items-center gap-1">
            {index > 0 ? <ChevronRight className="h-3 w-3 shrink-0 text-muted-foreground" aria-hidden /> : null}
            <button
              type="button"
              className="truncate rounded px-1 hover:bg-muted"
              onClick={() => onNavigate(crumb.id)}
            >
              {crumb.name}
            </button>
          </span>
        ))}
        {folder ? <TargetBadge target={folder.target} /> : null}
        {folder?.isCompany ? <Badge tone="neutral">{folder.authorName ?? 'Company'}</Badge> : null}
      </nav>

      <div className="flex flex-wrap items-center gap-2">
        <DriveStatusPill status={driveStatus} syncing={syncing} />

        {permissions.canAssignTarget && folder ? (
          <SimpleSelect
            className="h-8 w-32"
            label="Folder target"
            value={folder.target}
            onValueChange={(value) => onSetTarget(value as DataTarget)}
          >
            <SelectItem value="Live">Live</SelectItem>
            <SelectItem value="Draft">Draft</SelectItem>
          </SimpleSelect>
        ) : null}

        {permissions.canAssignTarget && folder ? (
          <Button variant="outline" size="sm" onClick={onCompany}>
            <Users aria-hidden />
            Company
          </Button>
        ) : null}

        {canConvert ? (
          <Button variant="outline" size="sm" onClick={onConvert}>
            Convert to Live
          </Button>
        ) : null}

        {permissions.canSync ? (
          <Button variant="outline" size="sm" onClick={onSync} disabled={syncing}>
            <RefreshCw className={syncing ? 'animate-spin' : undefined} aria-hidden />
            Sync now
          </Button>
        ) : null}

        {canCreateHere ? (
          <Button variant="outline" size="sm" onClick={onNewFolder}>
            <FolderPlus aria-hidden />
            New folder
          </Button>
        ) : null}

        {permissions.canManage && folder ? (
          <Button size="sm" onClick={onUpload}>
            <Upload aria-hidden />
            Upload
          </Button>
        ) : null}
      </div>
    </div>
  )
}

function DriveStatusPill({ status, syncing }: { status: DriveStatus | undefined; syncing: boolean }) {
  if (!status) {
    return (
      <span className="flex items-center gap-1 text-xs text-muted-foreground">
        <CloudOff className="h-3.5 w-3.5" aria-hidden />
        Drive status unknown
      </span>
    )
  }

  const running = status.isRunning || syncing
  const tone = status.lastRunError ? 'danger' : running ? 'warning' : 'success'
  const label = status.lastRunError
    ? 'Drive sync failed'
    : running
      ? 'Syncing…'
      : status.queuedImports > 0
        ? `${status.queuedImports} queued`
        : 'Drive in sync'

  return <Badge tone={tone}>{label}</Badge>
}
