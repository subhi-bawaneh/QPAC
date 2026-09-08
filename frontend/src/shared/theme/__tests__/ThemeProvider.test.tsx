import { afterEach, describe, expect, it, vi } from 'vitest'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ThemeProvider } from '../ThemeProvider'
import { useTheme } from '../useTheme'
import { THEME_STORAGE_KEY } from '../theme'

type MediaListener = (event: MediaQueryListEvent) => void

// A controllable stand-in for the OS setting, so the "system changed under us" path
// is testable rather than assumed.
function stubSystem(prefersDark: boolean) {
  const listeners = new Set<MediaListener>()
  const query = {
    matches: prefersDark,
    media: '(prefers-color-scheme: dark)',
    addEventListener: (_: string, listener: MediaListener) => { listeners.add(listener) },
    removeEventListener: (_: string, listener: MediaListener) => { listeners.delete(listener) },
  }

  vi.stubGlobal('matchMedia', vi.fn().mockReturnValue(query))

  return {
    change(next: boolean) {
      query.matches = next
      act(() => {
        for (const listener of listeners) listener({ matches: next } as MediaQueryListEvent)
      })
    },
  }
}

function Probe() {
  const { preference, resolved, setPreference } = useTheme()
  return (
    <div>
      <span data-testid="preference">{preference}</span>
      <span data-testid="resolved">{resolved}</span>
      <button type="button" onClick={() => setPreference('dark')}>Pick dark</button>
      <button type="button" onClick={() => setPreference('light')}>Pick light</button>
      <button type="button" onClick={() => setPreference('system')}>Follow system</button>
    </div>
  )
}

function renderProbe() {
  render(<ThemeProvider><Probe /></ThemeProvider>)
}

const isDark = () => document.documentElement.classList.contains('dark')

afterEach(() => {
  window.localStorage.clear()
  document.documentElement.classList.remove('dark')
  document.documentElement.style.colorScheme = ''
  vi.unstubAllGlobals()
})

describe('ThemeProvider', () => {
  it('follows the system setting on a first visit', () => {
    stubSystem(true)
    renderProbe()

    expect(screen.getByTestId('preference')).toHaveTextContent('system')
    expect(screen.getByTestId('resolved')).toHaveTextContent('dark')
    expect(isDark()).toBe(true)
  })

  it('uses the light system setting just the same', () => {
    stubSystem(false)
    renderProbe()

    expect(screen.getByTestId('resolved')).toHaveTextContent('light')
    expect(isDark()).toBe(false)
  })

  it('restores the choice made on a previous visit, over the system setting', () => {
    stubSystem(true)
    window.localStorage.setItem(THEME_STORAGE_KEY, 'light')
    renderProbe()

    expect(screen.getByTestId('preference')).toHaveTextContent('light')
    expect(isDark()).toBe(false)
  })

  it('persists a choice so the next visit starts there', async () => {
    const user = userEvent.setup()
    stubSystem(false)
    renderProbe()

    await user.click(screen.getByRole('button', { name: 'Pick dark' }))

    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')
    expect(isDark()).toBe(true)
    expect(document.documentElement.style.colorScheme).toBe('dark')
  })

  it('keeps following the system after the user goes back to it', async () => {
    const user = userEvent.setup()
    const system = stubSystem(false)
    renderProbe()

    await user.click(screen.getByRole('button', { name: 'Pick dark' }))
    expect(isDark()).toBe(true)

    await user.click(screen.getByRole('button', { name: 'Follow system' }))
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe('system')
    expect(isDark()).toBe(false)

    system.change(true)
    expect(screen.getByTestId('resolved')).toHaveTextContent('dark')
    expect(isDark()).toBe(true)
  })

  it('reacts when the system flips while the page is open', () => {
    const system = stubSystem(false)
    renderProbe()

    expect(isDark()).toBe(false)
    system.change(true)
    expect(isDark()).toBe(true)
  })

  it('leaves an explicit choice alone when the system flips', async () => {
    const user = userEvent.setup()
    const system = stubSystem(false)
    renderProbe()

    await user.click(screen.getByRole('button', { name: 'Pick light' }))
    system.change(true)

    expect(screen.getByTestId('resolved')).toHaveTextContent('light')
    expect(isDark()).toBe(false)
  })

  it('picks up a change another tab made', () => {
    stubSystem(false)
    renderProbe()

    window.localStorage.setItem(THEME_STORAGE_KEY, 'dark')
    act(() => { window.dispatchEvent(new StorageEvent('storage', { key: THEME_STORAGE_KEY })) })

    expect(screen.getByTestId('preference')).toHaveTextContent('dark')
    expect(isDark()).toBe(true)
  })
})
