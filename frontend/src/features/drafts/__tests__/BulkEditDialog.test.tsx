import { describe, expect, it } from 'vitest'
import { buildBulkFields } from '../draftEdit'

describe('buildBulkFields', () => {
  // Bulk update is set-field semantics: an empty box means "leave it alone",
  // so it must never reach the API as an empty value.
  it('sends only the fields that were filled in', () => {
    expect(buildBulkFields({ packageName: 'PKG-01', scopeArea: '' })).toEqual({ packageName: 'PKG-01' })
  })

  it('treats whitespace as untouched', () => {
    expect(buildBulkFields({ scale: '   ' })).toEqual({})
  })

  it('returns nothing when the form is empty', () => {
    expect(buildBulkFields({})).toEqual({})
  })
})
