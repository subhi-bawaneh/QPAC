import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  applyTheme, initialPreference, isThemePreference, resolveTheme, systemPrefersDark,
  themePreferences, themeStorage, THEME_STORAGE_KEY,
} from '../theme'

afterEach(() => {
  window.localStorage.clear()
  document.documentElement.classList.remove('dark')
  document.documentElement.style.colorScheme = ''
  vi.unstubAllGlobals()
})

function stubMatchMedia(matches: boolean) {
  vi.stubGlobal('matchMedia', vi.fn().mockReturnValue({
    matches,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
  }))
}

describe('resolveTheme', () => {
  it('follows the system setting when the preference is "system"', () => {
    expect(resolveTheme('system', true)).toBe('dark')
    expect(resolveTheme('system', false)).toBe('light')
  })

  it('overrides the system setting when the user picked a theme', () => {
    expect(resolveTheme('light', true)).toBe('light')
    expect(resolveTheme('dark', false)).toBe('dark')
  })
})

describe('systemPrefersDark', () => {
  it('reads the prefers-color-scheme media query', () => {
    stubMatchMedia(true)
    expect(systemPrefersDark()).toBe(true)
    expect(window.matchMedia).toHaveBeenCalledWith('(prefers-color-scheme: dark)')
  })

  it('is false when the system asks for light', () => {
    stubMatchMedia(false)
    expect(systemPrefersDark()).toBe(false)
  })

  it('does not throw where matchMedia is missing', () => {
    vi.stubGlobal('matchMedia', undefined)
    expect(systemPrefersDark()).toBe(false)
  })
})

describe('applyTheme', () => {
  it('puts the dark class and colour scheme on the document', () => {
    applyTheme('dark')

    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(document.documentElement.style.colorScheme).toBe('dark')
  })

  it('takes them off again for light', () => {
    applyTheme('dark')
    applyTheme('light')

    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(document.documentElement.style.colorScheme).toBe('light')
  })
})

describe('themeStorage', () => {
  it('round-trips a preference through local storage', () => {
    themeStorage.write('dark')

    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')
    expect(themeStorage.read()).toBe('dark')
  })

  it('ignores a value that is not one of the three preferences', () => {
    window.localStorage.setItem(THEME_STORAGE_KEY, 'neon')

    expect(themeStorage.read()).toBeNull()
    expect(initialPreference()).toBe('system')
  })

  it('falls back to the system default when nothing is stored', () => {
    expect(themeStorage.read()).toBeNull()
    expect(initialPreference()).toBe('system')
  })

  it('reads back what was stored on the last visit', () => {
    window.localStorage.setItem(THEME_STORAGE_KEY, 'light')

    expect(initialPreference()).toBe('light')
  })

  it('survives storage being blocked, as it is in private mode', () => {
    const getItem = vi.spyOn(window.localStorage, 'getItem')
      .mockImplementation(() => { throw new DOMException('denied') })
    const setItem = vi.spyOn(window.localStorage, 'setItem')
      .mockImplementation(() => { throw new DOMException('denied') })

    expect(() => themeStorage.write('dark')).not.toThrow()
    expect(themeStorage.read()).toBeNull()
    expect(initialPreference()).toBe('system')

    getItem.mockRestore()
    setItem.mockRestore()
  })
})

describe('the preference vocabulary', () => {
  it('is the three the menu offers', () => {
    expect(themePreferences).toEqual(['light', 'dark', 'system'])
  })

  it('recognises only those three', () => {
    expect(isThemePreference('system')).toBe(true)
    expect(isThemePreference('sepia')).toBe(false)
    expect(isThemePreference(null)).toBe(false)
  })

  // The inline script in index.html applies the theme before React can run, and it
  // has to look in the same place. If this key ever moves, that script moves with it.
  it('uses the key the pre-paint script in index.html reads', () => {
    expect(THEME_STORAGE_KEY).toBe('dip.theme')
  })
})
