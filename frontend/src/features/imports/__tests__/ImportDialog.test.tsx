import { describe, expect, it } from 'vitest'
import { defaultKindFor } from '../kinds'

describe('defaultKindFor', () => {
  // The detected kind saves the user a choice; only Unknown needs a fallback.
  it.each([
    ['Tidp', 'Tidp'],
    ['Midp', 'Midp'],
    ['Baseline', 'Baseline'],
    ['AconexHistory', 'AconexHistory'],
    ['Picklists', 'Picklists'],
    ['Lists', 'Lists'],
  ] as const)('uses the detected kind %s', (kind, expected) => {
    expect(defaultKindFor(kind)).toBe(expected)
  })

  it('falls back to TIDP when the kind could not be detected', () => {
    expect(defaultKindFor('Unknown')).toBe('Tidp')
  })
})
