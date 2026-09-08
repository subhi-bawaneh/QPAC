import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Download, FolderOpen, FolderPlus, GitCompare, RefreshCw, Users } from 'lucide-react'
import { Card, CardBody } from '@/shared/ui/card'
import { Spinner } from '@/shared/ui/spinner'
import { Button } from '@/shared/ui/button'
import { DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator } from '@/shared/ui/dropdown-menu'
import { PromptDialog } from '@/shared/ui/prompt-dialog'
import { useToast } from '@/shared/ui/useToast'
import { api, apiErrorMessage } from '@/shared/api/client'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import { useAuth } from '@/shared/auth/useAuth'
import { Permissions } from '@/shared/auth/permissions'
import { useSyncEvent } from '@/shared/realtime/useSyncEvent'
import type { DataTarget, FolderTreeNode } from '@/shared/api/types'
import { FolderTree } from './FolderTree'
import { TileGrid, type GridItem, type GridSelection } from './TileGrid'
import { ExplorerToolbar } from './ExplorerToolbar'
import { ContextMenu, type MenuAnchor } from './ContextMenu'
import { UploadDialog } from './UploadDialog'
import { ConvertToLiveDialog } from './ConvertToLiveDialog'
import { CompanyDialog } from './CompanyDialog'
import { subtreeFolderIds } from './tree'
import {
  useCreateFolder, useSubtreeFiles, useDriveStatus, useFolderDetail, useFolderTree,
  useSetFolderTarget, useTriggerSync,
} from './api'

