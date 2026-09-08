import { useContext } from 'react'
import { ToastContext, type ToastValue } from './toastContext'

export function useToast(): ToastValue {
  return useContext(ToastContext)
}
