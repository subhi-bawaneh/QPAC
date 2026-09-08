import type { WorkbookColumn } from '@/shared/api/types'

// The 34 columns A–AH of the TIDP/MIDP sheet, in PLAN.md § 5.1.1 order. The API
// serves the same list; this copy is what the editor maps cells back onto, and what
// the tests hold the API to.
export const documentColumnKeys = [
  'documentNumber',
  'title',
  'extractedFromModel',
  'scopeArea',
  'authoringSoftware',
  'exchangeFormat',
  'scale',
  'deliveryMilestone',
  'packageName',
  'activityId',
  'classificationCode',
  'f01Project',
  'f02Originator',
  'f03Contract',
  'f04DocType',
  'f05Discipline',
  'f06Zone',
  'f07Building',
  'f08ADrawingType',
  'f08BLevel',
  'f08CSequence',
  'corporateDiscipline',
  'ex1Author',
  'ex1Geometrical',
  'ex1NonGeometrical',
  'ex1DurationDays',
  'ex1Predecessor',
  'ex1ExchangeDate',
  'ex2Author',
  'ex2Geometrical',
  'ex2NonGeometrical',
  'ex2DurationDays',
  'ex2Predecessor',
  'ex2ExchangeDate',
] as const

export type DocumentColumnKey = (typeof documentColumnKeys)[number]

/** A1-style letters for a zero-based column index: A…Z, AA…AH. */
export function columnLetter(index: number): string {
  let letter = ''
  let n = index + 1
  while (n > 0) {
    const remainder = (n - 1) % 26
    letter = String.fromCharCode(65 + remainder) + letter
    n = Math.floor((n - 1) / 26)
  }
  return letter
}

export const documentColumnLetters = documentColumnKeys.map((_key, index) => columnLetter(index))

/** "C12" — what the formula bar's name box shows for the active cell. */
export function cellAddress(columns: WorkbookColumn[], columnIndex: number, rowNumber: number): string {
  const letter = columns[columnIndex]?.letter ?? columnLetter(columnIndex)
  return `${letter}${rowNumber}`
}

/** Column A is the composed number, recomputed from L..U — never typed. */
export function isEditable(column: WorkbookColumn | undefined, canEdit: boolean): boolean {
  return canEdit && column !== undefined && column.editable
}

const exchangeKeys: Record<string, { number: number; field: string }> = {
  ex1Author: { number: 1, field: 'author' },
  ex1Geometrical: { number: 1, field: 'geometrical' },
  ex1NonGeometrical: { number: 1, field: 'nonGeometrical' },
  ex1DurationDays: { number: 1, field: 'durationDays' },
  ex1Predecessor: { number: 1, field: 'predecessor' },
  ex1ExchangeDate: { number: 1, field: 'exchangeDate' },
  ex2Author: { number: 2, field: 'author' },
  ex2Geometrical: { number: 2, field: 'geometrical' },
  ex2NonGeometrical: { number: 2, field: 'nonGeometrical' },
  ex2DurationDays: { number: 2, field: 'durationDays' },
  ex2Predecessor: { number: 2, field: 'predecessor' },
  ex2ExchangeDate: { number: 2, field: 'exchangeDate' },
}

export interface RowPayload {
  [field: string]: unknown
  exchanges: {
    number: number
    stage: string | null
    programmeRef: string | null
    author: string | null
    geometrical: string | null
    nonGeometrical: string | null
    durationDays: number | null
    predecessor: string | null
    exchangeDate: string | null
  }[]
}

// Both the draft and the Live editors take a full replace of the row, so one edited
// cell is sent together with the rest of the row as the grid currently shows it.
// DocumentNumber is left out: the server recomposes it from L..U.
export function rowToPayload(columns: WorkbookColumn[], cells: (string | null)[]): RowPayload {
  const payload: RowPayload = { exchanges: [] }
  const exchanges = new Map<number, Record<string, unknown>>()

  columns.forEach((column, index) => {
    const value = cells[index] ?? null
    if (column.key === 'documentNumber') return

    const exchange = exchangeKeys[column.key]
    if (exchange) {
      const target = exchanges.get(exchange.number) ?? {
        number: exchange.number,
        stage: null,
        programmeRef: null,
        author: null,
        geometrical: null,
        nonGeometrical: null,
        durationDays: null,
        predecessor: null,
        exchangeDate: null,
      }
      target[exchange.field] = exchange.field === 'durationDays'
        ? (value === null || value === '' ? null : Number(value))
        : (value === '' ? null : value)
      exchanges.set(exchange.number, target)
      return
    }

    payload[column.key] = value === '' ? null : value
  })

  // A blank exchange block is not sent at all, so an untouched row keeps whatever
  // the import wrote.
  payload.exchanges = [...exchanges.values()]
    .filter((exchange) => Object.entries(exchange)
      .some(([key, value]) => key !== 'number' && value !== null))
    .map((exchange) => exchange as RowPayload['exchanges'][number])
    .sort((a, b) => a.number - b.number)

  return payload
}
