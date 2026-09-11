import { useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Upload } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Spinner } from '@/shared/ui/spinner'
import { InitialUploadState } from '@/shared/ui/InitialUploadState'
import { ReplaceDialog } from '@/shared/ui/ReplaceDialog'
import { useAuth } from '@/shared/auth/useAuth'
import { Permissions } from '@/shared/auth/permissions'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import { apiErrorMessage } from '@/shared/api/client'
import { formatDate, formatNumber } from '@/shared/lib/utils'
import { Breadcrumb, type Crumb } from './Breadcrumb'
import { DisciplineSidebar } from './DisciplineSidebar'
import { IconGrid, type GridItem } from './IconGrid'
import {
  useDeleteTidp, useDisciplines, useReplacePreview, useReplaceTidp, useTidpFiles, useUploadTidp,
} from './api'

// Two levels: disciplines, then the files inside one. There is no Drive root and no tree
// of arbitrary depth any more, because there is no Drive — a TIDP belongs to a
// discipline and that is the whole hierarchy.
export function TidpExplorerPage() {
  const navigate = useNavigate()
  const { can } = useAuth()
  const canUpload = can(Permissions.filesManage)

  const disciplines = useDisciplines(QPAC_PROJECT_ID)
  const files = useTidpFiles(QPAC_PROJECT_ID)
  const upload = useUploadTidp(QPAC_PROJECT_ID)
  const replace = useReplaceTidp()
  const remove = useDeleteTidp()

  const [disciplineId, setDisciplineId] = useState<string | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [pending, setPending] = useState<{ id: string; action: 'replace' | 'delete' } | null>(null)

  const preview = useReplacePreview(pending?.id ?? null)
  const replacement = useRef<File | null>(null)
  const uploadInput = useRef<HTMLInputElement>(null)
  const replaceInput = useRef<HTMLInputElement>(null)

  const current = disciplines.data?.find((d) => d.id === disciplineId) ?? null

  const items: GridItem[] = useMemo(() => {
    if (disciplineId === null) {
      return (disciplines.data ?? []).map((discipline) => ({
        id: discipline.id,
        name: discipline.corporateName,
        detail: `${formatNumber(discipline.fileCount)} ${discipline.fileCount === 1 ? 'file' : 'files'}`,
        kind: 'folder' as const,
      }))
    }

    return (files.data ?? [])
      .filter((file) => file.disciplineId === disciplineId)
      .map((file) => ({
        id: file.id,
        name: file.fileName,
        detail: `${formatNumber(file.documentCount)} rows`,
        secondary: formatDate(file.uploadedAt),
        kind: 'file' as const,
        state:
          file.status === 'Failed' ? ('failed' as const)
            : file.status === 'Importing' ? ('importing' as const)
              : ('ok' as const),
      }))
  }, [disciplineId, disciplines.data, files.data])

  const crumbs: Crumb[] = disciplineId === null
    ? [{ id: null, label: 'TIDPs' }]
    : [{ id: null, label: 'TIDPs' }, { id: disciplineId, label: current?.corporateName ?? '' }]

  function open(id: string) {
    if (disciplineId === null) {
      setDisciplineId(id)
      setSelectedId(null)
    } else {
      navigate(`/tidps/${id}`)
    }
  }

  async function confirm() {
    if (!pending) return
    if (pending.action === 'delete') {
      await remove.mutateAsync(pending.id)
    } else if (replacement.current) {
      await replace.mutateAsync({ tidpFileId: pending.id, file: replacement.current })
    }
    replacement.current = null
    setPending(null)
  }

  if (disciplines.isPending || files.isPending) return <Spinner />

  const hasAnyFile = (files.data?.length ?? 0) > 0

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Breadcrumb
          crumbs={crumbs}
          onNavigate={(id) => {
            setDisciplineId(id)
            setSelectedId(null)
          }}
        />

        {canUpload ? (
          <Button onClick={() => uploadInput.current?.click()} disabled={upload.isPending}>
            <Upload className="h-4 w-4" aria-hidden />
            {upload.isPending ? 'Uploading…' : 'Upload TIDP'}
          </Button>
        ) : null}
      </div>

      <input
        ref={uploadInput}
        type="file"
        accept=".xlsx"
        className="hidden"
        onChange={(event) => {
          const file = event.target.files?.[0]
          if (file) void upload.mutateAsync(file)
          event.target.value = ''
        }}
      />

      <input
        ref={replaceInput}
        type="file"
        accept=".xlsx"
        className="hidden"
        onChange={(event) => {
          const file = event.target.files?.[0]
          if (file) replacement.current = file
          event.target.value = ''
        }}
      />

      {upload.isError ? (
        <p className="text-sm text-destructive">{apiErrorMessage(upload.error)}</p>
      ) : null}

      {!hasAnyFile ? (
        <InitialUploadState
          title="No TIDP workbook yet"
          description="Upload one TIDP workbook per discipline. Its rows become the register, and engineers edit them here from then on."
          canUpload={canUpload}
          pending={upload.isPending}
          onSelect={(file) => void upload.mutateAsync(file)}
        />
      ) : (
        <div className="grid gap-4 md:grid-cols-[13rem_minmax(0,1fr)]">
          <aside className="hidden md:block">
            <DisciplineSidebar
              disciplines={disciplines.data ?? []}
              selectedId={disciplineId}
              onSelect={(id) => {
                setDisciplineId(id)
                setSelectedId(null)
              }}
            />
          </aside>

          <section className="min-w-0 rounded-md border border-border bg-card">
            <IconGrid
              items={items}
              selectedId={selectedId}
              onSelect={setSelectedId}
              onOpen={open}
              emptyMessage={
                disciplineId === null
                  ? 'This project has no disciplines yet.'
                  : 'No TIDP has been uploaded for this discipline.'
              }
            />

            {canUpload && disciplineId !== null && selectedId !== null ? (
              <div className="flex gap-2 border-t border-border px-3 py-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    setPending({ id: selectedId, action: 'replace' })
                    replaceInput.current?.click()
                  }}
                >
                  Replace
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPending({ id: selectedId, action: 'delete' })}
                >
                  Delete
                </Button>
                <Button variant="outline" size="sm" onClick={() => open(selectedId)}>
                  Open
                </Button>
              </div>
            ) : null}
          </section>
        </div>
      )}

      <ReplaceDialog
        open={pending !== null}
        action={pending?.action ?? 'replace'}
        preview={preview.data ?? null}
        pending={replace.isPending || remove.isPending}
        onCancel={() => {
          replacement.current = null
          setPending(null)
        }}
        onConfirm={confirm}
      />
    </div>
  )
}
