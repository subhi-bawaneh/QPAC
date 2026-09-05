import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Download } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Card, CardBody } from '@/shared/ui/card'
import { Spinner } from '@/shared/ui/spinner'
import { Tabs, TabPanel } from '@/shared/ui/tabs'
import { Badge } from '@/shared/ui/badge'
import { api, apiErrorMessage } from '@/shared/api/client'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import { formatDate, formatNumber } from '@/shared/lib/utils'
import { useAuth } from '@/shared/auth/useAuth'
import { Permissions } from '@/shared/auth/permissions'
import { downloadReport } from '@/features/tracker/api'
import { StatusBadge } from '@/features/tracker/StatusBadge'
import type { UnifiedStatus } from '@/shared/api/types'

interface DeliveredButUnplanned {
  documentNumber: string
  revision: string
  title: string
  aconexStatus: string
  status: UnifiedStatus | null
  dateModified: string
}

interface UnplannedDocument {
  type: string
  discipline: string
  documentNumber: string
  title: string
  plannedStart: string | null
  author: string | null
}

interface UnusedPackage {
  package: string
  activityCode: string
  originalDuration: number
  finish: string
  documentCount: number
}

interface DuplicateDocument {
  type: string
  discipline: string
  documentNumber: string
  title: string
  plannedStart: string | null
  author: string | null
  count: number
}

interface ControlFindings {
  deliveredButUnplanned: DeliveredButUnplanned[]
  unplanned: UnplannedDocument[]
  unusedPackages: UnusedPackage[]
  duplicates: DuplicateDocument[]
}

