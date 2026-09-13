import { useMemo, useState, useEffect } from 'react'
import { Grid3x3, List, Menu, Upload } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Spinner } from '@/shared/ui/spinner'
import { useAuth } from '@/shared/auth/useAuth'
import { Permissions } from '@/shared/auth/permissions'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import { apiErrorMessage } from '@/shared/api/client'
import { TidpFolderUploadDialog } from './TidpFolderUploadDialog'
import { TidpFolderSyncResultView } from './TidpFolderSyncResult'
import { TidpSidebar } from './TidpSidebar'
import { TidpListView } from './TidpListView'
import { TidpMobileDrawer } from './TidpMobileDrawer'
import {
  useSyncTidpFolder, useTidpFolderSyncStatus, useTidpFolderTree,
  type TidpOwnerDto, type TidpDisciplineFolderDto,
} from './api'
import { buildManifestFromWebkitDirectory } from './manifestBuilder'

const SIDEBAR_COLLAPSE_KEY = 'tidp-sidebar-collapsed'

export function TidpExplorerPage() {
  const { can } = useAuth()
  const canUpload = can(Permissions.filesManage)

  const folderTree = useTidpFolderTree(QPAC_PROJECT_ID)
  const syncFolder = useSyncTidpFolder(QPAC_PROJECT_ID)
  const [syncId, setSyncId] = useState<string | null>(null)
  const syncStatus = useTidpFolderSyncStatus(QPAC_PROJECT_ID, syncId)
  const [showFolderUpload, setShowFolderUpload] = useState(false)

  // Navigation state: path of IDs to current node
  const [path, setPath] = useState<{ id: string; name: string; type: 'owner' | 'discipline' }[]>([])

  // View mode state
  const [isGridView, setIsGridView] = useState(true)
  const [sidebarCollapsed, setSidebarCollapsed] = useState(() => {
    try {
      const stored = localStorage.getItem(SIDEBAR_COLLAPSE_KEY)
      return stored ? JSON.parse(stored) : false
    } catch {
      return false
    }
  })
  const [mobileDrawerOpen, setMobileDrawerOpen] = useState(false)

  useEffect(() => {
    try {
      localStorage.setItem(SIDEBAR_COLLAPSE_KEY, JSON.stringify(sidebarCollapsed))
    } catch {
      // localStorage might be unavailable in some contexts
    }
  }, [sidebarCollapsed])

  // Get current node
  const currentNode = useMemo(() => {
    if (path.length === 0) return null

    let node: any = folderTree.data?.owners.find((o) => o.id === path[0].id)
    if (!node) return null

    for (let i = 1; i < path.length; i++) {
      if ('disciplines' in node) {
        node = node.disciplines.find((d: TidpDisciplineFolderDto) => d.id === path[i].id)
      }
      if (!node) return null
    }

    return node
  }, [path, folderTree.data])

  // Get items to display
  const items = useMemo(() => {
    if (path.length === 0) {
      // Root: show all owners
      return folderTree.data?.owners ?? []
    }

    if (path.length === 1) {
      // Inside owner: show disciplines + direct files
      const owner = currentNode as TidpOwnerDto
      if (!owner) return []

      const items: any[] = [
        ...(owner.disciplines ?? []),
        ...(owner.files ?? []),
      ]
      return items
    }

    if (path.length === 2) {
      // Inside discipline: show files
      const discipline = currentNode as TidpDisciplineFolderDto
      return discipline?.files ?? []
    }

    return []
  }, [path, currentNode, folderTree.data])

  const isSyncComplete = syncId && syncStatus.data
    ? syncStatus.data.added + syncStatus.data.updated + syncStatus.data.skipped + syncStatus.data.missing + syncStatus.data.failed === syncStatus.data.totalFiles
    : false

  async function handleFolderSelected(folderName: string, fileList: FileList) {
    try {
      const { manifest, files } = await buildManifestFromWebkitDirectory(fileList, folderName)
      if (manifest.files.length === 0) {
        throw new Error('No .xlsx or .xlsm files found')
      }

      const result = await syncFolder.mutateAsync({ manifest, files })
      setSyncId(result.syncId)
    } catch (error) {
      console.error('Folder sync error:', error)
    }
  }

  if (folderTree.isPending) return <Spinner />

  // Show result view if sync is in progress or completed
  if (syncId && syncStatus.data && isSyncComplete) {
    return (
      <div className="space-y-4">
        <TidpFolderSyncResultView
          result={syncStatus.data}
          onDone={() => {
            setSyncId(null)
            void folderTree.refetch()
          }}
        />
      </div>
    )
  }

  if (syncId && syncStatus.data && !isSyncComplete) {
    return (
      <div className="text-center py-8">
        <Spinner />
        <p className="text-sm text-muted-foreground mt-2">Syncing folder...</p>
      </div>
    )
  }

  const breadcrumb = path.map(p => p.name).join(' / ')

  return (
    <>
      <TidpMobileDrawer
        isOpen={mobileDrawerOpen}
        onClose={() => setMobileDrawerOpen(false)}
        data={folderTree.data}
        onNavigate={(newPath) => {
          setPath(newPath)
          setMobileDrawerOpen(false)
        }}
      />

      <div className="flex flex-col h-screen">
      {/* Header */}
      <div className="border-b border-border bg-card p-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3 flex-1">
            {/* Mobile menu button */}
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setMobileDrawerOpen(true)}
              className="md:hidden p-1 h-auto"
              title="Menu"
            >
              <Menu className="h-5 w-5" />
            </Button>

            <div>
              <h1 className="text-lg font-semibold">TIDP Folder Structure</h1>
              {path.length > 0 && (
                <div className="text-sm text-muted-foreground mt-1">
                  {path.map((p, i) => (
                    <span key={p.id}>
                      {i > 0 && ' / '}
                      <button
                        onClick={() => setPath(path.slice(0, i))}
                        className="hover:underline"
                      >
                        {p.name}
                      </button>
                    </span>
                  ))}
                </div>
              )}
            </div>
          </div>

          <div className="flex gap-2">
            {items.length > 0 && (
              <>
                <Button
                  variant={isGridView ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => setIsGridView(true)}
                  title="Grid view"
                >
                  <Grid3x3 className="h-4 w-4" />
                </Button>
                <Button
                  variant={!isGridView ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => setIsGridView(false)}
                  title="List view"
                >
                  <List className="h-4 w-4" />
                </Button>
              </>
            )}

            {canUpload && (
              <Button onClick={() => setShowFolderUpload(true)} disabled={syncFolder.isPending}>
                <Upload className="h-4 w-4 mr-2" aria-hidden />
                {syncFolder.isPending ? 'Uploading…' : 'Upload TIDPs Folder'}
              </Button>
            )}
          </div>
        </div>

        {syncFolder.isError && (
          <p className="text-sm text-destructive mt-3">{apiErrorMessage(syncFolder.error)}</p>
        )}
      </div>

      <TidpFolderUploadDialog
        open={showFolderUpload}
        onClose={() => setShowFolderUpload(false)}
        onFolderSelected={handleFolderSelected}
        isLoading={syncFolder.isPending}
      />

      {/* Content */}
      <div className="flex-1 overflow-hidden flex">
        {/* Sidebar on desktop */}
        <div className="hidden md:flex md:flex-col">
          <TidpSidebar
            data={folderTree.data}
            selectedPath={path}
            onNavigate={setPath}
            isCollapsed={sidebarCollapsed}
            onToggleCollapse={() => setSidebarCollapsed(!sidebarCollapsed)}
          />
        </div>

        {/* Main content */}
        <div className="flex-1 overflow-y-auto p-4">
          {items.length === 0 ? (
            <div className="rounded-lg border border-border bg-card p-6 text-center">
              <h3 className="font-semibold mb-2">
                {path.length === 0 ? 'No TIDP folder uploaded yet' : 'No items in this folder'}
              </h3>
              <p className="text-sm text-muted-foreground mb-4">
                {path.length === 0
                  ? 'Click "Upload TIDPs Folder" to sync your TIDP workbooks.'
                  : 'This folder contains no subfolders or files.'}
              </p>
              {path.length === 0 && canUpload && (
                <Button onClick={() => setShowFolderUpload(true)}>
                  <Upload className="h-4 w-4 mr-2" aria-hidden />
                  Upload TIDPs Folder
                </Button>
              )}
            </div>
          ) : (
            <TidpListView
              items={items}
              breadcrumb={breadcrumb}
              onItemClick={(item) => {
                if ('ownerName' in item) {
                  setPath([{ id: item.id, name: item.folderName, type: 'owner' }])
                } else if ('disciplineCode' in item && !('ownerName' in item)) {
                  setPath([
                    ...path,
                    { id: item.id, name: item.folderName, type: 'discipline' },
                  ])
                }
              }}
              isGridView={isGridView}
            />
          )}
        </div>
      </div>
      </div>
    </>
  )
}