export function ExplorerPage() {
  const { can } = useAuth()
  const navigate = useNavigate()
  const { toast } = useToast()

  const permissions = {
    canManage: can(Permissions.foldersManage),
    canAssignTarget: can(Permissions.foldersAssignTarget),
    canPromote: can(Permissions.draftsPromote),
    canSync: can(Permissions.driveSync),
  }

  const [folderId, setFolderId] = useState<string | null>(null)
  const [selection, setSelection] = useState<GridSelection | null>(null)
  const [menu, setMenu] = useState<{ anchor: MenuAnchor; item: GridItem } | null>(null)
  const [dialog, setDialog] = useState<'upload' | 'convert' | 'company' | 'new-folder' | null>(null)
  // The folder a new folder goes into: the right-clicked tile, else the open folder.
  const [newFolderParent, setNewFolderParent] = useState<{ id: string; name: string } | null>(null)
  const [droppedFiles, setDroppedFiles] = useState<File[]>([])
  const [importing, setImporting] = useState<ReadonlySet<string>>(new Set())
  const [error, setError] = useState<string | null>(null)

  const tree = useFolderTree(QPAC_PROJECT_ID)
  const detail = useFolderDetail(folderId)
  const driveStatus = useDriveStatus(QPAC_PROJECT_ID)
  const createFolder = useCreateFolder(QPAC_PROJECT_ID)
  const setTarget = useSetFolderTarget(QPAC_PROJECT_ID)
  const triggerSync = useTriggerSync(QPAC_PROJECT_ID)

  // Select the first folder once the tree arrives, so the pane is never empty.
  useEffect(() => {
    if (folderId === null && tree.data?.length) setFolderId(tree.data[0].id)
  }, [tree.data, folderId])

  const folder = detail.data?.folder
  const files = useMemo(() => detail.data?.files ?? [], [detail.data])
  const subfolders = useMemo(() => detail.data?.subfolders ?? [], [detail.data])

  const breadcrumb = useMemo(() => buildBreadcrumb(tree.data ?? [], folderId), [tree.data, folderId])

  // Convert promotes every file below the folder, so its preview needs the subtree.
  const subtreeIds = useMemo(() => subtreeFolderIds(tree.data ?? [], folderId), [tree.data, folderId])
  const subtreeFiles = useSubtreeFiles(dialog === 'convert' ? subtreeIds : [])

  // ------------------------------------------------------------- realtime

  const mark = useCallback((fileId: string, active: boolean) => {
    setImporting((current) => {
      const next = new Set(current)
      if (active) next.add(fileId)
      else next.delete(fileId)
      return next
    })
  }, [])

  useSyncEvent('syncStarted', () => toast('Drive sync started'))
  useSyncEvent('syncFinished', (event) => {
    toast(
      event.error
        ? `Drive sync finished with errors: ${event.error}`
        : `Drive sync finished — ${event.foldersSynced} folder(s), ${event.filesQueued} file(s) queued`,
      event.error ? 'danger' : 'success',
    )
  })
  useSyncEvent('fileImportStarted', (event) => mark(event.fileId, true))
  useSyncEvent('fileImported', (event) => {
    mark(event.fileId, false)
    toast(`Imported ${event.batch.fileName} — ${event.batch.rowsRead} row(s)`, 'success')
  })
  useSyncEvent('fileFailed', (event) => {
    mark(event.fileId, false)
    toast(`Import failed: ${event.error}`, 'danger')
  })

  // --------------------------------------------------------------- actions

  const run = async (action: () => Promise<unknown>, fallback: string) => {
    setError(null)
    try {
      await action()
    } catch (caught) {
      setError(apiErrorMessage(caught, fallback))
    }
  }

  const open = (item: GridItem) => {
    if (item.kind === 'folder') {
      setFolderId(item.folder.id)
      setSelection(null)
      return
    }

    navigate(`/files/${item.file.id}`)
  }

  const download = async (fileId: string, name: string) => {
    const response = await api.get(`/api/folder-files/${fileId}/download`, { responseType: 'blob' })
    const url = URL.createObjectURL(response.data as Blob)
    const link = document.createElement('a')
    link.href = url
    link.download = name
    link.click()
    URL.revokeObjectURL(url)
  }

  const dropped = (incoming: File[]) => {
    if (incoming.length === 0 || !folder) return
    setDroppedFiles(incoming)
    setDialog('upload')
  }

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold tracking-tight">TIDPs</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Google Drive is mirrored here read-only. A folder's target decides which layer its
          rows count in; converting a company promotes its drafts and switches it to Live.
        </p>
      </div>

      {error ? (
        <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </p>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-[18rem_1fr]">
        <Card className="h-fit">
          <div className="flex items-center justify-between border-b px-4 py-3">
            <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              Folders
            </span>
            <Button
              variant="ghost"
              size="icon-sm"
              aria-label="Refresh folders"
              onClick={() => void tree.refetch()}
              disabled={tree.isFetching}
            >
              <RefreshCw className={tree.isFetching ? 'animate-spin' : undefined} aria-hidden />
            </Button>
          </div>

          <div className="max-h-[70vh] overflow-y-auto p-2">
            {tree.isPending ? <Spinner /> : null}
            {tree.isError ? (
              <p className="px-3 py-4 text-sm text-destructive">{apiErrorMessage(tree.error)}</p>
            ) : null}
            {tree.data ? (
              <FolderTree
                nodes={tree.data}
                selectedId={folderId}
                onSelect={(node: FolderTreeNode) => { setFolderId(node.id); setSelection(null) }}
              />
            ) : null}
          </div>
        </Card>

        <Card>
          <ExplorerToolbar
            folder={folder}
            breadcrumb={breadcrumb}
            permissions={permissions}
            driveStatus={driveStatus.data}
            syncing={triggerSync.isPending}
            onNavigate={(id) => { setFolderId(id); setSelection(null) }}
            onUpload={() => { setDroppedFiles([]); setDialog('upload') }}
            onSync={() => void run(async () => {
              const result = await triggerSync.mutateAsync()
              toast(result.alreadyRunning ? 'A sync is already running' : 'Drive sync queued')
            }, 'Could not start the sync')}
            onNewFolder={() => { setNewFolderParent(null); setDialog('new-folder') }}
            onSetTarget={(target: DataTarget) => folder && void run(
              () => setTarget.mutateAsync({ folderId: folder.id, target }),
              'Could not change the target',
            )}
            onCompany={() => setDialog('company')}
            onConvert={() => setDialog('convert')}
          />

          <CardBody className="p-0">
            {detail.isPending && folderId ? <Spinner /> : null}
            {detail.isError ? (
              <p className="px-5 py-4 text-sm text-destructive">{apiErrorMessage(detail.error)}</p>
            ) : null}
            {detail.data ? (
              <TileGrid
                folders={subfolders}
                files={files}
                importingFileIds={importing}
                selection={selection}
                canUpload={permissions.canManage}
                onSelect={setSelection}
                onOpen={open}
                onMenu={(item, event) => {
                  event.preventDefault()
                  setSelection({ kind: item.kind, id: item.kind === 'folder' ? item.folder.id : item.file.id })
                  setMenu({ anchor: { x: event.clientX, y: event.clientY }, item })
                }}
                onDropFiles={dropped}
              />
            ) : null}
          </CardBody>
        </Card>
      </div>

      <ContextMenu anchor={menu?.anchor ?? null} onClose={() => setMenu(null)}>
        {menu?.item.kind === 'folder' ? (
          <>
            <DropdownMenuLabel>{menu.item.folder.name}</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={() => open(menu.item)}>
              <FolderOpen aria-hidden />
              Open
            </DropdownMenuItem>
            <DropdownMenuItem
              disabled={!permissions.canAssignTarget}
              onSelect={() => { setFolderId(menu.item.kind === 'folder' ? menu.item.folder.id : null); setDialog('company') }}
            >
              <Users aria-hidden />
              Company / author
            </DropdownMenuItem>
            <DropdownMenuItem
              disabled={!permissions.canAssignTarget || !permissions.canPromote
                || menu.item.folder.target !== 'Draft'}
              onSelect={() => { setFolderId(menu.item.kind === 'folder' ? menu.item.folder.id : null); setDialog('convert') }}
            >
              <GitCompare aria-hidden />
              Convert to Live
            </DropdownMenuItem>
            <DropdownMenuItem
              disabled={!permissions.canManage || menu.item.folder.driveFolderId !== null}
              onSelect={() => {
                if (menu.item.kind === 'folder') setNewFolderParent({ id: menu.item.folder.id, name: menu.item.folder.name })
                setDialog('new-folder')
              }}
            >
              <FolderPlus aria-hidden />
              New folder
            </DropdownMenuItem>
          </>
        ) : null}

        {menu?.item.kind === 'file' ? (
          <>
            <DropdownMenuLabel className="max-w-[16rem] truncate">{menu.item.file.name}</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={() => open(menu.item)}>
              <FolderOpen aria-hidden />
              Open
            </DropdownMenuItem>
            <DropdownMenuItem
              onSelect={() => menu.item.kind === 'file' && void run(
                () => download(menu.item.kind === 'file' ? menu.item.file.id : '', menu.item.kind === 'file' ? menu.item.file.name : ''),
                'Could not download the file',
              )}
            >
              <Download aria-hidden />
              Download
            </DropdownMenuItem>
            {folder?.target === 'Draft' ? (
              <DropdownMenuItem
                onSelect={() => menu.item.kind === 'file' && navigate(`/drafts/${menu.item.file.id}`)}
              >
                <GitCompare aria-hidden />
                Review draft
              </DropdownMenuItem>
            ) : null}
          </>
        ) : null}
      </ContextMenu>

      {folder ? (
        <>
          <UploadDialog
            open={dialog === 'upload'}
            onClose={() => { setDialog(null); setDroppedFiles([]) }}
            folderId={folder.id}
            folderName={folder.name}
            projectId={QPAC_PROJECT_ID}
            initialFiles={droppedFiles.length > 0 ? droppedFiles : undefined}
          />

          <ConvertToLiveDialog
            open={dialog === 'convert'}
            onClose={() => setDialog(null)}
            folder={folder}
            files={subtreeFiles.files}
            loading={subtreeFiles.loading}
            projectId={QPAC_PROJECT_ID}
          />

          <CompanyDialog
            open={dialog === 'company'}
            onClose={() => setDialog(null)}
            folder={folder}
            projectId={QPAC_PROJECT_ID}
          />
        </>
      ) : null}

      <PromptDialog
        open={dialog === 'new-folder'}
        title="New folder"
        description={(newFolderParent ?? folder) ? `Created inside ${(newFolderParent ?? folder)?.name}.` : 'Created at the top level.'}
        label="Folder name"
        confirmLabel="Create"
        pending={createFolder.isPending}
        onCancel={() => setDialog(null)}
        onConfirm={(name) => void run(async () => {
          await createFolder.mutateAsync({ parentId: newFolderParent?.id ?? folderId, name })
          setDialog(null)
        }, 'Could not create the folder')}
      />
    </div>
  )
}

// The path from the root of the tree down to the open folder.
function buildBreadcrumb(
  nodes: FolderTreeNode[], folderId: string | null,
): { id: string; name: string }[] {
  if (folderId === null) return []

  const walk = (
    current: FolderTreeNode[], trail: { id: string; name: string }[],
  ): { id: string; name: string }[] | null => {
    for (const node of current) {
      const next = [...trail, { id: node.id, name: node.name }]
      if (node.id === folderId) return next
      const found = walk(node.children, next)
      if (found) return found
    }
    return null
  }

  return walk(nodes, []) ?? []
}
