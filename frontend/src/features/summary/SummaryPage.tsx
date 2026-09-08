import { useState } from 'react'
import { RefreshCw } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Card, CardBody, CardHeader, StatTile } from '@/shared/ui/card'
import { Spinner } from '@/shared/ui/spinner'
import { Tabs, TabPanel } from '@/shared/ui/tabs'
import { Badge } from '@/shared/ui/badge'
import { apiErrorMessage } from '@/shared/api/client'
import { formatDate, formatNumber } from '@/shared/lib/utils'
import { useAuth } from '@/shared/auth/useAuth'
import { Permissions } from '@/shared/auth/permissions'
import { SCurveChart } from '@/features/reports/SCurveChart'
import { SpiChart } from '@/features/reports/SpiChart'
import { spiLabel, spiTone } from '@/features/reports/chartTheme'
import {
  useBaselineSummary, useCorporateSummary, useEvmSummary, useRecalculate,
  type EvmRow, type SummaryGroup,
} from '@/features/reports/api'

const percent = (value: number | null) =>
  value === null ? '—' : `${(value * 100).toFixed(1)}%`

export function SummaryPage() {
  const [tab, setTab] = useState('overview')
  const { can } = useAuth()

  const corporate = useCorporateSummary()
  const baseline = useBaselineSummary()
  const evm = useEvmSummary()
  const recalculate = useRecalculate()

  const stale = corporate.data?.recalculationRequired ?? false

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold">Summary</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            {corporate.data
              ? `Reported as at ${formatDate(corporate.data.summary.reportDate)}.`
              : 'Progress, quality and schedule performance across the project.'}
          </p>
        </div>

        {stale && can(Permissions.importRun) ? (
          <Button size="sm" onClick={() => recalculate.mutate()} disabled={recalculate.isPending}>
            <RefreshCw className={`h-4 w-4 ${recalculate.isPending ? 'animate-spin' : ''}`} aria-hidden />
            {recalculate.isPending ? 'Recalculating…' : 'Recalculate'}
          </Button>
        ) : null}
      </div>

      {stale ? (
        <p className="rounded-md bg-amber-100 px-3 py-2 text-sm text-amber-900 dark:bg-amber-900/40 dark:text-amber-100">
          {formatNumber(corporate.data?.documentsWithoutSnapshot ?? 0)} document(s) have not been
          calculated since the last import, so these numbers are behind.
        </p>
      ) : null}

      {recalculate.isError ? (
        <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {apiErrorMessage(recalculate.error)}
        </p>
      ) : null}

      <Tabs
        active={tab}
        onChange={setTab}
        items={[
          { id: 'overview', label: 'Overview' },
          { id: 'corporate', label: 'Corporate' },
          { id: 'baseline', label: 'Baseline' },
          { id: 'evm', label: 'EVM' },
        ]}
      />

      <TabPanel id="overview" active={tab}>
        {corporate.isPending ? <Spinner /> : null}
        {corporate.isError ? (
          <p className="text-sm text-destructive">{apiErrorMessage(corporate.error)}</p>
        ) : null}

        {corporate.data ? (
          <div className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <StatTile label="Documents" value={formatNumber(corporate.data.summary.total.total)} />
              <StatTile label="Submitted" value={formatNumber(corporate.data.summary.total.submitted)}
                hint={percent(corporate.data.summary.total.submitted / Math.max(corporate.data.summary.total.total, 1))} />
              <StatTile label="Approved" value={formatNumber(corporate.data.summary.total.approved)}
                hint={`Quality ${percent(corporate.data.summary.total.quality)}`} />
              <StatTile label="Completed" value={percent(corporate.data.summary.total.completedPercent)}
                hint={`Planned ${percent(corporate.data.summary.total.plannedPercent)}`} />
            </div>

            <Card>
              <CardHeader title="Delivery curve" description="Cumulative planned, submitted and approved." />
              <CardBody>
                <SCurveChart
                  weeks={corporate.data.summary.weeks}
                  currentWeek={corporate.data.summary.currentWeek}
                />
              </CardBody>
            </Card>
          </div>
        ) : null}
      </TabPanel>

      <TabPanel id="corporate" active={tab}>
        {corporate.data ? (
          <div className="space-y-4">
            <GroupTable
              title="By discipline"
              rows={[corporate.data.summary.total, ...corporate.data.summary.disciplines]}
            />
            <GroupTable title="By author" rows={corporate.data.summary.authors} />
          </div>
        ) : <Spinner />}
      </TabPanel>

      <TabPanel id="baseline" active={tab}>
        {baseline.isPending ? <Spinner /> : null}
        {baseline.isError ? (
          <p className="text-sm text-destructive">{apiErrorMessage(baseline.error)}</p>
        ) : null}

        {baseline.data ? (
          <div className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              {baseline.data.summary.packageStatuses.map((status) => (
                <StatTile
                  key={status.status}
                  label={`${status.status} packages`}
                  value={formatNumber(status.packages)}
                  hint={`${formatNumber(status.drawings)} drawings`}
                />
              ))}
            </div>

            <Card>
              <CardHeader
                title="By discipline"
                description={`${formatNumber(baseline.data.summary.totalPackages)} packages carrying ${formatNumber(baseline.data.summary.totalPackageDrawings)} drawings.`}
              />
              <CardBody className="p-0">
                <table className="w-full text-sm">
                  <thead className="border-b border-border text-left text-xs uppercase text-muted-foreground">
                    <tr>
                      <th className="px-5 py-2 font-medium">Discipline</th>
                      <th className="px-5 py-2 text-right font-medium">Total</th>
                      <th className="px-5 py-2 text-right font-medium">Submitted</th>
                      <th className="px-5 py-2 text-right font-medium">Approved</th>
                      <th className="px-5 py-2 text-right font-medium">C — Revise</th>
                      <th className="px-5 py-2 text-right font-medium">D — Rejected</th>
                      <th className="px-5 py-2 text-right font-medium">Under review</th>
                    </tr>
                  </thead>
                  <tbody>
                    {[baseline.data.summary.total, ...baseline.data.summary.disciplines].map((row, index) => (
                      <tr key={row.name} className={`border-b border-border last:border-0 ${index === 0 ? 'font-medium' : ''}`}>
                        <td className="px-5 py-2">{row.name}</td>
                        <td className="px-5 py-2 text-right tabular-nums">{formatNumber(row.total)}</td>
                        <td className="px-5 py-2 text-right tabular-nums">{formatNumber(row.submitted)}</td>
                        <td className="px-5 py-2 text-right tabular-nums">{formatNumber(row.approved)}</td>
                        <td className="px-5 py-2 text-right tabular-nums">{formatNumber(row.cRevise)}</td>
                        <td className="px-5 py-2 text-right tabular-nums">{formatNumber(row.dRejected)}</td>
                        <td className="px-5 py-2 text-right tabular-nums">{formatNumber(row.underReview)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </CardBody>
            </Card>
          </div>
        ) : null}
      </TabPanel>


      <TabPanel id="evm" active={tab}>
        {evm.isPending ? <Spinner /> : null}
        {evm.isError ? <p className="text-sm text-destructive">{apiErrorMessage(evm.error)}</p> : null}

        {evm.data ? (
          <div className="space-y-4">
            <Card>
              <CardHeader
                title="Schedule performance"
                description="CPI needs actual hours, which nothing records yet, so only SPI is shown."
              />
              <CardBody>
                <SpiChart rows={evm.data.summary.disciplines} />
              </CardBody>
            </Card>

            <Card>
              <CardBody className="p-0">
                <table className="w-full text-sm">
                  <thead className="border-b border-border text-left text-xs uppercase text-muted-foreground">
                    <tr>
                      <th className="px-5 py-2 font-medium">Group</th>
                      <th className="px-5 py-2 text-right font-medium">Documents</th>
                      <th className="px-5 py-2 text-right font-medium">PV</th>
                      <th className="px-5 py-2 text-right font-medium">EV</th>
                      <th className="px-5 py-2 text-right font-medium">BAC</th>
                      <th className="px-5 py-2 text-right font-medium">SPI</th>
                      <th className="px-5 py-2 font-medium">Assessment</th>
                    </tr>
                  </thead>
                  <tbody>
                    {[evm.data.summary.total, ...evm.data.summary.disciplines].map((row, index) => (
                      <EvmTableRow key={row.name} row={row} emphasise={index === 0} />
                    ))}
                  </tbody>
                </table>
              </CardBody>
            </Card>
          </div>
        ) : null}
      </TabPanel>
    </div>
  )
}

function EvmTableRow({ row, emphasise }: { row: EvmRow; emphasise: boolean }) {
  const tone = spiTone(row.schedulePerformanceIndex)
  const badgeTone = { good: 'success', warning: 'warning', critical: 'danger', none: 'neutral' } as const

  return (
    <tr className={`border-b border-border last:border-0 ${emphasise ? 'font-medium' : ''}`}>
      <td className="px-5 py-2">{row.name}</td>
      <td className="px-5 py-2 text-right tabular-nums">{formatNumber(row.documents)}</td>
      <td className="px-5 py-2 text-right tabular-nums">{row.plannedValue.toFixed(1)}</td>
      <td className="px-5 py-2 text-right tabular-nums">{row.earnedValue.toFixed(1)}</td>
      <td className="px-5 py-2 text-right tabular-nums">{row.budgetAtCompletion.toFixed(0)}</td>
      <td className="px-5 py-2 text-right tabular-nums">
        {row.schedulePerformanceIndex === null ? '—' : row.schedulePerformanceIndex.toFixed(3)}
      </td>
      <td className="px-5 py-2">
        {/* The chart colours by the same bands; the words carry the meaning here. */}
        <Badge tone={badgeTone[tone]}>{spiLabel[tone]}</Badge>
      </td>
    </tr>
  )
}

function GroupTable({ title, rows }: { title: string; rows: SummaryGroup[] }) {
  return (
    <Card>
      <CardHeader title={title} description="Progress, quality and value at the current week." />
      <CardBody className="p-0">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="border-b border-border text-left text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-4 py-2 font-medium">Group</th>
                <th className="px-4 py-2 text-right font-medium">Total</th>
                <th className="px-4 py-2 text-right font-medium">Planned</th>
                <th className="px-4 py-2 text-right font-medium">Submitted</th>
                <th className="px-4 py-2 text-right font-medium">Approved</th>
                <th className="px-4 py-2 text-right font-medium">Rejected</th>
                <th className="px-4 py-2 text-right font-medium">Under review</th>
                <th className="px-4 py-2 text-right font-medium">Revisions</th>
                <th className="px-4 py-2 text-right font-medium">Quality</th>
                <th className="px-4 py-2 text-right font-medium">Planned %</th>
                <th className="px-4 py-2 text-right font-medium">Completed %</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row, index) => (
                <tr key={row.name} className={`border-b border-border last:border-0 ${index === 0 && row.name === 'Total' ? 'font-medium' : ''}`}>
                  <td className="px-4 py-2">{row.name}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(row.total)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(row.planned)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(row.submitted)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(row.approved)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(row.rejected)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(row.underReview)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(row.totalRevisions)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{percent(row.quality)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{percent(row.plannedPercent)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{percent(row.completedPercent)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </CardBody>
    </Card>
  )
}
