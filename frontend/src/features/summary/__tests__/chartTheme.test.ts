import { describe, expect, it } from 'vitest'
import { spiLabel, spiTone } from '../chartTheme'

describe('spiTone', () => {
  // PLAN.md § 7: >= 1 on plan, 0.9-1 slipping, below 0.9 behind.
  it.each([
    [3.11, 'good'],
    [1, 'good'],
    [0.99, 'warning'],
    [0.9, 'warning'],
    [0.8999, 'critical'],
    [0.31, 'critical'],
  ] as const)('puts SPI %s in the %s band', (spi, expected) => {
    expect(spiTone(spi)).toBe(expected)
  })

  it('has no band when nothing is planned yet', () => {
    expect(spiTone(null)).toBe('none')
  })

  // A status colour never carries meaning alone, so every band needs its words.
  it('gives every band a label', () => {
    for (const tone of ['good', 'warning', 'critical', 'none'] as const) {
      expect(spiLabel[tone]).toBeTruthy()
    }
  })
})
