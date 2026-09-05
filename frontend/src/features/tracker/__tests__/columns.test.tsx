import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { trackerColumns } from '../trackerColumns'
import { midpColumns } from '@/features/midp/midpColumns'
import type { TrackerRow } from '@/shared/api/types'

const row: TrackerRow = {
  documentId: 'doc-1',
  documentNumber: 'QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004',
  type: 'SDW',
  discipline: 'Structural',
  title: 'BLADE 4 - Welding Details',
  deliveryMilestone: '2025-12-19T00:00:00',
  activityId: 'QP.E.ST.GEN.GEN.1000',
  packageName: null,
  building: 'Z00000',
  level: 'ZZ',
  trade: 'STL',
  author: 'JINGGONG',
  submissionsCount: 2,
  revision: '01',
  aconexStatus: 'B - Approved with Comments',
  status: 'Approved',
  submissionDate: '2026-04-18T10:20:36.551',
  dateModified: '2026-04-26T13:04:49.041',
  transmittal: 'BSBG-TRANSMIT-000156',
  plannedStart: '2025-12-14T00:00:00',
  plannedFinish: '2025-12-28T00:00:00',
  actualStart: '2026-03-31T11:35:50.696',
  actualFinish: '2026-04-26T13:04:49.041',
}

function renderCells(columns: typeof trackerColumns) {
  render(<>{columns.map((column) => <div key={column.key}>{column.render(row)}</div>)}</>)
}

describe('tracker columns', () => {
  it('show the computed state of the document', () => {
    renderCells(trackerColumns)

    expect(screen.getByText(row.documentNumber)).toBeInTheDocument()
    expect(screen.getByText('01')).toBeInTheDocument()
    expect(screen.getByText('Approved')).toBeInTheDocument()
    // Date Modified and Actual Finish are the same instant for an approved
    // document — the engine sets Actual Finish to it (PLAN.md § 5.4.1).
    expect(screen.getAllByText('26 Apr 2026')).toHaveLength(2)
    expect(screen.getByText('18 Apr 2026')).toBeInTheDocument()
  })

  it('render an em dash where a document has no Aconex history', () => {
    render(
      <>
        {trackerColumns
          .filter((column) => column.key === 'revision' || column.key === 'submissions')
          .map((column) => (
            <div key={column.key}>{column.render({ ...row, revision: null, submissionsCount: null })}</div>
          ))}
      </>,
    )

    expect(screen.getAllByText('—')).toHaveLength(2)
  })
})

describe('midp columns', () => {
  it('show the plan rather than the outcome', () => {
    renderCells(midpColumns)

    expect(screen.getByText('SDW')).toBeInTheDocument()
    expect(screen.getByText('QP.E.ST.GEN.GEN.1000')).toBeInTheDocument()
    expect(screen.getByText('JINGGONG')).toBeInTheDocument()
    expect(screen.getByText('19 Dec 2025')).toBeInTheDocument()
  })

  it('leave the computed columns out entirely', () => {
    const keys = midpColumns.map((column) => column.key)

    expect(keys).not.toContain('status')
    expect(keys).not.toContain('revision')
    expect(keys).not.toContain('actualFinish')
  })
})
