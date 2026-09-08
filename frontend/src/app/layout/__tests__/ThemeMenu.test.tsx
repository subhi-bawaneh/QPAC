import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ThemeMenu } from '../ThemeMenu'
import { themeOptions } from '@/shared/theme/themeOptions'
import { ThemeProvider } from '@/shared/theme/ThemeProvider'
import { themePreferences, THEME_STORAGE_KEY } from '@/shared/theme/theme'

function stubSystem(prefersDark: boolean) {
  vi.stubGlobal('matchMedia', vi.fn().mockReturnValue({
    matches: prefersDark,
    media: '(prefers-color-scheme: dark)',
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
  }))
}

afterEach(() => {
  window.localStorage.clear()
  document.documentElement.classList.remove('dark')
  vi.unstubAllGlobals()
})

function renderMenu() {
  render(<ThemeProvider><ThemeMenu /></ThemeProvider>)
}

describe('ThemeMenu', () => {
  it('offers exactly the three preferences, in that order', () => {
    expect(themeOptions.map((option) => option.value)).toEqual(themePreferences)
    expect(themeOptions.map((option) => option.label)).toEqual(['Light', 'Dark', 'System'])
  })

  it('names the choice, and what it currently resolves to', () => {
    stubSystem(true)
    renderMenu()

    // The label says what was chosen; the title says what that means right now.
    expect(screen.getByRole('button', { name: 'Theme: System' }))
      .toHaveAttribute('title', 'System (dark)')
  })

  it('names an explicit choice plainly', () => {
    stubSystem(true)
    window.localStorage.setItem(THEME_STORAGE_KEY, 'light')
    renderMenu()

    expect(screen.getByRole('button', { name: 'Theme: Light' }))
      .toHaveAttribute('title', 'Light')
  })

  it('reflects the restored choice rather than the system setting', () => {
    stubSystem(false)
    window.localStorage.setItem(THEME_STORAGE_KEY, 'dark')
    renderMenu()

    expect(screen.getByRole('button', { name: 'Theme: Dark' })).toBeInTheDocument()
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })
})
