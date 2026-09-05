import type { FileKind, ImportKind } from '@/shared/api/types'

export const importKinds: { value: ImportKind; label: string }[] = [
  { value: 'Tidp', label: 'TIDP' },
  { value: 'Midp', label: 'MIDP' },
  { value: 'AconexHistory', label: 'Aconex history' },
  { value: 'Baseline', label: 'Baseline' },
  { value: 'Picklists', label: 'Picklists' },
  { value: 'Lists', label: 'Lists' },
]

/** The kind detected at upload is the default; Unknown falls back to TIDP. */
export function defaultKindFor(kind: FileKind): ImportKind {
  return kind === 'Unknown' ? 'Tidp' : kind
}
