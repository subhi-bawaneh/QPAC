import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { HistoryPanel } from '../HistoryPanel'
import type { AuditEntry } from '@/shared/api/types'

const entries: AuditEntry[] = []

vi.mock('../api', () => ({
  useAuditHistory: () => ({ isPending: false, data: { items: entries, page: 1, pageSize: 100, total: entries.length, totalPages: 1 } }),
}))

function set(items: AuditEntry[]) {
  entries.length = 0
  entries.push(...items)
}

const base: AuditEntry = {
  id: 'a1',
  entityName: 'Document',
  entityId: 'd1',
  action: 'Update',
  field: null,
  oldValue: null,
  newValue: null,
  userId: 'engineer@dip.local',
  at: '2026-09-11T10:00:00',
}

describe('HistoryPanel', () => {
  it('says plainly when nobody has changed the row', () => {
    set([])
    render(<HistoryPanel entity="Document" entityId="d1" />)
    expect(screen.getByText(/Nobody has changed this row/)).toBeInTheDocument()
  })

  // Field, old value, new value and who: the four things that answer "why does this
  // row look like this".
  it('shows the field and the before and after of a change', () => {
    set([{ ...base, field: 'Title', oldValue: 'Old title', newValue: 'New title' }])
    render(<HistoryPanel entity="Document" entityId="d1" />)

    expect(screen.getByText('Title')).toBeInTheDocument()
    expect(screen.getByText('Old title')).toBeInTheDocument()
    expect(screen.getByText('New title')).toBeInTheDocument()
    expect(screen.getByText(/engineer@dip.local/)).toBeInTheDocument()
  })

  // A replace dumps the whole row as JSON. Squeezing that into the old-value column
  // would make every other entry unreadable, so it collapses.
  it('collapses a whole-row dump behind a disclosure', () => {
    set([{ ...base, action: 'Replaced', oldValue: '{"documentNumber":"QF01012-NES"}' }])
    render(<HistoryPanel entity="Document" entityId="d1" />)

    expect(screen.getByText('Replaced')).toBeInTheDocument()
    expect(screen.getByText('The row as it was')).toBeInTheDocument()
  })
})
