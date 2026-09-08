import {
  CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts'
import type { SummaryWeek } from './api'
import { useChartPalette } from './chartTheme'

/**
 * The S-curve: cumulative planned, submitted and approved documents by week.
 *
 * Three series, so a legend is always present. Light-mode aqua sits below 3:1
 * against the surface, which obliges the relief the page already carries — the
 * same numbers appear in the discipline table below the chart.
 */
export function SCurveChart({ weeks, currentWeek }: { weeks: SummaryWeek[]; currentWeek: string }) {
  const palette = useChartPalette()

  // ~150 weekly points is more than a chart this size can resolve; showing every
  // second week keeps the line honest and the axis readable.
  const step = weeks.length > 80 ? 2 : 1
  const data = weeks
    .filter((_, index) => index % step === 0)
    .map((week) => ({
      week: week.number,
      label: new Date(week.to).toLocaleDateString('en-GB', { month: 'short', year: '2-digit' }),
      Planned: week.cumulativePlanned,
      Submitted: week.cumulativeSubmitted,
      Approved: week.cumulativeApproved,
    }))

  const currentLabel = new Date(currentWeek).toLocaleDateString('en-GB', {
    day: '2-digit', month: 'short', year: 'numeric',
  })

  return (
    <figure className="m-0">
      <figcaption className="mb-3 text-xs text-muted-foreground">
        Cumulative documents by week, as at {currentLabel}.
      </figcaption>

      <div className="h-72 w-full">
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={data} margin={{ top: 4, right: 12, bottom: 4, left: 4 }}>
            <CartesianGrid stroke={palette.grid} strokeDasharray="3 3" vertical={false} />
            <XAxis
              dataKey="label"
              tick={{ fill: palette.axis, fontSize: 11 }}
              stroke={palette.grid}
              minTickGap={40}
            />
            <YAxis
              tick={{ fill: palette.axis, fontSize: 11 }}
              stroke={palette.grid}
              width={56}
              tickFormatter={(value: number) => value.toLocaleString('en-GB')}
            />
            <Tooltip
              contentStyle={{
                background: palette.surface,
                border: `1px solid ${palette.grid}`,
                borderRadius: 8,
                fontSize: 12,
              }}
              labelStyle={{ color: palette.axis }}
              formatter={(value: number, name: string) => [value.toLocaleString('en-GB'), name]}
            />
            <Legend wrapperStyle={{ fontSize: 12 }} />
            <Line type="monotone" dataKey="Planned" stroke={palette.planned} strokeWidth={2} dot={false} />
            <Line type="monotone" dataKey="Submitted" stroke={palette.submitted} strokeWidth={2} dot={false} />
            <Line type="monotone" dataKey="Approved" stroke={palette.approved} strokeWidth={2} dot={false} />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </figure>
  )
}
