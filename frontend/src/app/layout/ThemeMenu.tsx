import { Moon, Sun } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuLabel, DropdownMenuRadioGroup,
  DropdownMenuRadioItem, DropdownMenuSeparator, DropdownMenuTrigger,
} from '@/shared/ui/dropdown-menu'
import { useTheme } from '@/shared/theme/useTheme'
import { themeLabels, themeOptions } from '@/shared/theme/themeOptions'
import type { ThemePreference } from '@/shared/theme/theme'

// Three choices rather than a toggle, because "follow my system" is a distinct
// answer from "always light" — and it is the one most people already expect.
export function ThemeMenu() {
  const { preference, resolved, setPreference } = useTheme()

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size="sm"
          aria-label={`Theme: ${themeLabels[preference]}`}
          title={preference === 'system' ? `System (${resolved})` : themeLabels[preference]}
        >
          {resolved === 'dark'
            ? <Moon className="h-4 w-4" aria-hidden />
            : <Sun className="h-4 w-4" aria-hidden />}
        </Button>
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end">
        <DropdownMenuLabel>Theme</DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuRadioGroup
          value={preference}
          onValueChange={(value) => setPreference(value as ThemePreference)}
        >
          {themeOptions.map(({ value, label, Icon }) => (
            <DropdownMenuRadioItem key={value} value={value}>
              <Icon aria-hidden />
              {label}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
