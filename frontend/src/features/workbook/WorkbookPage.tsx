import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, Download } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Card } from '@/shared/ui/card'
import { Badge } from '@/shared/ui/badge'
import { Spinner } from '@/shared/ui/spinner'
import { api, apiErrorMessage } from '@/shared/api/client'
import { formatDate } from '@/shared/lib/utils'
import { useAuth } from '@/shared/auth/useAuth'
import { Permissions } from '@/shared/auth/permissions'
import type { WorkbookRow } from '@/shared/api/types'
import { Grid, type ActiveCell } from './Grid'
import { FormulaBar } from './FormulaBar'
import { SheetTabs } from './SheetTabs'
import { StatusBar } from './StatusBar'
import { cellAddress } from './columns'
import { useSaveRow, useWorkbook } from './api'

export function WorkbookPage() {
  const { fileId = '' } = useParams()
  const { can } = useAuth()

  const [sheet, setSheet] = useState('TIDP')
  const [active, setActive] = useState<ActiveCell>({ row: 0, column: 0 })
  const [saveError, setSaveError] = useState<string | null>(null)

  const workbook = useWorkbook(fileId, sheet)
  const first = workbook.data?.pages[0]
  const layer = first?.layer ?? 'Live'

  const canEdit = layer === 'Draft' ? can(Permissions.draftsEdit) : can(Permissions.documentsEditLive)
  const save = useSaveRow(fileId, sheet, layer)

  const rows: WorkbookRow[] = useMemo(
    () => workbook.data?.pages.flatMap((page) => page.sheet.rows) ?? [],
    [workbook.data],
  )

  const columns = first?.sheet.columns ?? []
  const activeRow = rows[active.row]
  const activeValue = activeRow?.cells[active.column] ?? null

  // The API names the document sheet TIDP or MIDP; the tab strip follows it.
  const sheets = first?.sheets ?? []
  const currentSheet = first?.sheet.name ?? sheet

  const onSave = async (row: WorkbookRow, columnIndex: number, value: string) => {
    setSaveError(null)
    const cells = [...row.cells]
    cells[columnIndex] = value
    try {
      await save.mutateAsync({ rowId: row.rowId, columns, cells })
    } catch (caught) {
      setSaveError(apiErrorMessage(caught, 'The cell could not be saved'))
      throw caught
    }
  }

  const download = async () => {
    if (!first) return
    const response = await api.get(`/api/folder-files/${fileId}/download`, { responseType: 'blob' })
    const url = URL.createObjectURL(response.data as Blob)
    const link = document.createElement('a')
    link.href = url
    link.download = first.fileName
    link.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="truncate text-lg font-semibold">{first?.fileName ?? 'Workbook'}</h1>
            <Badge tone={layer === 'Draft' ? 'warning' : 'info'}>
              {layer === 'Draft' ? 'DB1 Draft' : 'DB2 Live'}
            </Badge>
            {first?.hasNewerDraft ? <Badge tone="warning">Drive has newer data</Badge> : null}
          </div>
          {first ? (
            <p className="mt-1 text-sm text-muted-foreground">
              {first.contentSource === 'Drive' ? 'From Google Drive' : 'Uploaded'}
              {' · '}
              {formatDate(first.contentModifiedAt)}
            </p>
          ) : null}
        </div>

        <div className="flex gap-2">
          <Button variant="outline" size="sm" asChild>
            <Link to="/tidps">
              <ArrowLeft aria-hidden />
              Back
            </Link>
          </Button>
          <Button variant="outline" size="sm" onClick={() => void download()}>
            <Download aria-hidden />
            Download
          </Button>
        </div>
      </div>

      {workbook.isError ? (
        <p className="text-sm text-destructive">{apiErrorMessage(workbook.error)}</p>
      ) : null}

      <Card className="overflow-hidden">
        <FormulaBar
          address={cellAddress(columns, active.column, activeRow?.rowNumber ?? 0)}
          value={activeValue}
        />

        {first && first.sheet.headerBlock.length > 0 ? (
          <dl className="grid gap-x-6 gap-y-1 border-b border-border px-3 py-2 text-xs sm:grid-cols-3">
            {first.sheet.headerBlock.map((cell) => (
              <div key={cell.label} className="flex gap-2">
                <dt className="w-40 shrink-0 font-medium text-muted-foreground">{cell.label}</dt>
                <dd className="truncate">{cell.value ?? '—'}</dd>
              </div>
            ))}
          </dl>
        ) : null}

        {workbook.isPending ? <Spinner /> : null}

        {first ? (
          <Grid
            columns={columns}
            rows={rows}
            canEdit={canEdit && currentSheet !== 'Baseline' && currentSheet !== 'Picklists'}
            saving={save.isPending}
            saveError={saveError}
            onActiveChange={setActive}
            onSave={onSave}
            onClearError={() => setSaveError(null)}
            onScrollEnd={() => {
              if (workbook.hasNextPage && !workbook.isFetchingNextPage) void workbook.fetchNextPage()
            }}
          />
        ) : null}

        <SheetTabs sheets={sheets} active={currentSheet} onChange={setSheet} />

        <StatusBar
          totalRows={first?.sheet.totalRows ?? 0}
          loadedRows={rows.length}
          layer={layer}
          lastImportedAt={null}
          loadingMore={workbook.isFetchingNextPage}
        />
      </Card>
    </div>
  )
}
