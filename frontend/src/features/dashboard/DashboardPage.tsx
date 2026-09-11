import { AlertTriangle, ArrowRight, FileSpreadsheet, RefreshCw } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Button } from '@/shared/ui/button'
import { Card, CardBody, CardHeader, StatTile } from '@/shared/ui/card'
import { Badge } from '@/shared/ui/badge'
import { Spinner } from '@/shared/ui/spinner'
import { apiErrorMessage } from '@/shared/api/client'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import { formatDate, formatNumber } from '@/shared/lib/utils'
import { useAuth } from '@/shared/auth/useAuth'
import { Permissions } from '@/shared/auth/permissions'
import type { ImportBatchSummary } from '@/shared/api/types'
import { useTidpFiles } from '@/features/tidps/api'
import { SCurveChart } from '@/features/reports/SCurveChart'
import { SpiChart } from '@/features/reports/SpiChart'
import { ProgressChart } from '@/features/reports/ProgressChart'
import { spiLabel, spiTone } from '@/features/reports/chartTheme'
import {
  useBaselineSummary, useControlFindings, useCorporateSummary, useEvmSummary,
  useRecalculate, useRecentImports,
} from '@/features/reports/api'

const percent = (value: number | null | undefined) =>
  value === null || value === undefined ? '—' : `${(value * 100).toFixed(1)}%`

const ratio = (part: number, whole: number) => (whole > 0 ? part / whole : null)

/**
 * The landing overview: the Engineering Tracker's aggregations (Corporate Summary,
 * Baseline Summary, Control Findings, SPI) computed over the effective document set,
 * plus the state of the Drive-to-database pipeline that feeds them. The detailed
 * tables stay on the Summary and Control Findings pages.
 */
