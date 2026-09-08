import { describe, expect, it, vi } from 'vitest'
import { QueryClient } from '@tanstack/react-query'
import { invalidateFor, invalidationMap } from '../invalidation'
import { syncEventNames } from '../syncHub'

describe('invalidation map', () => {
  it('covers every hub event', () => {
    expect(Object.keys(invalidationMap).sort()).toEqual([...syncEventNames].sort())
  })

  it('refreshes the folder view when a file finishes importing', () => {
    const client = new QueryClient()
    const invalidate = vi.spyOn(client, 'invalidateQueries').mockResolvedValue()

    invalidateFor(client, 'fileImported')

    expect(invalidate.mock.calls.map((call) => call[0]?.queryKey)).toEqual([
      ['folders'], ['imports'], ['workbook'], ['drafts'],
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

  it('does nothing for an event that changes no data', () => {
    const client = new QueryClient()
    const invalidate = vi.spyOn(client, 'invalidateQueries').mockResolvedValue()

    invalidateFor(client, 'syncStarted')

    expect(invalidate).not.toHaveBeenCalled()
  })
})
