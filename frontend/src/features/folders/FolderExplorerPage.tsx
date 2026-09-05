import { useEffect, useMemo, useState } from 'react'
import { FolderPlus, RefreshCw, Upload } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Card, CardBody } from '@/shared/ui/card'
import { Select } from '@/shared/ui/select'
import { Spinner } from '@/shared/ui/spinner'
import { apiErrorMessage } from '@/shared/api/client'
import { useAuth } from '@/shared/auth/useAuth'
import { Permissions } from '@/shared/auth/permissions'
import type { DataTarget, FolderFileSummary, FolderTreeNode } from '@/shared/api/types'
import { FolderTree } from './FolderTree'
import { FileList } from './FileList'
import { UploadDialog } from './UploadDialog'
import { TargetBadge } from './FolderBadges'
import { ImportDialog } from '@/features/imports/ImportDialog'
import { useCreateFolder, useFolderDetail, useFolderTree, useSetFolderTarget, useDeleteFile } from './api'

// The seeded QPAC project; a switcher arrives with the admin screens in 6.6.
const QPAC_PROJECT_ID = '11111111-1111-1111-1111-111111111111'

export function FolderExplorerPage() {
  const { can } = useAuth()
  const canManage = can(Permissions.foldersManage)
  const canAssignTarget = can(Permissions.foldersAssignTarget)
  const canImport = can(Permissions.importRun)

  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [uploadOpen, setUploadOpen] = useState(false)
  const [importFile, setImportFile] = useState<FolderFileSummary | null>(null)
  const [error, setError] = useState<string | null>(null)

  const tree = useFolderTree(QPAC_PROJECT_ID)
  const detail = useFolderDetail(selectedId)
  const createFolder = useCreateFolder(QPAC_PROJECT_ID)
  const setTarget = useSetFolderTarget(QPAC_PROJECT_ID)
  const deleteFile = useDeleteFile(QPAC_PROJECT_ID)

  // Select the first folder once the tree arrives, so the pane is never empty.
  useEffect(() => {
    if (selectedId === null && tree.data?.length) setSelectedId(tree.data[0].id)
  }, [tree.data, selectedId])

  const breadcrumb = useMemo(
    () => (detail.data ? detail.data.folder.path.split('/').filter(Boolean) : []),
    [detail.data],
  )

  const onCreateFolder = async () => {
    const name = window.prompt('Folder name')?.trim()
    if (!name) return
    setError(null)
    try {
      await createFolder.mutateAsync({ parentId: selectedId, name })
    } catch (caught) {
      setError(apiErrorMessage(caught, 'Could not create the folder'))
    }
  }

  const onChangeTarget = async (target: DataTarget) => {
    if (!selectedId) return
    setError(null)
    try {
      await setTarget.mutateAsync({ folderId: selectedId, target })
    } catch (caught) {
      setError(apiErrorMessage(caught, 'Could not change the target'))
    }
  }

  const onDeleteFile = async (file: FolderFileSummary) => {
    if (!selectedId) return
    if (!window.confirm(`Delete ${file.name}? Imported rows are not removed.`)) return
    setError(null)
    try {
      await deleteFile.mutateAsync({ fileId: file.id, folderId: selectedId })
    } catch (caught) {
      setError(apiErrorMessage(caught, 'Could not delete the file'))
    }
  }

  const folder = detail.data?.folder

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">TIDPs</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Folders, workbooks and imports. A folder's target decides whether an import
            writes straight to Live or into a draft for review.
          </p>
        </div>

        <div className="flex gap-2">
          {canManage ? (
            <Button variant="outline" size="sm" onClick={() => void onCreateFolder()}>
              <FolderPlus className="h-4 w-4" aria-hidden />
              New folder
            </Button>
          ) : null}
          <Button
            variant="outline"
            size="sm"
            onClick={() => void tree.refetch()}
            disabled={tree.isFetching}
          >
            <RefreshCw className="h-4 w-4" aria-hidden />
            Refresh
          </Button>
        </div>
      </div>

      {error ? (
        <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </p>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-[18rem_1fr]">
        <Card className="h-fit">
          <div className="border-b border-border px-4 py-3 text-xs font-semibold uppercase text-muted-foreground">
            Folders
          </div>
          <div className="p-2">
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
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-5 py-3">
            <div className="min-w-0">
              <nav aria-label="Breadcrumb" className="text-xs text-muted-foreground">
                {breadcrumb.length ? breadcrumb.join('  ›  ') : '—'}
              </nav>
              <h2 className="mt-1 flex items-center gap-2 text-sm font-semibold">
                {folder?.name ?? 'Select a folder'}
                {folder ? <TargetBadge target={folder.target} /> : null}
              </h2>
            </div>

            {folder ? (
              <div className="flex items-center gap-2">
                {canAssignTarget ? (
                  <label className="flex items-center gap-2 text-xs text-muted-foreground">
                    Target
                    <Select
                      className="h-8 w-28"
                      aria-label="Folder target"
                      value={folder.target}
                      disabled={setTarget.isPending}
                      onChange={(event) => void onChangeTarget(event.target.value as DataTarget)}
                    >
                      <option value="Live">Live</option>
                      <option value="Draft">Draft</option>
                    </Select>
                  </label>
                ) : null}

                {canManage ? (
                  <Button size="sm" onClick={() => setUploadOpen(true)}>
                    <Upload className="h-4 w-4" aria-hidden />
                    Upload
                  </Button>
                ) : null}
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
                onDelete={(file) => void onDeleteFile(file)}
              />
            ) : null}
          </CardBody>
        </Card>
      </div>

      {folder ? (
        <UploadDialog
          open={uploadOpen}
          onClose={() => setUploadOpen(false)}
          folderId={folder.id}
          folderName={folder.name}
          projectId={QPAC_PROJECT_ID}
        />
      ) : null}

      {folder ? (
        <ImportDialog
          open={importFile !== null}
          onClose={() => setImportFile(null)}
          file={importFile}
          folderId={folder.id}
          folderTarget={folder.target}
          projectId={QPAC_PROJECT_ID}
        />
      ) : null}
    </div>
  )
}
