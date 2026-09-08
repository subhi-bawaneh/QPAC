import { useEffect, useState } from 'react'

/**
 * Chart colours, validated with the dataviz palette validator.
 *
 * Series use categorical slots 1-3 (blue, orange, aqua): the first three slots
 * clear the all-pairs CVD and normal-vision floors in both modes. Light-mode aqua
 * sits below 3:1 against the surface, so every chart that uses it ships the
 * numbers as a table too — that is the required relief, not a nicety.
 *
 * SPI uses the fixed status palette, which is never themed and never doubles as a
 * series colour. Status colour never carries meaning alone: every SPI bar is
 * labelled with its own value.
 */
export interface ChartPalette {
  planned: string
  submitted: string
  approved: string
  grid: string
  axis: string
  surface: string
  status: {
    good: string
    warning: string
    critical: string
  }
}

const light: ChartPalette = {
  planned: '#2a78d6',
  submitted: '#eb6834',
  approved: '#1baf7a',
  grid: '#e5e7eb',
  axis: '#52514e',
  surface: '#ffffff',
  status: { good: '#0ca30c', warning: '#fab219', critical: '#d03b3b' },
}

const dark: ChartPalette = {
  planned: '#3987e5',
  submitted: '#d95926',
  approved: '#199e70',
  grid: '#2b3444',
  axis: '#c3c2b7',
  surface: '#1a1a19',
  status: { good: '#0ca30c', warning: '#fab219', critical: '#d03b3b' },
}

/** Follows the theme toggle, which stamps `dark` on the document element. */
export function useChartPalette(): ChartPalette {
  const [isDark, setIsDark] = useState(
    () => typeof document !== 'undefined' && document.documentElement.classList.contains('dark'),
  )

  useEffect(() => {
    const target = document.documentElement
    const observer = new MutationObserver(() => setIsDark(target.classList.contains('dark')))
    observer.observe(target, { attributes: true, attributeFilter: ['class'] })
    return () => observer.disconnect()
  }, [])

  return isDark ? dark : light
}

/** SPI bands from PLAN.md § 7: on or above plan, slipping, behind. */
export function spiTone(spi: number | null): 'good' | 'warning' | 'critical' | 'none' {
  if (spi === null) return 'none'
  if (spi >= 1) return 'good'
  if (spi >= 0.9) return 'warning'
  return 'critical'
}

export const spiLabel = {
  good: 'On or ahead of plan',
  warning: 'Slipping',
  critical: 'Behind plan',
  none: 'Nothing planned yet',
} as const
