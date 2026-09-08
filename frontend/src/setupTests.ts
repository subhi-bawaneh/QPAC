import '@testing-library/jest-dom/vitest'

// Recharts' ResponsiveContainer observes its box; jsdom has no ResizeObserver, so
// charts would throw on render. The stub reports nothing, which is enough for the
// component to mount — layout itself is not what these tests check.
class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}

globalThis.ResizeObserver ??= ResizeObserverStub as unknown as typeof ResizeObserver

// This jsdom build exposes a `localStorage` getter that resolves to undefined, so
// anything reading it sees nothing at all rather than an empty store. The app guards
// every access (private-mode browsers do the same), but the theme and token stores
// are worth testing for real, so a minimal in-memory Storage stands in.
if (window.localStorage === undefined) {
  const entries = new Map<string, string>()

  const memoryStorage: Storage = {
    get length() {
      return entries.size
    },
    key: (index: number) => [...entries.keys()][index] ?? null,
    getItem: (key: string) => entries.get(key) ?? null,
    setItem: (key: string, value: string) => {
      entries.set(key, String(value))
    },
    removeItem: (key: string) => {
      entries.delete(key)
    },
    clear: () => entries.clear(),
  }

  Object.defineProperty(window, 'localStorage', { configurable: true, value: memoryStorage })
}

