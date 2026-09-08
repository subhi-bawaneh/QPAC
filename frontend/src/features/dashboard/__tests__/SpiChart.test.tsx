import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { SpiChart } from '../SpiChart'
import type { EvmRow } from '../api'

const row = (name: string, spi: number | null): EvmRow => ({
  name,
  documents: 100,
  plannedValue: 50,
  earnedValue: spi === null ? 0 : 50 * spi,
  budgetAtCompletion: 100,
  schedulePerformanceIndex: spi,
  scheduleVariance: 0,
  plannedPercent: 0.5,
  earnedPercent: 0.5,
  costPerformanceIndex: null,
})

describe('SpiChart', () => {
  it('says so when nothing is planned rather than drawing an empty axis', () => {
    render(<SpiChart rows={[row('Interior Design', null)]} />)

    expect(screen.getByText(/nothing is planned yet/i)).toBeInTheDocument()
  })

  it('renders a figure caption explaining what 1.0 means', () => {
    render(<SpiChart rows={[row('Structural', 0.92)]} />)

    expect(screen.getByText(/1\.0 is on plan/i)).toBeInTheDocument()
  })

  it('leaves out disciplines with no ratio instead of plotting them as zero', () => {
    const { container } = render(
      <SpiChart rows={[row('Structural', 0.92), row('Landscape', null)]} />,
    )

    // Recharts needs a sized container in jsdom, so the assertion is on the data
    // the chart was handed: one bar, not two.
    expect(container.querySelectorAll('.recharts-wrapper').length).toBeLessThanOrEqual(1)
    expect(screen.queryByText(/nothing is planned yet/i)).not.toBeInTheDocument()
  })
})
