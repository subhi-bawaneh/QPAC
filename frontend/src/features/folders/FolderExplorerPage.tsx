import { useEffect, useMemo, useState } from 'react'
import { ChevronRight, FolderPlus, MoreHorizontal, Pencil, RefreshCw, Trash2, Upload } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Card, CardBody } from '@/shared/ui/card'
import { SelectItem, SimpleSelect } from '@/shared/ui/select'
import { Spinner } from '@/shared/ui/spinner'
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel,
  DropdownMenuSeparator, DropdownMenuTrigger,
} from '@/shared/ui/dropdown-menu'
import { ConfirmDialog, PromptDialog } from '@/shared/ui/prompt-dialog'
import { apiErrorMessage } from '@/shared/api/client'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import { useAuth } from '@/shared/auth/useAuth'
import { Permissions } from '@/shared/auth/permissions'
import type { DataTarget, FolderFileSummary, FolderTreeNode } from '@/shared/api/types'
import { FolderTree } from './FolderTree'
import { FileList } from './FileList'
import { UploadDialog } from './UploadDialog'
import { SyncDriveDialog } from './SyncDriveDialog'
import { TargetBadge } from './FolderBadges'
import { ImportDialog } from '@/features/imports/ImportDialog'
import {
  useCreateFolder, useDeleteFile, useDeleteFolder, useFolderDetail,
  useFolderTree, useRenameFolder, useSetFolderTarget,
} from './api'

type PendingAction =
  | { kind: 'new-folder' }
  | { kind: 'rename'; id: string; name: string; parentId: string | null }
  | { kind: 'delete-folder'; id: string; name: string; parentId: string | null }
  | { kind: 'delete-file'; file: FolderFileSummary }

