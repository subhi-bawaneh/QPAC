import { describe, expect, it } from 'vitest'
import { saveRowUrl } from '../api'

describe('saveRowUrl', () => {
  it('edits the draft row when the file sits in the Draft layer', () => {
    expect(saveRowUrl('Draft', 'row-1')).toBe('/api/drafts/documents/row-1')
  })

  it('edits the Live document when the file sits in the Live layer', () => {
    expect(saveRowUrl('Live', 'row-1')).toBe('/api/documents/row-1')
  })
})
