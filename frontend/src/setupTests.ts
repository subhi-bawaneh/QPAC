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
