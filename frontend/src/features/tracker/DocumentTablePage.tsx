import { useState, type ReactNode } from 'react'
import { Download } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Card, CardBody } from '@/shared/ui/card'
import { Input } from '@/shared/ui/input'
import { Select } from '@/shared/ui/select'
import { Spinner } from '@/shared/ui/spinner'
import { apiErrorMessage } from '@/shared/api/client'
import { useAuth } from '@/shared/auth/useAuth'
import { Permissions } from '@/shared/auth/permissions'
import type { TrackerRow, UnifiedStatus } from '@/shared/api/types'
import { downloadReport, useTrackerRows } from './api'
import { DocumentDrawer } from './DocumentDrawer'

const PAGE_SIZE = 25
const statuses: UnifiedStatus[] = ['Approved', 'Rejected', 'UnderReview', 'Withdrawn']

export interface Column {
  key: string
  header: string
  align?: 'right'
  render: (row: TrackerRow) => ReactNode
}

/**
 * The shared document grid. The Tracker and MIDP pages are the same server-paged
 * list of documents shown through different columns, so they share everything but
 * the column set — one filter/paging/drawer implementation, two views.
 */
export function DocumentTablePage({ title, description, columns, exportKind, showStatusFilter }: {
  title: string
  description: string
  columns: Column[]
  exportKind: string
  showStatusFilter: boolean
}) {
  const { can } = useAuth()
  const canExport = can(Permissions.reportsExport)

  const [search, setSearch] = useState('')
  const [discipline, setDiscipline] = useState('')
  const [status, setStatus] = useState<UnifiedStatus | ''>('')
  const [page, setPage] = useState(1)
  const [openDocumentId, setOpenDocumentId] = useState<string | null>(null)
  const [exportError, setExportError] = useState<string | null>(null)

  const rows = useTrackerRows({
    search,
    discipline: discipline || undefined,
    status: status || undefined,
    page,
    pageSize: PAGE_SIZE,
  })

  // The disciplines present on the current page — enough to filter by without a
  // dedicated endpoint, and it grows as the user pages through.
  const disciplines = [...new Set((rows.data?.items ?? []).map((row) => row.discipline))].sort()

  const onExport = async () => {
    setExportError(null)
    try {
      await downloadReport(exportKind)
    } catch (caught) {
      setExportError(apiErrorMessage(caught, 'The export failed'))
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold">{title}</h1>
          <p className="mt-1 text-sm text-muted-foreground">{description}</p>
        </div>

        {canExport ? (
          <Button variant="outline" size="sm" onClick={() => void onExport()}>
            <Download className="h-4 w-4" aria-hidden />
            Export
          </Button>
        ) : null}
      </div>

      {exportError ? (
        <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {exportError}
        </p>
      ) : null}

      <Card>
        <div className="flex flex-wrap items-center gap-3 border-b border-border px-5 py-3">
          <Input
            className="h-8 w-64"
            placeholder="Search number or title"
            aria-label="Search documents"
            value={search}
            onChange={(event) => {
              setSearch(event.target.value)
              setPage(1)
            }}
          />

          <Select
            className="h-8 w-44"
            aria-label="Filter by discipline"
            value={discipline}
            onChange={(event) => {
              setDiscipline(event.target.value)
              setPage(1)
            }}
          >
            <option value="">All disciplines</option>
            {disciplines.map((value) => <option key={value} value={value}>{value}</option>)}
          </Select>

          {showStatusFilter ? (
            <Select
              className="h-8 w-40"
              aria-label="Filter by status"
              value={status}
              onChange={(event) => {
                setStatus(event.target.value as UnifiedStatus | '')
                setPage(1)
              }}
            >
              <option value="">All statuses</option>
              {statuses.map((value) => (
                <option key={value} value={value}>{value === 'UnderReview' ? 'Under Review' : value}</option>
              ))}
            </Select>
          ) : null}

          <span className="ml-auto text-xs text-muted-foreground">
            {rows.data ? `${rows.data.total.toLocaleString('en-GB')} documents` : ''}
          </span>
        </div>

        <CardBody className="p-0">
          {rows.isPending ? <Spinner /> : null}
          {rows.isError ? (
            <p className="px-5 py-4 text-sm text-destructive">{apiErrorMessage(rows.error)}</p>
          ) : null}

          {rows.data && rows.data.items.length === 0 ? (
            <p className="px-5 py-6 text-sm text-muted-foreground">
              No documents match these filters. Import a TIDP or the MIDP to populate this list.
            </p>
          ) : null}

          {rows.data?.items.length ? (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="border-b border-border text-left text-xs uppercase text-muted-foreground">
                  <tr>
                    {columns.map((column) => (
                      <th
                        key={column.key}
                        className={`whitespace-nowrap px-3 py-2 font-medium ${column.align === 'right' ? 'text-right' : ''}`}
                      >
                        {column.header}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {rows.data.items.map((row) => (
                    <tr
                      key={row.documentId}
                      className="cursor-pointer border-b border-border last:border-0 hover:bg-muted"
                      onClick={() => setOpenDocumentId(row.documentId)}
                    >
                      {columns.map((column) => (
                        <td
                          key={column.key}
                          className={`px-3 py-2 ${column.align === 'right' ? 'text-right tabular-nums' : ''}`}
                        >
                          {column.render(row)}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </CardBody>

        {rows.data && rows.data.totalPages > 1 ? (
          <div className="flex items-center justify-between border-t border-border px-5 py-3 text-sm">
            <span className="text-muted-foreground">
              Page {rows.data.page} of {rows.data.totalPages.toLocaleString('en-GB')}
            </span>
            <div className="flex gap-2">
              <Button size="sm" variant="outline" disabled={rows.data.page <= 1}
                onClick={() => setPage((value) => value - 1)}>Previous</Button>
              <Button size="sm" variant="outline" disabled={rows.data.page >= rows.data.totalPages}
                onClick={() => setPage((value) => value + 1)}>Next</Button>
            </div>
          </div>
        ) : null}
      </Card>

      <DocumentDrawer documentId={openDocumentId} onClose={() => setOpenDocumentId(null)} />
    </div>
  )
}
