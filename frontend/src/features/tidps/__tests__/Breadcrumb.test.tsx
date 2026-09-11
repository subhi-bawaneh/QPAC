import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { Breadcrumb } from '../Breadcrumb'

describe('Breadcrumb', () => {
  it('shows the root alone at the top level', () => {
    render(<Breadcrumb crumbs={[{ id: null, label: 'TIDPs' }]} onNavigate={vi.fn()} />)

    expect(screen.getByText('TIDPs')).toHaveAttribute('aria-current', 'page')
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  // The last crumb is where you are, so it is not a link: clicking it would do nothing.
  it('makes every crumb but the last one clickable', () => {
    const onNavigate = vi.fn()
    render(
      <Breadcrumb
        crumbs={[{ id: null, label: 'TIDPs' }, { id: 'd1', label: 'Structural' }]}
        onNavigate={onNavigate}
      />,
    )

    expect(screen.getByText('Structural')).toHaveAttribute('aria-current', 'page')
    screen.getByRole('button', { name: 'TIDPs' }).click()
    expect(onNavigate).toHaveBeenCalledWith(null)
  })
})
