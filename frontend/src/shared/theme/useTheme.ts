import { useContext } from 'react'
import { ThemeContext, type ThemeValue } from './themeContext'

export function useTheme(): ThemeValue {
  return useContext(ThemeContext)
}
