import { describe, expect, it } from 'vitest'
import {
  cellAddress, columnLetter, documentColumnKeys, documentColumnLetters, isEditable, rowToPayload,
} from '../columns'
import type { WorkbookColumn } from '@/shared/api/types'

const column = (letter: string, key: string, editable = true): WorkbookColumn => ({
  letter, key, title: key, width: 100, editable, kind: 'text',
})

describe('workbook columns', () => {
  it('has the 34 columns of PLAN.md § 5.1.1, A through AH', () => {
    expect(documentColumnKeys).toHaveLength(34)
    expect(documentColumnLetters[0]).toBe('A')
    expect(documentColumnLetters[21]).toBe('V')
    expect(documentColumnLetters[25]).toBe('Z')
    expect(documentColumnLetters[26]).toBe('AA')
    expect(documentColumnLetters[33]).toBe('AH')
  })

  it('keeps the numbering fields in L..U order, which composes column A', () => {
    expect(documentColumnKeys.slice(11, 21)).toEqual([
      'f01Project', 'f02Originator', 'f03Contract', 'f04DocType', 'f05Discipline',
      'f06Zone', 'f07Building', 'f08ADrawingType', 'f08BLevel', 'f08CSequence',
    ])
    expect(documentColumnKeys[21]).toBe('corporateDiscipline')
  })

  it('splits the two exchange blocks at W and AC', () => {
    expect(documentColumnLetters[22]).toBe('W')
    expect(documentColumnKeys[22]).toBe('ex1Author')
    expect(documentColumnLetters[28]).toBe('AC')
    expect(documentColumnKeys[28]).toBe('ex2Author')
  })

  it('names cells the way Excel does', () => {
    const columns = [column('A', 'documentNumber'), column('C', 'extractedFromModel')]
    expect(cellAddress(columns, 1, 12)).toBe('C12')
    expect(columnLetter(33)).toBe('AH')
  })

  it('never lets column A be edited', () => {
    expect(isEditable(column('A', 'documentNumber', false), true)).toBe(false)
    expect(isEditable(column('B', 'title'), true)).toBe(true)
    expect(isEditable(column('B', 'title'), false)).toBe(false)
  })
})

describe('rowToPayload', () => {
  const columns: WorkbookColumn[] = [
    column('A', 'documentNumber', false),
    column('B', 'title'),
    column('U', 'f08CSequence'),
    column('W', 'ex1Author'),
    { ...column('Z', 'ex1DurationDays'), kind: 'int' },
    column('AC', 'ex2Author'),
  ]

  it('leaves the document number out — the server recomposes it', () => {
    const payload = rowToPayload(columns, ['QF01012-…-0004', 'A title', '0005', null, null, null])

    expect(payload.documentNumber).toBeUndefined()
    expect(payload.title).toBe('A title')
    expect(payload.f08CSequence).toBe('0005', )
  })

  it('collects the exchange columns into their blocks', () => {
    const payload = rowToPayload(columns, ['x', 'A title', '0005', 'JINGGONG', '14', 'AFCO'])

    expect(payload.exchanges).toEqual([
      expect.objectContaining({ number: 1, author: 'JINGGONG', durationDays: 14 }),
      expect.objectContaining({ number: 2, author: 'AFCO' }),
    ])
  })

  it('sends no exchange block when the whole block is blank', () => {
    const payload = rowToPayload(columns, ['x', 'A title', '0005', null, null, null])

    expect(payload.exchanges).toEqual([])
  })

  it('turns an emptied cell into null rather than an empty string', () => {
    const payload = rowToPayload(columns, ['x', '', '0005', null, null, null])

    expect(payload.title).toBeNull()
  })
})
