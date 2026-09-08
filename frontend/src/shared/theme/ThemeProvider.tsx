import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import {
  applyTheme, initialPreference, resolveTheme, systemPrefersDark, themeStorage,
  type ThemePreference,
} from './theme'
import { ThemeContext, type ThemeValue } from './themeContext'

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ThemePreference>(initialPreference)
  const [prefersDark, setPrefersDark] = useState(systemPrefersDark)

  // While the preference is "system", the OS can change under us — someone flips
  // their machine to dark at sunset, or macOS does it on a schedule — and the page
  // has to follow without a reload.
  useEffect(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return

    const query = window.matchMedia('(prefers-color-scheme: dark)')
    const onChange = (event: MediaQueryListEvent) => setPrefersDark(event.matches)

    setPrefersDark(query.matches)
    query.addEventListener('change', onChange)
    return () => query.removeEventListener('change', onChange)
  }, [])

  const resolved = resolveTheme(preference, prefersDark)

  useEffect(() => { applyTheme(resolved) }, [resolved])

  // Another tab of the same app changing the theme should not leave this one behind.
  useEffect(() => {
    if (typeof window === 'undefined') return

    const onStorage = () => setPreferenceState(initialPreference())
    window.addEventListener('storage', onStorage)
    return () => window.removeEventListener('storage', onStorage)
  }, [])

  const setPreference = useCallback((next: ThemePreference) => {
    themeStorage.write(next)
    setPreferenceState(next)
  }, [])

  const value = useMemo<ThemeValue>(
    () => ({ preference, resolved, setPreference }),
    [preference, resolved, setPreference],
  )

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}
