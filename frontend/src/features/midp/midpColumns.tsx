import { formatDate } from '@/shared/lib/utils'
import type { Column } from '@/features/tracker/DocumentTablePage'

/** The MIDP view: what was planned, rather than what has happened to it. */
export const midpColumns: Column[] = [
  { key: 'number', header: 'Document No', render: (row) => <span className="font-mono text-xs">{row.documentNumber}</span> },
  { key: 'title', header: 'Title', render: (row) => <span className="block max-w-sm truncate">{row.title}</span> },
  { key: 'type', header: 'Type', render: (row) => row.type },
  { key: 'discipline', header: 'Discipline', render: (row) => row.discipline },
  { key: 'trade', header: 'Trade', render: (row) => row.trade },
  { key: 'building', header: 'Building', render: (row) => row.building },
  { key: 'level', header: 'Level', render: (row) => row.level },
  { key: 'milestone', header: 'Delivery milestone', render: (row) => formatDate(row.deliveryMilestone) },
  { key: 'activity', header: 'Activity ID', render: (row) => row.activityId ?? '—' },
  { key: 'author', header: 'Author', render: (row) => row.author ?? '—' },
]
