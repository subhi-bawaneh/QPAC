import { Laptop, Moon, Sun, type LucideIcon } from 'lucide-react'
import { type ThemePreference } from './theme'

// The theme menu's contents as data: the order they appear in, the words on them and
// the icon each carries. Kept beside the theme module rather than inside the menu so
// it can be asserted without rendering a Radix portal.
export const themeOptions: { value: ThemePreference; label: string; Icon: LucideIcon }[] = [
  { value: 'light', label: 'Light', Icon: Sun },
  { value: 'dark', label: 'Dark', Icon: Moon },
  { value: 'system', label: 'System', Icon: Laptop },
]

export const themeLabels = Object.fromEntries(
  themeOptions.map((option) => [option.value, option.label]),
) as Record<ThemePreference, string>