export function DashboardPage() {
  const { can } = useAuth()

  const corporate = useCorporateSummary()
  const baseline = useBaselineSummary()
  const evm = useEvmSummary()
  const findings = useControlFindings()
  const tidpFiles = useTidpFiles(QPAC_PROJECT_ID)
  const imports = useRecentImports()
  const recalculate = useRecalculate()

  const summary = corporate.data?.summary
  const total = summary?.total
  const spi = evm.data?.summary.total.schedulePerformanceIndex ?? null
  const tone = spiTone(spi)
  const badgeTone = { good: 'success', warning: 'warning', critical: 'danger', none: 'neutral' } as const

  const findingCounts = findings.data
    ? [
        { key: 'delivered', label: 'Delivered but unplanned', count: findings.data.findings.deliveredButUnplanned.length },
        { key: 'unplanned', label: 'Unplanned documents', count: findings.data.findings.unplanned.length },
        { key: 'packages', label: 'Unused packages', count: findings.data.findings.unusedPackages.length },
        { key: 'duplicates', label: 'Duplicate numbers', count: findings.data.findings.duplicates.length },
      ]
    : null
  const findingsTotal = findingCounts?.reduce((sum, item) => sum + item.count, 0) ?? null
  const stale = corporate.data?.recalculationRequired ?? false

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold">Dashboard</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            {summary
              ? `Engineering Tracker figures as at ${formatDate(summary.reportDate)}, week of ${formatDate(summary.currentWeek)}.`
              : 'Progress, quality and schedule across every targeted company.'}
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

      {corporate.isError ? (
        <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {apiErrorMessage(corporate.error)}
        </p>
      ) : null}

      {corporate.isPending ? <Spinner label="Loading the tracker figures…" /> : null}

      {total ? (
        <section aria-label="Key figures" className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
          <StatTile label="Documents" value={formatNumber(total.total)} hint={`${formatNumber(total.planned)} planned to date`} />
          <StatTile label="Submitted" value={formatNumber(total.submitted)} hint={percent(ratio(total.submitted, total.total))} />
          <StatTile label="Approved" value={formatNumber(total.approved)} hint={`Quality ${percent(total.quality)}`} />
          <StatTile label="Under review" value={formatNumber(total.underReview)} hint={`${formatNumber(total.rejected)} rejected`} />
          <StatTile label="Completed" value={percent(total.completedPercent)} hint={`Planned ${percent(total.plannedPercent)}`} />
          <StatTile
            label="Schedule (SPI)"
            value={spi === null ? '—' : spi.toFixed(2)}
            hint={spiLabel[tone]}
          />
        </section>
      ) : null}

      <div className="grid gap-4 xl:grid-cols-[2fr_1fr]">
        <Card>
          <CardHeader title="Delivery curve" description="Cumulative planned, submitted and approved documents by week." />
          <CardBody>
            {summary ? (
              <SCurveChart weeks={summary.weeks} currentWeek={summary.currentWeek} />
            ) : (
              <p className="text-sm text-muted-foreground">Nothing imported yet.</p>
            )}
          </CardBody>
        </Card>

        <Card>
          <CardHeader
            title="Source files"
            description="Every workbook the register was built from, and what each upload did."
            action={
              <Button variant="outline" size="sm" asChild>
                <Link to="/tidps">
                  TIDPs
                  <ArrowRight className="h-4 w-4" aria-hidden />
                </Link>
              </Button>
            }
          />
          <CardBody className="space-y-3 text-sm">
            <div className="flex items-center gap-2">
              <FileSpreadsheet className="h-4 w-4 text-muted-foreground" aria-hidden />
              {tidpFiles.data?.some((f) => f.status === 'Failed') ? (
                <Badge tone="danger">An upload failed</Badge>
              ) : tidpFiles.data?.some((f) => f.status === 'Importing') ? (
                <Badge tone="info">Importing</Badge>
              ) : tidpFiles.data?.length ? (
                <Badge tone="success">All files imported</Badge>
              ) : (
                <Badge tone="neutral">Nothing uploaded yet</Badge>
              )}
            </div>
            <dl className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs">
              <dt className="text-muted-foreground">TIDP files</dt>
              <dd className="tabular-nums">{formatNumber(tidpFiles.data?.length ?? null)}</dd>
              <dt className="text-muted-foreground">Disciplines covered</dt>
              <dd className="tabular-nums">
                {tidpFiles.data
                  ? formatNumber(new Set(tidpFiles.data.map((f) => f.disciplineId)).size)
                  : '—'}
              </dd>
              <dt className="text-muted-foreground">Rows imported</dt>
              <dd className="tabular-nums">
                {tidpFiles.data
                  ? formatNumber(tidpFiles.data.reduce((sum, f) => sum + f.documentCount, 0))
                  : '—'}
              </dd>
              <dt className="text-muted-foreground">Edited by hand</dt>
              <dd className="tabular-nums">
                {tidpFiles.data
                  ? formatNumber(tidpFiles.data.reduce((sum, f) => sum + f.editedCount, 0))
                  : '—'}
              </dd>
            </dl>

            <div>
              <p className="mb-1 text-xs font-medium uppercase tracking-wide text-muted-foreground">Recent imports</p>
              {imports.data?.length ? (
                <ul className="divide-y divide-border">
                  {imports.data.slice(0, 6).map((batch) => (
                    <ImportRow key={batch.id} batch={batch} />
                  ))}
                </ul>
              ) : (
                <p className="text-xs text-muted-foreground">Nothing imported yet.</p>
              )}
            </div>
          </CardBody>
        </Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader
            title="Progress by discipline"
            description="Share of each corporate discipline's documents submitted and approved."
            action={<Button variant="ghost" size="sm" asChild><Link to="/summary">Summary</Link></Button>}
          />
          <CardBody className="p-0">
            {summary ? <ProgressChart rows={summary.disciplines} /> : <Spinner />}
          </CardBody>
        </Card>

        <Card>
          <CardHeader
            title="Progress by company"
            description="The same figures for each author of exchange 01."
          />
          <CardBody className="p-0">
            {summary ? <ProgressChart rows={summary.authors} maxRows={13} /> : <Spinner />}
          </CardBody>
        </Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader
            title="Baseline packages"
            description={
              baseline.data
                ? `${formatNumber(baseline.data.summary.totalPackages)} packages carrying ${formatNumber(baseline.data.summary.totalPackageDrawings)} drawings.`
                : 'How far each P6 package has got.'
            }
          />
          <CardBody>
            {baseline.data ? (
              <div className="grid gap-3 sm:grid-cols-2">
                {baseline.data.summary.packageStatuses.map((status) => (
                  <StatTile
                    key={status.status}
                    label={`${status.status} packages`}
                    value={formatNumber(status.packages)}
                    hint={`${formatNumber(status.drawings)} drawings`}
                  />
                ))}
              </div>
            ) : baseline.isError ? (
              <p className="text-sm text-destructive">{apiErrorMessage(baseline.error)}</p>
            ) : (
              <Spinner />
            )}
          </CardBody>
        </Card>

        <Card>
          <CardHeader
            title="Control findings"
            description="Things that do not add up between the plan, the baseline and Aconex."
            action={
              <Button variant="outline" size="sm" asChild>
                <Link to="/findings">
                  Open
                  <ArrowRight className="h-4 w-4" aria-hidden />
                </Link>
              </Button>
            }
          />
          <CardBody className="p-0">
            {findingCounts ? (
              <ul className="divide-y divide-border text-sm">
                {findingCounts.map((item) => (
                  <li key={item.key} className="flex items-center justify-between px-5 py-2.5">
                    <span className="flex items-center gap-2">
                      {item.count > 0 ? (
                        <AlertTriangle className="h-4 w-4 text-amber-600 dark:text-amber-400" aria-hidden />
                      ) : (
                        <span className="inline-block h-4 w-4" aria-hidden />
                      )}
                      {item.label}
                    </span>
                    <span className="tabular-nums font-medium">{formatNumber(item.count)}</span>
                  </li>
                ))}
                <li className="flex items-center justify-between px-5 py-2.5 font-medium">
                  <span>Total</span>
                  <span className="tabular-nums">{formatNumber(findingsTotal)}</span>
                </li>
              </ul>
            ) : findings.isError ? (
              <p className="px-5 py-4 text-sm text-destructive">{apiErrorMessage(findings.error)}</p>
            ) : (
              <Spinner />
            )}
          </CardBody>
        </Card>
      </div>

      <Card>
        <CardHeader
          title="Schedule performance by discipline"
          description="SPI = earned value over planned value; on or above 1 is on plan."
          action={evm.data ? <Badge tone={badgeTone[tone]}>{spiLabel[tone]}</Badge> : undefined}
        />
        <CardBody>
          {evm.data ? (
            <SpiChart rows={evm.data.summary.disciplines} />
          ) : evm.isError ? (
            <p className="text-sm text-destructive">{apiErrorMessage(evm.error)}</p>
          ) : (
            <Spinner />
          )}
        </CardBody>
      </Card>
    </div>
  )
}

function ImportRow({ batch }: { batch: ImportBatchSummary }) {
  return (
    <li className="flex items-center justify-between gap-2 py-1.5 text-xs">
      <span className="min-w-0 truncate" title={batch.fileName}>{batch.fileName}</span>
      <span className="flex shrink-0 items-center gap-2 text-muted-foreground">
        <span className="tabular-nums">{formatNumber(batch.rowsRead)} rows</span>
        <Badge tone={batch.status === 'Completed' ? 'success' : batch.status === 'Failed' ? 'danger' : 'info'}>{batch.status}</Badge>
      </span>
    </li>
  )
}

