import { useNavigate } from 'react-router-dom'
import { Badge } from '@/shared/ui/badge'
import { formatDate } from '@/shared/lib/utils'
import {
  type TidpOwnerDto, type TidpDisciplineFolderDto, type TidpFolderFileDto,
} from './api'

interface Column {
  name: string
  label: string
  render: (item: any) => React.ReactNode
}

interface TidpListViewProps {
  items: (TidpOwnerDto | TidpDisciplineFolderDto | TidpFolderFileDto)[]
  breadcrumb: string
  onItemClick: (item: any) => void
  isGridView: boolean
}

export function TidpListView({
  items, breadcrumb, onItemClick, isGridView,
}: TidpListViewProps) {
  const navigate = useNavigate()

  const isOwner = (item: any): item is TidpOwnerDto => 'ownerName' in item
  const isDiscipline = (item: any): item is TidpDisciplineFolderDto =>
    'disciplineCode' in item && !('ownerName' in item)
  const isFile = (item: any): item is TidpFolderFileDto =>
    'relativePath' in item && !('disciplines' in item) && !('disciplineCode' in item)

  const columns: Column[] = [
    {
      name: 'name',
      label: 'Name',
      render: (item) => {
        if (isOwner(item)) return <span className="font-medium">{item.folderName}</span>
        if (isDiscipline(item)) return <span>{item.folderName}</span>
        if (isFile(item)) return <span className="text-sm">{item.fileName}</span>
        return null
      },
    },
    {
      name: 'type',
      label: 'Type',
      render: (item) => {
        if (isOwner(item)) return <Badge tone="info">Owner</Badge>
        if (isDiscipline(item)) return <Badge tone="info">Discipline</Badge>
        if (isFile(item)) return <Badge tone="neutral">File</Badge>
        return null
      },
    },
    {
      name: 'items',
      label: 'Items',
      render: (item) => {
        if (isOwner(item)) return `${item.disciplines.length + item.files.length}`
        if (isDiscipline(item)) return `${item.files.length}`
        return '—'
      },
    },
    {
      name: 'modified',
      label: 'Modified',
      render: (item) => {
        if (isFile(item) && item.lastModifiedUtc) {
          return formatDate(new Date(item.lastModifiedUtc))
        }
        return '—'
      },
    },
    {
      name: 'status',
      label: 'Status',
      render: (item) => {
        if (isFile(item)) {
          if (item.folderStatus === 'Missing') {
            return <Badge tone="danger" className="text-xs">Missing</Badge>
          }
          if (item.status) return <span className="text-xs">{item.status}</span>
        }
        return '—'
      },
    },
  ]

  if (isGridView) {
    return (
      <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
        {items.map((item) => {
          if (isOwner(item)) {
            return (
              <button
                key={item.id}
                onClick={() => onItemClick(item)}
                className="rounded-lg border border-border bg-card p-4 hover:bg-accent text-left transition-colors"
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="flex-1 min-w-0">
                    <h3 className="font-medium truncate">{item.folderName}</h3>
                    <p className="text-sm text-muted-foreground">
                      {item.disciplines.length + item.files.length} items
                    </p>
                    {item.ownerName && (
                      <p className="text-xs text-muted-foreground mt-1">{item.ownerName}</p>
                    )}
                  </div>
                </div>
              </button>
            )
          }

          if (isDiscipline(item)) {
            return (
              <button
                key={item.id}
                onClick={() => onItemClick(item)}
                className="rounded-lg border border-border bg-card p-4 hover:bg-accent text-left transition-colors"
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="flex-1 min-w-0">
                    <h3 className="font-medium truncate">{item.folderName}</h3>
                    <p className="text-sm text-muted-foreground">
                      {item.files.length} file{item.files.length !== 1 ? 's' : ''}
                    </p>
                  </div>
                </div>
              </button>
            )
          }

          if (isFile(item)) {
            return (
              <button
                key={item.id}
                onClick={() => {}}
                onDoubleClick={() => navigate(`/tidps/${item.id}`, { state: { breadcrumb } })}
                className={`rounded-lg border p-4 text-left hover:bg-accent transition-colors cursor-pointer ${
                  item.folderStatus === 'Missing' ? 'border-yellow-300 bg-yellow-50' : 'border-border bg-card'
                }`}
              >
                <h3 className="font-medium truncate text-sm">{item.fileName}</h3>
                <div className="text-xs text-muted-foreground mt-2 space-y-1">
                  {item.lastModifiedUtc && (
                    <p>Modified: {formatDate(new Date(item.lastModifiedUtc))}</p>
                  )}
                  {item.status && <p>Status: {item.status}</p>}
                  {item.rowsImported !== null && <p>Rows imported: {item.rowsImported}</p>}
                  {item.folderStatus === 'Missing' && <p className="text-yellow-700">⚠ Missing</p>}
                </div>
              </button>
            )
          }

          return null
        })}
      </div>
    )
  }

  // Table view
  return (
    <div className="border border-border rounded-lg overflow-hidden">
      <table className="w-full text-sm">
        <thead className="bg-muted border-b border-border">
          <tr>
            {columns.map((col) => (
              <th key={col.name} className="px-4 py-3 text-left font-medium">
                {col.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {items.map((item) => {
            const isFileItem = isFile(item)
            const onClick = isFileItem ? () => {} : () => onItemClick(item)
            const onDoubleClick = isFileItem ? () => navigate(`/tidps/${item.id}`, { state: { breadcrumb } }) : undefined

            return (
              <tr
                key={item.id}
                onClick={onClick}
                onDoubleClick={onDoubleClick}
                className={`border-b border-border hover:bg-accent transition-colors ${
                  isFileItem ? 'cursor-pointer' : 'cursor-pointer'
                }`}
              >
                {columns.map((col) => (
                  <td key={col.name} className="px-4 py-3">
                    {col.render(item)}
                  </td>
                ))}
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
