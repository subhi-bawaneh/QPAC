import { useCallback, useMemo, useState, type ReactNode } from 'react'
import { cn } from '@/shared/lib/utils'
import { ToastContext, type ToastTone } from './toastContext'

interface Toast {
  id: number
  message: string
  tone: ToastTone
}

const toneClass: Record<ToastTone, string> = {
  info: 'border-border bg-card text-foreground',
  success: 'border-emerald-500/40 bg-emerald-50 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-100',
  warning: 'border-amber-500/40 bg-amber-50 text-amber-900 dark:bg-amber-950 dark:text-amber-100',
  danger: 'border-destructive/40 bg-destructive/10 text-destructive',
}

let nextId = 1

// A minimal shadcn-style toaster: sync and import progress arrives while the user is
// looking at something else, so it must not steal focus or block the page.
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])

  const toast = useCallback((message: string, tone: ToastTone = 'info') => {
    const id = nextId++
    setToasts((current) => [...current.slice(-3), { id, message, tone }])
    window.setTimeout(() => {
      setToasts((current) => current.filter((t) => t.id !== id))
    }, 6000)
  }, [])

  const value = useMemo(() => ({ toast }), [toast])

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div
        role="status"
        aria-live="polite"
        className="pointer-events-none fixed bottom-4 right-4 z-50 flex w-80 flex-col gap-2"
      >
        {toasts.map((item) => (
          <div
            key={item.id}
            className={cn(
              'pointer-events-auto rounded-lg border px-4 py-3 text-sm shadow-lg',
              toneClass[item.tone],
            )}
          >
            {item.message}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}
