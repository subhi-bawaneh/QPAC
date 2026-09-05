import { Badge } from '@/shared/ui/badge'
import type { UnifiedStatus } from '@/shared/api/types'

const tones = {
  Approved: 'success',
  Rejected: 'danger',
  UnderReview: 'warning',
  Withdrawn: 'neutral',
} as const

// The sheet's own labels; the enum name has no space.
const labels = {
  Approved: 'Approved',
  Rejected: 'Rejected',
  UnderReview: 'Under Review',
  Withdrawn: 'Withdrawn',
} as const

export function StatusBadge({ status }: { status: UnifiedStatus | null }) {
  if (!status) return <span className="text-xs text-muted-foreground">—</span>
  return <Badge tone={tones[status]}>{labels[status]}</Badge>
}