export function FolderExplorerPage() {
  const { can } = useAuth()
  const canManage = can(Permissions.foldersManage)
  const canAssignTarget = can(Permissions.foldersAssignTarget)
  const canImport = can(Permissions.importRun)
  const canSync = can(Permissions.driveSync)

  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [uploadOpen, setUploadOpen] = useState(false)
  const [syncOpen, setSyncOpen] = useState(false)
  const [importFile, setImportFile] = useState<FolderFileSummary | null>(null)
  const [pending, setPending] = useState<PendingAction | null>(null)
  const [error, setError] = useState<string | null>(null)

  const tree = useFolderTree(QPAC_PROJECT_ID)
  const detail = useFolderDetail(selectedId)
  const createFolder = useCreateFolder(QPAC_PROJECT_ID)
  const renameFolder = useRenameFolder(QPAC_PROJECT_ID)
  const deleteFolder = useDeleteFolder(QPAC_PROJECT_ID)
  const setTarget = useSetFolderTarget(QPAC_PROJECT_ID)
  const deleteFile = useDeleteFile(QPAC_PROJECT_ID)

  // Select the first folder once the tree arrives, so the pane is never empty.
  useEffect(() => {
    if (selectedId === null && tree.data?.length) setSelectedId(tree.data[0].id)
  }, [tree.data, selectedId])

  const folder = detail.data?.folder
  const breadcrumb = useMemo(
    () => (folder ? folder.path.split('/').filter(Boolean) : []),
    [folder],
  )

  const busy = createFolder.isPending || renameFolder.isPending || deleteFolder.isPending || deleteFile.isPending

  const runAction = async (action: () => Promise<unknown>, fallback: string) => {
    setError(null)
    try {
      await action()
      setPending(null)
    } catch (caught) {
      setError(apiErrorMessage(caught, fallback))
      setPending(null)
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">TIDPs</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Folders, workbooks and imports. A folder's target decides whether an import
            writes straight to Live or into a draft for review.
          </p>
        </div>

        <div className="flex gap-2">
          {canSync ? (
            <Button variant="outline" size="sm" onClick={() => setSyncOpen(true)}>
              <RefreshCw aria-hidden />
              Sync from Drive
            </Button>
          ) : null}
          {canManage ? (
            <Button size="sm" onClick={() => setPending({ kind: 'new-folder' })}>
              <FolderPlus aria-hidden />
              New folder
            </Button>
          ) : null}
        </div>
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
                selectedId={selectedId}
                onSelect={(node: FolderTreeNode) => setSelectedId(node.id)}
              />
            ) : null}
          </div>
        </Card>

        <Card>
          <div className="flex flex-wrap items-center justify-between gap-3 border-b px-5 py-3">
            <div className="min-w-0">
              <nav aria-label="Breadcrumb" className="flex items-center gap-1 text-xs text-muted-foreground">
                {breadcrumb.length === 0 ? '—' : breadcrumb.map((part, index) => (
                  <span key={`${part}-${index}`} className="flex items-center gap-1">
                    {index > 0 ? <ChevronRight className="h-3 w-3" aria-hidden /> : null}
                    {part}
                  </span>
                ))}
              </nav>
              <h2 className="mt-1 flex items-center gap-2 text-sm font-semibold">
                {folder?.name ?? 'Select a folder'}
                {folder ? <TargetBadge target={folder.target} /> : null}
              </h2>
            </div>

            {folder ? (
              <div className="flex items-center gap-2">
                {canAssignTarget ? (
                  <SimpleSelect
                    className="h-8 w-28"
                    label="Folder target"
                    value={folder.target}
                    disabled={setTarget.isPending}
                    onValueChange={(value) =>
                      void runAction(
                        () => setTarget.mutateAsync({ folderId: folder.id, target: value as DataTarget }),
                        'Could not change the target',
                      )
                    }
                  >
                    <SelectItem value="Live">Live</SelectItem>
                    <SelectItem value="Draft">Draft</SelectItem>
                  </SimpleSelect>
                ) : null}

                {canManage ? (
                  <Button size="sm" onClick={() => setUploadOpen(true)}>
                    <Upload aria-hidden />
                    Upload
                  </Button>
                ) : null}

                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="outline" size="icon-sm" aria-label={`Actions for ${folder.name}`}>
                      <MoreHorizontal aria-hidden />
                    </Button>
                  </DropdownMenuTrigger>

                  <DropdownMenuContent align="end">
                    <DropdownMenuLabel>{folder.name}</DropdownMenuLabel>
                    <DropdownMenuSeparator />

                    <DropdownMenuItem
                      disabled={!canManage}
                      onSelect={() => setPending({ kind: 'new-folder' })}
                    >
                      <FolderPlus aria-hidden />
                      New subfolder
                    </DropdownMenuItem>

                    <DropdownMenuItem
                      disabled={!canManage}
                      onSelect={() =>
                        setPending({ kind: 'rename', id: folder.id, name: folder.name, parentId: folder.parentId })
                      }
                    >
                      <Pencil aria-hidden />
                      Rename
                    </DropdownMenuItem>

                    <DropdownMenuItem disabled={!canSync} onSelect={() => setSyncOpen(true)}>
                      <RefreshCw aria-hidden />
                      Sync from Drive
                    </DropdownMenuItem>

                    <DropdownMenuSeparator />

                    <DropdownMenuItem
                      destructive
                      disabled={!canManage}
                      onSelect={() =>
                        setPending({ kind: 'delete-folder', id: folder.id, name: folder.name, parentId: folder.parentId })
                      }
                    >
                      <Trash2 aria-hidden />
                      Delete folder
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </div>
            ) : null}
          </div>

          <CardBody className="p-0">
            {detail.isPending && selectedId ? <Spinner /> : null}
            {detail.isError ? (
              <p className="px-5 py-4 text-sm text-destructive">{apiErrorMessage(detail.error)}</p>
            ) : null}
            {detail.data ? (
              <FileList
                files={detail.data.files}
                canImport={canImport}
                canManage={canManage}
                isDraftFolder={detail.data.folder.target === 'Draft'}
                onImport={setImportFile}
                onDelete={(file) => setPending({ kind: 'delete-file', file })}
              />
            ) : null}
          </CardBody>
        </Card>
      </div>

      {folder ? (
        <>
          <UploadDialog
            open={uploadOpen}
            onClose={() => setUploadOpen(false)}
            folderId={folder.id}
            folderName={folder.name}
            projectId={QPAC_PROJECT_ID}
          />

          <ImportDialog
            open={importFile !== null}
            onClose={() => setImportFile(null)}
            file={importFile}
            folderId={folder.id}
            folderTarget={folder.target}
            projectId={QPAC_PROJECT_ID}
          />
        </>
      ) : null}

      <SyncDriveDialog open={syncOpen} onClose={() => setSyncOpen(false)} projectId={QPAC_PROJECT_ID} />

      <PromptDialog
        open={pending?.kind === 'new-folder'}
        title="New folder"
        description={folder ? `Created inside ${folder.name}.` : 'Created at the top level.'}
        label="Folder name"
        confirmLabel="Create"
        pending={busy}
        onCancel={() => setPending(null)}
        onConfirm={(name) =>
          void runAction(
            () => createFolder.mutateAsync({ parentId: selectedId, name }),
            'Could not create the folder',
          )
        }
      />

      <PromptDialog
        open={pending?.kind === 'rename'}
        title="Rename folder"
        label="Folder name"
        initialValue={pending?.kind === 'rename' ? pending.name : ''}
        confirmLabel="Rename"
        pending={busy}
        onCancel={() => setPending(null)}
        onConfirm={(newName) =>
          pending?.kind === 'rename'
            ? void runAction(
                () => renameFolder.mutateAsync({ folderId: pending.id, newName }),
                'Could not rename the folder',
              )
            : undefined
        }
      />

      <ConfirmDialog
        open={pending?.kind === 'delete-folder'}
        title="Delete this folder?"
        description={
          pending?.kind === 'delete-folder'
            ? `'${pending.name}' is removed from the explorer. It must be empty first — subfolders and files are deleted separately.`
            : ''
        }
        confirmLabel="Delete folder"
        pending={busy}
        onCancel={() => setPending(null)}
        onConfirm={() =>
          pending?.kind === 'delete-folder'
            ? void runAction(async () => {
                await deleteFolder.mutateAsync({ folderId: pending.id, parentId: pending.parentId })
                if (selectedId === pending.id) setSelectedId(null)
              }, 'Could not delete the folder')
            : undefined
        }
      />

      <ConfirmDialog
        open={pending?.kind === 'delete-file'}
        title="Delete this file?"
        description={
          pending?.kind === 'delete-file'
            ? `'${pending.file.name}' is removed. Rows already imported from it stay in the Live layer.`
            : ''
        }
        confirmLabel="Delete file"
        pending={busy}
        onCancel={() => setPending(null)}
        onConfirm={() =>
          pending?.kind === 'delete-file' && selectedId
            ? void runAction(
                () => deleteFile.mutateAsync({ fileId: pending.file.id, folderId: selectedId }),
                'Could not delete the file',
              )
            : undefined
        }
      />
    </div>
  )
}
