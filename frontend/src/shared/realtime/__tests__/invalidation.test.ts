import { describe, expect, it, vi } from 'vitest'
import { QueryClient } from '@tanstack/react-query'
import { invalidateFor, invalidationMap } from '../invalidation'
import { syncEventNames } from '../syncHub'

describe('invalidation map', () => {
  it('covers every hub event', () => {
    expect(Object.keys(invalidationMap).sort()).toEqual([...syncEventNames].sort())
  })

  it('refreshes the file list and the grid when an import finishes', () => {
    const client = new QueryClient()
    const invalidate = vi.spyOn(client, 'invalidateQueries').mockResolvedValue()

    invalidateFor(client, 'importFinished')

    expect(invalidate.mock.calls.map((call) => call[0]?.queryKey)).toEqual([
      ['imports'], ['tidp-files'], ['workbook'], ['documents'],
    ])
  })

  it('refreshes the reports when a recalculation finishes', () => {
    const client = new QueryClient()
    const invalidate = vi.spyOn(client, 'invalidateQueries').mockResolvedValue()

    invalidateFor(client, 'recalculationFinished')

    expect(invalidate.mock.calls.map((call) => call[0]?.queryKey)).toEqual([
      ['tracker'], ['summary'], ['findings'], ['documents'],
    ])
  })

  it('refreshes the upload list as soon as a batch is queued', () => {
    const client = new QueryClient()
    const invalidate = vi.spyOn(client, 'invalidateQueries').mockResolvedValue()

    invalidateFor(client, 'importQueued')

    expect(invalidate.mock.calls.map((call) => call[0]?.queryKey)).toEqual([['imports']])
  })
})
