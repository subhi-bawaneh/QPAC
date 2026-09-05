import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, Pencil, Upload } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Card, CardBody } from '@/shared/ui/card'
import { Input } from '@/shared/ui/input'
import { SelectItem, SimpleSelect } from '@/shared/ui/select'
import { Spinner } from '@/shared/ui/spinner'
import { Badge } from '@/shared/ui/badge'
import { Checkbox } from '@/shared/ui/checkbox'
import { apiErrorMessage } from '@/shared/api/client'
import { formatDate } from '@/shared/lib/utils'
import { useAuth } from '@/shared/auth/useAuth'
import { Permissions } from '@/shared/auth/permissions'
import type { DraftDocument, DraftRowState } from '@/shared/api/types'
import { useDraftDocuments } from './api'
import { DraftStateBadge } from './DraftStateBadge'
import { DraftRowEditor } from './DraftRowEditor'
import { PromoteDialog } from './PromoteDialog'
import { BulkEditDialog } from './BulkEditDialog'

// Radix forbids an empty-string item value, so "no filter" needs a sentinel.
const ALL = '__all__'
const PAGE_SIZE = 25
const states: DraftRowState[] = ['New', 'Modified', 'Unchanged', 'Conflict', 'Deleted']

export function DraftReviewPage() {
  const { folderFileId = '' } = useParams()
  const { can } = useAuth()
  const canEdit = can(Permissions.draftsEdit)
  const canPromote = can(Permissions.draftsPromote)

  const [state, setState] = useState<DraftRowState | ''>('')
  const [duplicatesOnly, setDuplicatesOnly] = useState(false)
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [editing, setEditing] = useState<DraftDocument | null>(null)
  const [promoteOpen, setPromoteOpen] = useState(false)
  const [bulkOpen, setBulkOpen] = useState(false)

  const drafts = useDraftDocuments(folderFileId, {
    state: state || undefined,
    isDuplicate: duplicatesOnly ? true : undefined,
    search,
    page,
    pageSize: PAGE_SIZE,
  })

  const rows = drafts.data?.items ?? []
  const toggle = (id: string) =>
    setSelected((current) => {
      const next = new Set(current)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <Link to="/tidps" className="flex items-center gap-1 text-xs text-muted-foreground hover:underline">
            <ArrowLeft className="h-3 w-3" aria-hidden />
            Back to TIDPs
          </Link>
          <h1 className="mt-1 text-xl font-semibold">Draft review</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Nothing here has reached the Live layer yet. Edit what needs fixing, then promote.
          </p>
        </div>

        <div className="flex gap-2">
          {canEdit ? (
            <Button
              variant="outline"
              size="sm"
              disabled={selected.size === 0}
              onClick={() => setBulkOpen(true)}
            >
              <Pencil className="h-4 w-4" aria-hidden />
              Edit {selected.size || ''} selected
            </Button>
          ) : null}
          {canPromote ? (
            <Button size="sm" onClick={() => setPromoteOpen(true)}>
              <Upload className="h-4 w-4" aria-hidden />
              Promote to Live
            </Button>
          ) : null}
        </div>
      </div>

      <Card>
        <div className="flex flex-wrap items-center gap-3 border-b border-border px-5 py-3">
          <Input
            className="h-8 w-64"
            placeholder="Search number or title"
            aria-label="Search draft rows"
            value={search}
            onChange={(event) => {
              setSearch(event.target.value)
              setPage(1)
            }}
          />

          <SimpleSelect
            className="h-8 w-40"
            label="Filter by state"
            value={state || ALL}
            onValueChange={(value) => {
              setState(value === ALL ? '' : (value as DraftRowState))
              setPage(1)
            }}
          >
            <SelectItem value={ALL}>All states</SelectItem>
            {states.map((value) => <SelectItem key={value} value={value}>{value}</SelectItem>)}
          </SimpleSelect>

          <label className="flex items-center gap-2 text-sm text-muted-foreground">
            <Checkbox
              checked={duplicatesOnly}
              aria-label="Duplicates only"
              onCheckedChange={(checked) => {
                setDuplicatesOnly(checked === true)
                setPage(1)
              }}
            />
            Duplicates only
          </label>

          <span className="ml-auto text-xs text-muted-foreground">
            {drafts.data ? `${drafts.data.total.toLocaleString('en-GB')} rows` : ''}
          </span>
        </div>

        <CardBody className="p-0">
          {drafts.isPending ? <Spinner /> : null}
          {drafts.isError ? (
            <p className="px-5 py-4 text-sm text-destructive">{apiErrorMessage(drafts.error)}</p>
          ) : null}

          {drafts.data && rows.length === 0 ? (
            <p className="px-5 py-6 text-sm text-muted-foreground">
              No draft rows match these filters.
            </p>
          ) : null}

          {rows.length ? (
            <table className="w-full text-sm">
              <thead className="border-b border-border text-left text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="w-10 px-3 py-2"><span className="sr-only">Select</span></th>
                  <th className="px-3 py-2 font-medium">Document No</th>
                  <th className="px-3 py-2 font-medium">Title</th>
                  <th className="px-3 py-2 font-medium">Discipline</th>
                  <th className="px-3 py-2 font-medium">State</th>
                  <th className="px-3 py-2 font-medium">Updated</th>
                  <th className="px-3 py-2 text-right font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.id} className="border-b border-border last:border-0">
                    <td className="px-3 py-2">
                      <Checkbox
                        aria-label={`Select ${row.documentNumber}`}
                        checked={selected.has(row.id)}
                        onCheckedChange={() => toggle(row.id)}
                      />
                    </td>
                    <td className="px-3 py-2 font-mono text-xs">
                      {row.documentNumber}
                      {row.isDuplicate ? <span className="ml-2"><Badge tone="danger">Duplicate</Badge></span> : null}
                    </td>
                    <td className="max-w-sm truncate px-3 py-2">{row.title}</td>
                    <td className="px-3 py-2 text-muted-foreground">{row.corporateDiscipline}</td>
                    <td className="px-3 py-2">
                      <DraftStateBadge state={row.state} />
                      {row.conflictReason ? (
                        <p className="mt-1 max-w-xs text-xs text-destructive">{row.conflictReason}</p>
                      ) : null}
                    </td>
                    <td className="px-3 py-2 text-muted-foreground">{formatDate(row.updatedAt)}</td>
                    <td className="px-3 py-2 text-right">
                      {canEdit ? (
                        <Button size="sm" variant="ghost" onClick={() => setEditing(row)}>
                          <Pencil className="h-3 w-3" aria-hidden />
                          Edit
                        </Button>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}
        </CardBody>

        {drafts.data && drafts.data.totalPages > 1 ? (
          <div className="flex items-center justify-between border-t border-border px-5 py-3 text-sm">
            <span className="text-muted-foreground">
              Page {drafts.data.page} of {drafts.data.totalPages}
            </span>
            <div className="flex gap-2">
              <Button
                size="sm"
                variant="outline"
                disabled={drafts.data.page <= 1}
                onClick={() => setPage((value) => value - 1)}
              >
                Previous
              </Button>
              <Button
                size="sm"
                variant="outline"
                disabled={drafts.data.page >= drafts.data.totalPages}
                onClick={() => setPage((value) => value + 1)}
              >
                Next
              </Button>
            </div>
          </div>
        ) : null}
      </Card>

      <DraftRowEditor row={editing} folderFileId={folderFileId} onClose={() => setEditing(null)} />

      <PromoteDialog
        open={promoteOpen}
        onClose={() => setPromoteOpen(false)}
        folderFileId={folderFileId}
      />

      <BulkEditDialog
        open={bulkOpen}
        onClose={() => setBulkOpen(false)}
        folderFileId={folderFileId}
        ids={[...selected]}
      />
    </div>
  )
}
