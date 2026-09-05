import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { DraftStateBadge } from '../DraftStateBadge'
import type { DraftRowState } from '@/shared/api/types'

describe('DraftStateBadge', () => {
  it.each(['New', 'Modified', 'Unchanged', 'Deleted', 'Conflict'] as DraftRowState[])(
    'renders %s', (state) => {
      render(<DraftStateBadge state={state} />)

      expect(screen.getByText(state)).toBeInTheDocument()
    },
  )
})
