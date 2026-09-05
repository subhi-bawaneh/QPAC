import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { StatusBadge } from '../StatusBadge'

describe('StatusBadge', () => {
  // The enum name has no space; the sheet and the UI both say "Under Review".
  it('renders the label the reports use, not the enum name', () => {
    render(<StatusBadge status="UnderReview" />)

    expect(screen.getByText('Under Review')).toBeInTheDocument()
  })

  it.each(['Approved', 'Rejected', 'Withdrawn'] as const)('renders %s', (status) => {
    render(<StatusBadge status={status} />)

    expect(screen.getByText(status)).toBeInTheDocument()
  })

  it('shows a dash when the document has no Aconex history', () => {
    render(<StatusBadge status={null} />)

    expect(screen.getByText('—')).toBeInTheDocument()
  })
})
