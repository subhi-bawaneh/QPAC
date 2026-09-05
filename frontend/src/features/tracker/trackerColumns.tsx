import { formatDate } from '@/shared/lib/utils'
import type { Column } from './DocumentTablePage'
import { StatusBadge } from './StatusBadge'

/** The computed columns of Tracker.xlsx!Tracker — what has happened to each document. */
export const trackerColumns: Column[] = [
  { key: 'number', header: 'Document No', render: (row) => <span className="font-mono text-xs">{row.documentNumber}</span> },
  { key: 'title', header: 'Title', render: (row) => <span className="block max-w-xs truncate">{row.title}</span> },
  { key: 'discipline', header: 'Discipline', render: (row) => row.discipline },
  { key: 'submissions', header: '#', align: 'right', render: (row) => row.submissionsCount ?? '—' },
  { key: 'revision', header: 'Rev', render: (row) => row.revision ?? '—' },
  { key: 'status', header: 'Status', render: (row) => <StatusBadge status={row.status} /> },
  { key: 'submissionDate', header: 'Submitted', render: (row) => formatDate(row.submissionDate) },
  { key: 'dateModified', header: 'Modified', render: (row) => formatDate(row.dateModified) },
  { key: 'plannedStart', header: 'Planned start', render: (row) => formatDate(row.plannedStart) },
  { key: 'actualFinish', header: 'Actual finish', render: (row) => formatDate(row.actualFinish) },
]
