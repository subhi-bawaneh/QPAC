import { useState } from 'react'
import { useQueries } from '@tanstack/react-query'
import { Dialog } from '@/shared/ui/dialog'
import { Button } from '@/shared/ui/button'
import { Spinner } from '@/shared/ui/spinner'
import { api, apiErrorMessage } from '@/shared/api/client'
import { formatNumber } from '@/shared/lib/utils'
import type {
  ConvertToLiveResult, FolderFileSummary, FolderNode, PromoteDiff,
} from '@/shared/api/types'
import { useConvertToLive } from './api'

// Converting rewrites the Live layer for every file below the folder, so the counts
// are shown before the button is armed, and the result table afterwards.
export function ConvertToLiveDialog({ open, onClose, folder, files, projectId }: {
  open: boolean
  onClose: () => void
  folder: FolderNode
  files: FolderFileSummary[]
  projectId: string
}) {
  const [result, setResult] = useState<ConvertToLiveResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const convert = useConvertToLive(projectId)

  const diffs = useQueries({
    queries: files.map((file) => ({
      queryKey: ['drafts', 'diff', file.id],
      enabled: open && result === null,
      retry: false,
      queryFn: async () => {
        const { data } = await api.get<PromoteDiff>(
          `/api/drafts/promote-diff?folderFileId=${file.id}`)
        return data
      },
    })),
  })

  const loading = diffs.some((query) => query.isLoading)
  const previews = files
    .map((file, index) => ({ file, diff: diffs[index]?.data }))
    .filter((row): row is { file: FolderFileSummary; diff: PromoteDiff } => row.diff !== undefined)

  const run = async () => {
    setError(null)
    try {
      setResult(await convert.mutateAsync(folder.id))
    } catch (caught) {
      setError(apiErrorMessage(caught, 'Could not convert this folder'))
    }
  }

  const close = () => {
    setResult(null)
    setError(null)
    onClose()
  }

  return (
    <Dialog
      open={open}
      onClose={close}
      title={`Convert ${folder.name} to Live`}
      description="Every draft below this folder is promoted, then the folder switches to the Live layer. Drive keeps refreshing the drafts afterwards."
    >
      <div className="space-y-4">
        {result === null && loading ? <Spinner /> : null}

        {result === null && !loading ? (
          previews.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No file below this folder has a draft to promote.
            </p>
          ) : (
            <CountsTable
              rows={previews.map(({ file, diff }) => ({
                name: file.name,
                added: diff.added,
                updated: diff.modified,
                deleted: diff.deleted,
                conflicts: diff.conflicts,
              }))}
            />
          )
        ) : null}

        {result ? (
          <>
            <p className="text-sm">
              {formatNumber(result.filesConverted)} file(s) converted:
              {' '}{formatNumber(result.added)} added, {formatNumber(result.updated)} updated,
              {' '}{formatNumber(result.deleted)} deleted, {formatNumber(result.conflicts)} conflicts.
            </p>
            <CountsTable
              rows={result.perFile.map((row) => ({
                name: row.name,
                added: row.added,
                updated: row.updated,
                deleted: row.deleted,
                conflicts: row.conflicts,
              }))}
            />
          </>
        ) : null}

        {error ? (
          <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {error}
          </p>
        ) : null}

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={close}>{result ? 'Close' : 'Cancel'}</Button>
          {result === null ? (
            <Button onClick={() => void run()} disabled={convert.isPending || loading}>
              {convert.isPending ? 'Converting…' : 'Convert to Live'}
            </Button>
          ) : null}
        </div>
      </div>
    </Dialog>
  )
}

function CountsTable({ rows }: {
  rows: { name: string; added: number; updated: number; deleted: number; conflicts: number }[]
}) {
  return (
    <div className="max-h-64 overflow-y-auto rounded-md border border-border">
      <table className="w-full text-sm">
        <thead className="border-b border-border text-left text-xs uppercase text-muted-foreground">
          <tr>
            <th className="px-3 py-2 font-medium">File</th>
            <th className="px-3 py-2 text-right font-medium">Added</th>
            <th className="px-3 py-2 text-right font-medium">Updated</th>
            <th className="px-3 py-2 text-right font-medium">Deleted</th>
            <th className="px-3 py-2 text-right font-medium">Conflicts</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.name} className="border-b border-border last:border-0">
              <td className="px-3 py-2">{row.name}</td>
              <td className="px-3 py-2 text-right tabular-nums">{formatNumber(row.added)}</td>
              <td className="px-3 py-2 text-right tabular-nums">{formatNumber(row.updated)}</td>
              <td className="px-3 py-2 text-right tabular-nums">{formatNumber(row.deleted)}</td>
              <td className="px-3 py-2 text-right tabular-nums">{formatNumber(row.conflicts)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
