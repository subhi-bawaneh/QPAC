import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Link } from 'react-router-dom'
import { Button } from '../button'

describe('Button', () => {
  it('renders a button by default', () => {
    render(<Button>Import</Button>)

    expect(screen.getByRole('button', { name: 'Import' })).toBeInTheDocument()
  })

  // asChild is what lets a router Link wear the button styling without nesting an
  // <a> inside a <button>, which is invalid and breaks keyboard navigation.
  it('renders its child as the element when asChild is set', () => {
    render(
      <MemoryRouter>
        <Button asChild variant="outline">
          <Link to="/drafts/1">Review draft</Link>
        </Button>
      </MemoryRouter>,
    )

    const link = screen.getByRole('link', { name: 'Review draft' })
    expect(link).toHaveAttribute('href', '/drafts/1')
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('applies the destructive variant', () => {
    render(<Button variant="destructive">Delete</Button>)

    expect(screen.getByRole('button', { name: 'Delete' }).className).toContain('bg-destructive')
  })
})
