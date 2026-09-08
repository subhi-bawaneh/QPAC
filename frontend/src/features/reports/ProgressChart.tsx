import { useState } from 'react'
import {
  Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts'
import { Button } from '@/shared/ui/button'
import { formatNumber } from '@/shared/lib/utils'
import type { SummaryGroup } from './api'
import { useChartPalette } from './chartTheme'

/**
 * Submitted and approved share of each group's documents, as thin horizontal bars.
 *
 * Two series, so a legend is always shown and the same numbers are one click away
 * as a table (the dataviz relief for light-mode contrast). Colours are the validated
 * submitted / approved slots of the chart palette, never the status colours.
 */
export function ProgressChart({ rows, maxRows = 12 }: { rows: SummaryGroup[]; maxRows?: number }) {
  const palette = useChartPalette()
  const [view, setView] = useState<'chart' | 'table'>('chart')

  const data = [...rows]
    .filter((row) => row.total > 0)
    .sort((a, b) => b.total - a.total)
    .slice(0, maxRows)
    .map((row) => ({
      name: row.name,
      total: row.total,
      submitted: row.submitted,
      approved: row.approved,
      submittedPct: Math.round((row.submitted / row.total) * 1000) / 10,
      approvedPct: Math.round((row.approved / row.total) * 1000) / 10,
    }))

  if (data.length === 0) {
    return <p className="px-5 py-6 text-sm text-muted-foreground">No documents to chart yet.</p>
  }

  const height = Math.max(160, data.length * 36 + 48)

  return (
    <div className="space-y-2">
      <div className="flex justify-end px-5 pt-3">
        <Button
          variant="ghost"
          size="sm"
          aria-pressed={view === 'table'}
          onClick={() => setView(view === 'chart' ? 'table' : 'chart')}
        >
          {view === 'chart' ? 'Show table' : 'Show chart'}
        </Button>
      </div>

      {view === 'chart' ? (
        <div style={{ height }} className="px-2 pb-3">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={data} layout="vertical" barGap={2} barCategoryGap={12} margin={{ left: 8, right: 24 }}>
              <CartesianGrid horizontal={false} stroke={palette.grid} />
              <XAxis
                type="number"
                domain={[0, 100]}
                tickFormatter={(value: number) => `${value}%`}
                tick={{ fill: palette.axis, fontSize: 11 }}
                axisLine={false}
                tickLine={false}
              />
              <YAxis
                type="category"
                dataKey="name"
                width={132}
                tick={{ fill: palette.axis, fontSize: 11 }}
                axisLine={false}
                tickLine={false}
              />
              <Tooltip
                cursor={{ fill: palette.grid, opacity: 0.4 }}
                contentStyle={{ background: palette.surface, borderColor: palette.grid, fontSize: 12 }}
                formatter={(value: number, key: string, entry) => {
                  const row = entry.payload as (typeof data)[number]
                  const count = key === 'submittedPct' ? row.submitted : row.approved
                  return [`${value}% (${formatNumber(count)} of ${formatNumber(row.total)})`, key === 'submittedPct' ? 'Submitted' : 'Approved']
                }}
              />
              <Legend
                verticalAlign="top"
                align="right"
                iconType="circle"
                iconSize={8}
                wrapperStyle={{ fontSize: 12, paddingBottom: 8 }}
                formatter={(value: string) => (value === 'submittedPct' ? 'Submitted' : 'Approved')}
              />
              <Bar dataKey="submittedPct" fill={palette.submitted} barSize={10} radius={[0, 4, 4, 0]} />
              <Bar dataKey="approvedPct" fill={palette.approved} barSize={10} radius={[0, 4, 4, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="border-b border-border text-left text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-5 py-2 font-medium">Group</th>
                <th className="px-5 py-2 text-right font-medium">Documents</th>
                <th className="px-5 py-2 text-right font-medium">Submitted</th>
                <th className="px-5 py-2 text-right font-medium">Approved</th>
              </tr>
            </thead>
            <tbody>
              {data.map((row) => (
                <tr key={row.name} className="border-b border-border last:border-0">
                  <td className="px-5 py-2">{row.name}</td>
                  <td className="px-5 py-2 text-right tabular-nums">{formatNumber(row.total)}</td>
                  <td className="px-5 py-2 text-right tabular-nums">{formatNumber(row.submitted)} · {row.submittedPct}%</td>
                  <td className="px-5 py-2 text-right tabular-nums">{formatNumber(row.approved)} · {row.approvedPct}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
