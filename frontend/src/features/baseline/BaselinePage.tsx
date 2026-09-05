import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Card, CardBody } from '@/shared/ui/card'
import { Input } from '@/shared/ui/input'
import { Select } from '@/shared/ui/select'
import { Badge } from '@/shared/ui/badge'
import { Button } from '@/shared/ui/button'
import { Spinner } from '@/shared/ui/spinner'
import { api, apiErrorMessage } from '@/shared/api/client'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import { formatDate, formatNumber } from '@/shared/lib/utils'
import type { BaselineActivityRow, BaselineActivityType, PagedResult } from '@/shared/api/types'

const PAGE_SIZE = 25

export function BaselinePage() {
  const [search, setSearch] = useState('')
  const [type, setType] = useState<BaselineActivityType | ''>('')
  const [used, setUsed] = useState<'' | 'used' | 'unused'>('')
  const [page, setPage] = useState(1)

  const activities = useQuery({
    queryKey: ['baseline', QPAC_PROJECT_ID, { search, type, used, page }],
    queryFn: async () => {
      const { data } = await api.get<PagedResult<BaselineActivityRow>>(
        `/api/projects/${QPAC_PROJECT_ID}/baseline`,
        {
          params: {
            search: search || undefined,
            type: type || undefined,
            used: used === '' ? undefined : used === 'used',
            page,
            pageSize: PAGE_SIZE,
          },
        },
      )
      return data
    },
    placeholderData: (previous) => previous,
  })

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">Baseline</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          The programme behind every planned date. A Submittal activity's finish is a
          document's planned start; the Approval activity of the same package gives its
          planned finish.
        </p>
      </div>

      <Card>
        <div className="flex flex-wrap items-center gap-3 border-b border-border px-5 py-3">
          <Input
            className="h-8 w-64"
            placeholder="Search code or package"
            aria-label="Search activities"
            value={search}
            onChange={(event) => {
              setSearch(event.target.value)
              setPage(1)
            }}
          />

          <Select
            className="h-8 w-40"
            aria-label="Filter by activity type"
            value={type}
            onChange={(event) => {
              setType(event.target.value as BaselineActivityType | '')
              setPage(1)
            }}
          >
            <option value="">Both types</option>
            <option value="Submittal">Submittal</option>
            <option value="Approval">Approval</option>
          </Select>

          <Select
            className="h-8 w-40"
            aria-label="Filter by usage"
            value={used}
            onChange={(event) => {
              setUsed(event.target.value as '' | 'used' | 'unused')
              setPage(1)
            }}
          >
            <option value="">Used and unused</option>
            <option value="used">Used only</option>
            <option value="unused">Unused only</option>
          </Select>

          <span className="ml-auto text-xs text-muted-foreground">
            {activities.data ? `${formatNumber(activities.data.total)} activities` : ''}
          </span>
        </div>

        <CardBody className="p-0">
          {activities.isPending ? <Spinner /> : null}
          {activities.isError ? (
            <p className="px-5 py-4 text-sm text-destructive">{apiErrorMessage(activities.error)}</p>
          ) : null}

          {activities.data && activities.data.items.length === 0 ? (
            <p className="px-5 py-6 text-sm text-muted-foreground">
              No activities match. Import a baseline workbook from the TIDPs page.
            </p>
          ) : null}

          {activities.data?.items.length ? (
            <table className="w-full text-sm">
              <thead className="border-b border-border text-left text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-3 py-2 font-medium">Activity code</th>
                  <th className="px-3 py-2 font-medium">Package</th>
                  <th className="px-3 py-2 font-medium">Type</th>
                  <th className="px-3 py-2 text-right font-medium">Duration</th>
                  <th className="px-3 py-2 font-medium">Start</th>
                  <th className="px-3 py-2 font-medium">Finish</th>
                  <th className="px-3 py-2 text-right font-medium">Documents</th>
                </tr>
              </thead>
              <tbody>
                {activities.data.items.map((activity) => (
                  <tr key={activity.id} className="border-b border-border last:border-0">
                    <td className="px-3 py-2 font-mono text-xs">{activity.activityCode}</td>
                    <td className="max-w-sm truncate px-3 py-2 text-muted-foreground">{activity.package}</td>
                    <td className="px-3 py-2">
                      <Badge tone={activity.type === 'Submittal' ? 'info' : 'neutral'}>{activity.type}</Badge>
                    </td>
                    <td className="px-3 py-2 text-right tabular-nums">{activity.originalDuration}</td>
                    <td className="px-3 py-2 text-muted-foreground">{formatDate(activity.start)}</td>
                    <td className="px-3 py-2 text-muted-foreground">{formatDate(activity.finish)}</td>
                    <td className="px-3 py-2 text-right">
                      {activity.used
                        ? <span className="tabular-nums">{formatNumber(activity.documentCount)}</span>
                        : <Badge tone="warning">Unused</Badge>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}
        </CardBody>

        {activities.data && activities.data.totalPages > 1 ? (
          <div className="flex items-center justify-between border-t border-border px-5 py-3 text-sm">
            <span className="text-muted-foreground">
              Page {activities.data.page} of {formatNumber(activities.data.totalPages)}
            </span>
            <div className="flex gap-2">
              <Button size="sm" variant="outline" disabled={activities.data.page <= 1}
                onClick={() => setPage((value) => value - 1)}>Previous</Button>
              <Button size="sm" variant="outline" disabled={activities.data.page >= activities.data.totalPages}
                onClick={() => setPage((value) => value + 1)}>Next</Button>
            </div>
          </div>
        ) : null}
      </Card>
    </div>
  )
}
