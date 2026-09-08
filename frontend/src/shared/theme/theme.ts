// Three states, not two. "system" is a real choice — it means "keep following the
// OS" — and it is the default, so a first-time visitor gets the theme their machine
// already asked for instead of whatever we happened to hard-code.
export type ThemePreference = 'light' | 'dark' | 'system'

/** What actually gets painted once the preference is resolved. */
export type ResolvedTheme = 'light' | 'dark'

export const THEME_STORAGE_KEY = 'dip.theme'

export const themePreferences: ThemePreference[] = ['light', 'dark', 'system']

export function isThemePreference(value: unknown): value is ThemePreference {
  return typeof value === 'string' && (themePreferences as string[]).includes(value)
}

/** The OS-level setting, via the media query browsers expose for it. */
export function systemPrefersDark(): boolean {
  return typeof window !== 'undefined'
    && typeof window.matchMedia === 'function'
    && window.matchMedia('(prefers-color-scheme: dark)').matches
}

export function resolveTheme(preference: ThemePreference, prefersDark: boolean): ResolvedTheme {
  if (preference === 'system') return prefersDark ? 'dark' : 'light'
  return preference
}

// Tailwind is configured with darkMode: ['class'], so the class on <html> is what
// every component reads. `color-scheme` is set alongside it so the browser's own
// chrome — form controls, the default scrollbar, the caret — follows too.
export function applyTheme(resolved: ResolvedTheme) {
  if (typeof document === 'undefined') return
  const root = document.documentElement
  root.classList.toggle('dark', resolved === 'dark')
  root.style.colorScheme = resolved
}

// Storage throws in private-mode browsers and when site data is blocked, so every
// access is guarded the way tokenStorage guards its own.
export const themeStorage = {
  read(): ThemePreference | null {
    try {
      const stored = window.localStorage.getItem(THEME_STORAGE_KEY)
      return isThemePreference(stored) ? stored : null
    } catch {
      return null
    }
  },

  write(preference: ThemePreference) {
    try {
      window.localStorage.setItem(THEME_STORAGE_KEY, preference)
    } catch {
      /* the choice simply won't survive a reload */
    }
  },
}

/** The stored choice, or "system" for anyone who has never made one. */
export function initialPreference(): ThemePreference {
  return themeStorage.read() ?? 'system'
}
