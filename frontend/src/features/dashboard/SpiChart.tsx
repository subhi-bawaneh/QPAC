import { Bar, BarChart, CartesianGrid, Cell, LabelList, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { EvmRow } from './api'
import { spiLabel, spiTone, useChartPalette } from './chartTheme'

/**
 * SPI per discipline, coloured by the PLAN.md § 7 bands.
 *
 * Those are status colours, so they never carry the meaning alone: every bar is
 * labelled with its own SPI, and the reference line at 1.0 shows where "on plan"
 * sits. A discipline with nothing planned has no ratio and is left out rather
 * than drawn as zero.
 */
export function SpiChart({ rows }: { rows: EvmRow[] }) {
  const palette = useChartPalette()

  const data = rows
    .filter((row) => row.schedulePerformanceIndex !== null)
    .map((row) => ({
      name: row.name,
      spi: Number(row.schedulePerformanceIndex),
      tone: spiTone(row.schedulePerformanceIndex),
    }))

  if (data.length === 0) {
    return (
      <p className="py-8 text-center text-sm text-muted-foreground">
        Nothing is planned yet, so there is no schedule performance to show.
      </p>
    )
  }

  const colours = {
    good: palette.status.good,
    warning: palette.status.warning,
    critical: palette.status.critical,
    none: palette.grid,
  }

  return (
    <figure className="m-0">
      <figcaption className="mb-3 text-xs text-muted-foreground">
        Schedule Performance Index by discipline — earned over planned value. 1.0 is on plan.
      </figcaption>

      <div className="h-72 w-full">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={data} margin={{ top: 16, right: 12, bottom: 4, left: 4 }}>
            <CartesianGrid stroke={palette.grid} strokeDasharray="3 3" vertical={false} />
            <XAxis
              dataKey="name"
              tick={{ fill: palette.axis, fontSize: 11 }}
              stroke={palette.grid}
              interval={0}
              angle={-20}
              textAnchor="end"
              height={60}
            />
            <YAxis tick={{ fill: palette.axis, fontSize: 11 }} stroke={palette.grid} width={48} />
            <ReferenceLine y={1} stroke={palette.axis} strokeDasharray="4 4" />
            <Tooltip
              cursor={{ fill: palette.grid, fillOpacity: 0.3 }}
              contentStyle={{
                background: palette.surface,
                border: `1px solid ${palette.grid}`,
                borderRadius: 8,
                fontSize: 12,
              }}
              formatter={(value: number, _name, entry) => [
                `${value.toFixed(2)} — ${spiLabel[(entry.payload as { tone: keyof typeof spiLabel }).tone]}`,
                'SPI',
              ]}
            />
            <Bar dataKey="spi" radius={[4, 4, 0, 0]}>
              {data.map((row) => (
                <Cell key={row.name} fill={colours[row.tone]} />
              ))}
              <LabelList
                dataKey="spi"
                position="top"
                fill={palette.axis}
                fontSize={11}
                formatter={(value: number) => value.toFixed(2)}
              />
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>
    </figure>
  )
}
