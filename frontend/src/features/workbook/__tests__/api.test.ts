import { describe, expect, it } from 'vitest'
import { saveRowUrl } from '../api'

describe('saveRowUrl', () => {
  // With the Draft layer gone there is one row behind every cell: the document.
  it('edits the document', () => {
    expect(saveRowUrl('row-1')).toBe('/api/documents/row-1')
  })
})