export function ControlFindingsPage() {
  const [tab, setTab] = useState('delivered')
  const [exportError, setExportError] = useState<string | null>(null)
  const { can } = useAuth()

  const findings = useQuery({
    queryKey: ['findings', QPAC_PROJECT_ID],
    queryFn: async () => {
      const { data } = await api.get<{
        findings: ControlFindings
        recalculationRequired: boolean
      }>(`/api/projects/${QPAC_PROJECT_ID}/control-findings`)
      return data
    },
  })

  const data = findings.data?.findings

  const onExport = async () => {
    setExportError(null)
    try {
      await downloadReport('ControlFindings')
    } catch (caught) {
      setExportError(apiErrorMessage(caught, 'The export failed'))
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold">Control Findings</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Four reports of things that do not add up: work delivered that nobody planned,
            work planned against no baseline, packages nothing was assigned to, and numbers
            used twice.
          </p>
        </div>

        {can(Permissions.reportsExport) ? (
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

      {findings.isPending ? <Spinner /> : null}
      {findings.isError ? (
        <p className="text-sm text-destructive">{apiErrorMessage(findings.error)}</p>
      ) : null}

      {data ? (
        <>
          <Tabs
            active={tab}
            onChange={setTab}
            items={[
              { id: 'delivered', label: 'Delivered but unplanned', count: data.deliveredButUnplanned.length, tone: 'danger' },
              { id: 'unplanned', label: 'Unplanned in MIDP', count: data.unplanned.length, tone: 'danger' },
              { id: 'packages', label: 'Unused packages', count: data.unusedPackages.length },
              { id: 'duplicates', label: 'Duplicate numbers', count: data.duplicates.length, tone: 'danger' },
            ]}
          />

          <TabPanel id="delivered" active={tab}>
            <FindingsCard
              explanation="An Aconex submission whose document number is in no TIDP or MIDP — someone delivered something nobody planned."
              empty="Every submission matches a planned document."
              rows={data.deliveredButUnplanned}
              headers={['Document No', 'Rev', 'Title', 'Aconex status', 'Status', 'Modified']}
              render={(row) => (
                <>
                  <td className="px-4 py-2 font-mono text-xs">{row.documentNumber}</td>
                  <td className="px-4 py-2">{row.revision}</td>
                  <td className="max-w-sm truncate px-4 py-2">{row.title}</td>
                  <td className="px-4 py-2 text-muted-foreground">{row.aconexStatus}</td>
                  <td className="px-4 py-2"><StatusBadge status={row.status} /></td>
                  <td className="px-4 py-2 text-muted-foreground">{formatDate(row.dateModified)}</td>
                </>
              )}
            />
          </TabPanel>

          <TabPanel id="unplanned" active={tab}>
            <FindingsCard
              explanation="A planned document with no Planned Start: its Activity ID matches no baseline activity, or it has none."
              empty="Every document is tied to the baseline."
              rows={data.unplanned}
              headers={['Type', 'Discipline', 'Document No', 'Title', 'Author']}
              render={(row) => (
                <>
                  <td className="px-4 py-2">{row.type}</td>
                  <td className="px-4 py-2">{row.discipline}</td>
                  <td className="px-4 py-2 font-mono text-xs">{row.documentNumber}</td>
                  <td className="max-w-sm truncate px-4 py-2">{row.title}</td>
                  <td className="px-4 py-2 text-muted-foreground">{row.author ?? '—'}</td>
                </>
              )}
            />
          </TabPanel>

          <TabPanel id="packages" active={tab}>
            <FindingsCard
              explanation="A Submittal activity in the baseline that no document points at — planned work with nothing assigned to it."
              empty="Every baseline package has documents."
              rows={data.unusedPackages}
              headers={['Package', 'Activity code', 'Duration', 'Finish']}
              render={(row) => (
                <>
                  <td className="max-w-md truncate px-4 py-2">{row.package}</td>
                  <td className="px-4 py-2 font-mono text-xs">{row.activityCode}</td>
                  <td className="px-4 py-2 tabular-nums">{row.originalDuration}</td>
                  <td className="px-4 py-2 text-muted-foreground">{formatDate(row.finish)}</td>
                </>
              )}
            />
          </TabPanel>

          <TabPanel id="duplicates" active={tab}>
            <FindingsCard
              explanation="Every row sharing a document number with another. The Live layer forbids duplicates, so these come from drafts or from data imported before that rule applied."
              empty="No document number is used twice."
              rows={data.duplicates}
              headers={['Document No', 'Discipline', 'Title', 'Author', 'Rows']}
              render={(row) => (
                <>
                  <td className="px-4 py-2 font-mono text-xs">{row.documentNumber}</td>
                  <td className="px-4 py-2">{row.discipline}</td>
                  <td className="max-w-sm truncate px-4 py-2">{row.title}</td>
                  <td className="px-4 py-2 text-muted-foreground">{row.author ?? '—'}</td>
                  <td className="px-4 py-2"><Badge tone="danger">{row.count}</Badge></td>
                </>
              )}
            />
          </TabPanel>
        </>
      ) : null}
    </div>
  )
}

const MAX_ROWS = 200

function FindingsCard<T>({ explanation, empty, rows, headers, render }: {
  explanation: string
  empty: string
  rows: T[]
  headers: string[]
  render: (row: T) => React.ReactNode
}) {
  return (
    <Card>
      <div className="border-b border-border px-5 py-3">
        <p className="text-sm text-muted-foreground">{explanation}</p>
      </div>

      <CardBody className="p-0">
        {rows.length === 0 ? (
          <p className="px-5 py-6 text-sm text-muted-foreground">{empty}</p>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="border-b border-border text-left text-xs uppercase text-muted-foreground">
                  <tr>
                    {headers.map((header) => (
                      <th key={header} className="whitespace-nowrap px-4 py-2 font-medium">{header}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {rows.slice(0, MAX_ROWS).map((row, index) => (
                    <tr key={index} className="border-b border-border last:border-0">{render(row)}</tr>
                  ))}
                </tbody>
              </table>
            </div>

            {rows.length > MAX_ROWS ? (
              <p className="border-t border-border px-5 py-3 text-xs text-muted-foreground">
                Showing the first {MAX_ROWS} of {formatNumber(rows.length)}. Export for the full list.
              </p>
            ) : null}
          </>
        )}
      </CardBody>
    </Card>
  )
}
