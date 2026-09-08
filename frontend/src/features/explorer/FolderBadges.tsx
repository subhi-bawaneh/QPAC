import { Badge } from '@/shared/ui/badge'
import type { DataTarget, FileKind, ImportState } from '@/shared/api/types'

/** Live vs Draft decides which layer this folder's rows count in (refactor-plan § 3 R8). */
export function TargetBadge({ target }: { target: DataTarget }) {
  return <Badge tone={target === 'Draft' ? 'warning' : 'info'}>{target === 'Draft' ? 'DB1 Draft' : 'DB2 Live'}</Badge>
}

const stateTone = {
  NotImported: 'neutral',
  Imported: 'success',
  Outdated: 'warning',
  Failed: 'danger',
} as const

const stateLabel = {
  NotImported: 'Not imported',
  Imported: 'Imported',
  Outdated: 'Outdated',
  Failed: 'Failed',
} as const

export function ImportStateBadge({ state }: { state: ImportState }) {
  return <Badge tone={stateTone[state]}>{stateLabel[state]}</Badge>
}

const kindLabel: Record<FileKind, string> = {
  Unknown: 'Unknown',
  Tidp: 'TIDP',
  Midp: 'MIDP',
  Baseline: 'Baseline',
  AconexHistory: 'Aconex history',
  Lists: 'Lists',
  Picklists: 'Picklists',
}

export function FileKindBadge({ kind }: { kind: FileKind }) {
  return <Badge tone={kind === 'Unknown' ? 'neutral' : 'info'}>{kindLabel[kind]}</Badge>
}

/** Drive has refreshed the draft since the last promote — Convert can run again. */
export function NewerDraftBadge() {
  return <Badge tone="warning">Drive has newer data</Badge>
}
