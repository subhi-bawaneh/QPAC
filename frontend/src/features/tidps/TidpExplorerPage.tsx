import { useMemo, useState } from 'react'
import { ChevronRight, Upload } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Spinner } from '@/shared/ui/spinner'
import { useAuth } from '@/shared/auth/useAuth'
import { Permissions } from '@/shared/auth/permissions'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import { apiErrorMessage } from '@/shared/api/client'
import { formatDate } from '@/shared/lib/utils'
import { TidpFolderUploadDialog } from './TidpFolderUploadDialog'
import { TidpFolderSyncResultView } from './TidpFolderSyncResult'
import {
  useSyncTidpFolder, useTidpFolderSyncStatus, useTidpFolderTree,
  type TidpOwnerDto, type TidpDisciplineFolderDto, type TidpFolderFileDto,
} from './api'
import { buildManifestFromWebkitDirectory } from './manifestBuilder'

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

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
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

        {canUpload && (
          <Button onClick={() => setShowFolderUpload(true)} disabled={syncFolder.isPending}>
            <Upload className="h-4 w-4 mr-2" aria-hidden />
            {syncFolder.isPending ? 'Uploading…' : 'Upload TIDPs Folder'}
          </Button>
        )}
      </div>

      <TidpFolderUploadDialog
        open={showFolderUpload}
        onClose={() => setShowFolderUpload(false)}
        onFolderSelected={handleFolderSelected}
        isLoading={syncFolder.isPending}
      />

      {syncFolder.isError && (
        <p className="text-sm text-destructive">{apiErrorMessage(syncFolder.error)}</p>
      )}

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
        <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
          {items.map((item) => {
            const isOwner = 'ownerName' in item
            const isDiscipline = 'disciplineCode' in item && !('ownerName' in item)
            const isFile = 'relativePath' in item && !('disciplines' in item) && !('disciplineCode' in item)

            if (isOwner) {
              const owner = item as TidpOwnerDto
              return (
                <button
                  key={owner.id}
                  onClick={() => setPath([{ id: owner.id, name: owner.folderName, type: 'owner' }])}
                  className="rounded-lg border border-border bg-card p-4 hover:bg-accent text-left transition-colors"
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex-1 min-w-0">
                      <h3 className="font-medium truncate">{owner.folderName}</h3>
                      <p className="text-sm text-muted-foreground">
                        {owner.disciplines.length + owner.files.length} items
                      </p>
                      {owner.ownerName && (
                        <p className="text-xs text-muted-foreground mt-1">{owner.ownerName}</p>
                      )}
                    </div>
                    <ChevronRight className="h-4 w-4 flex-shrink-0 mt-1" />
                  </div>
                </button>
              )
            }

            if (isDiscipline) {
              const discipline = item as TidpDisciplineFolderDto
              return (
                <button
                  key={discipline.id}
                  onClick={() =>
                    setPath([
                      ...path,
                      { id: discipline.id, name: discipline.folderName, type: 'discipline' },
                    ])
                  }
                  className="rounded-lg border border-border bg-card p-4 hover:bg-accent text-left transition-colors"
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex-1 min-w-0">
                      <h3 className="font-medium truncate">{discipline.folderName}</h3>
                      <p className="text-sm text-muted-foreground">
                        {discipline.files.length} file{discipline.files.length !== 1 ? 's' : ''}
                      </p>
                    </div>
                    <ChevronRight className="h-4 w-4 flex-shrink-0 mt-1" />
                  </div>
                </button>
              )
            }

            if (isFile) {
              const file = item as TidpFolderFileDto
              return (
                <div
                  key={file.id}
                  className={`rounded-lg border p-4 ${
                    file.folderStatus === 'Missing' ? 'border-yellow-300 bg-yellow-50' : 'border-border bg-card'
                  }`}
                >
                  <h3 className="font-medium truncate text-sm">{file.fileName}</h3>
                  <div className="text-xs text-muted-foreground mt-2 space-y-1">
                    {file.lastModifiedUtc && (
                      <p>Modified: {formatDate(new Date(file.lastModifiedUtc))}</p>
                    )}
                    {file.status && <p>Status: {file.status}</p>}
                    {file.rowsImported !== null && <p>Rows imported: {file.rowsImported}</p>}
                    {file.folderStatus === 'Missing' && <p className="text-yellow-700">⚠ Missing</p>}
                  </div>
                </div>
              )
            }

            return null
          })}
        </div>
      )}
    </div>
  )
}
