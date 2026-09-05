import { Badge } from '@/shared/ui/badge'
import type { DraftRowState } from '@/shared/api/types'

// New/Modified/Unchanged is how the importer classified the row against Live;
// Conflict is set by a promote that refused to write it.
const tones = {
  New: 'success',
  Modified: 'warning',
  Unchanged: 'neutral',
  Deleted: 'danger',
  Conflict: 'danger',
} as const

export function DraftStateBadge({ state }: { state: DraftRowState }) {
  return <Badge tone={tones[state]}>{state}</Badge>
}
