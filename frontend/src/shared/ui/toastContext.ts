import { createContext } from 'react'

export type ToastTone = 'info' | 'success' | 'warning' | 'danger'

export interface ToastValue {
  toast: (message: string, tone?: ToastTone) => void
}

export const ToastContext = createContext<ToastValue>({ toast: () => undefined })
