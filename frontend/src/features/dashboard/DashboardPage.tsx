import { useQuery } from '@tanstack/react-query'
import { api, apiErrorMessage } from '@/shared/api/client'
import type { FolderTreeNode, ImportBatchSummary } from '@/shared/api/types'
import { Card, CardBody, CardHeader, StatTile } from '@/shared/ui/card'
import { Badge } from '@/shared/ui/badge'
import { Spinner } from '@/shared/ui/spinner'
import { useAuth } from '@/shared/auth/useAuth'
import { formatDate, formatNumber } from '@/shared/lib/utils'

// The seeded QPAC project (Dip.Infrastructure/Seeding/SeedData.cs). Phase 6.6 adds
// a project switcher; until then every screen reports on this one.
const QPAC_PROJECT_ID = '11111111-1111-1111-1111-111111111111'

export function DashboardPage() {
  const { user } = useAuth()

  const folders = useQuery({
    queryKey: ['folders', QPAC_PROJECT_ID],
    queryFn: async () => {
      const { data } = await api.get<FolderTreeNode[]>(`/api/projects/${QPAC_PROJECT_ID}/folders/tree`)
      return data
    },
  })

  const imports = useQuery({
    queryKey: ['imports', QPAC_PROJECT_ID],
    queryFn: async () => {
      const { data } = await api.get<ImportBatchSummary[]>(
        `/api/projects/${QPAC_PROJECT_ID}/imports?take=10`,
      )
      return data
    },
  })

  const folderCount = folders.data ? countFolders(folders.data) : null
  const rowsRead = imports.data?.reduce((total, batch) => total + batch.rowsRead, 0) ?? null
  const rowsInserted = imports.data?.reduce((total, batch) => total + batch.rowsInserted, 0) ?? null

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold">Welcome{user?.fullName ? `, ${user.fullName}` : ''}</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Live status of the QPAC delivery platform.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile label="Folders" value={formatNumber(folderCount)} hint="Synced from Drive and uploads" />
        <StatTile label="Recent imports" value={formatNumber(imports.data?.length ?? null)} hint="Last 10 batches" />
        <StatTile label="Rows read" value={formatNumber(rowsRead)} hint="Across those batches" />
        <StatTile label="Rows inserted" value={formatNumber(rowsInserted)} />
      </div>

      <Card>
        <CardHeader title="Recent imports" description="Newest first, from every folder in the project." />
        <CardBody className="p-0">
          {imports.isPending ? <Spinner /> : null}

          {imports.isError ? (
            <p className="px-5 py-4 text-sm text-destructive">{apiErrorMessage(imports.error)}</p>
          ) : null}

          {imports.data?.length === 0 ? (
            <p className="px-5 py-6 text-sm text-muted-foreground">
              Nothing imported yet. Upload a workbook from the TIDPs page to get started.
            </p>
          ) : null}

          {imports.data?.length ? (
            <table className="w-full text-sm">
              <thead className="border-b border-border text-left text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-5 py-2 font-medium">File</th>
                  <th className="px-5 py-2 font-medium">Kind</th>
                  <th className="px-5 py-2 font-medium">Target</th>
                  <th className="px-5 py-2 font-medium">Imported</th>
                  <th className="px-5 py-2 text-right font-medium">Rows</th>
                  <th className="px-5 py-2 font-medium">State</th>
                </tr>
              </thead>
              <tbody>
                {imports.data.map((batch) => (
                  <tr key={batch.id} className="border-b border-border last:border-0">
                    <td className="px-5 py-2">{batch.fileName}</td>
                    <td className="px-5 py-2">{batch.kind}</td>
                    <td className="px-5 py-2">
                      <Badge tone={batch.target === 'Draft' ? 'warning' : 'info'}>{batch.target}</Badge>
                    </td>
                    <td className="px-5 py-2 text-muted-foreground">{formatDate(batch.importedAt)}</td>
                    <td className="px-5 py-2 text-right tabular-nums">{formatNumber(batch.rowsRead)}</td>
                    <td className="px-5 py-2">
                      <Badge tone={batch.completed ? 'success' : 'warning'}>
                        {batch.completed ? 'Completed' : 'In progress'}
                      </Badge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}
        </CardBody>
      </Card>
    </div>
  )
}

function countFolders(nodes: FolderTreeNode[]): number {
  return nodes.reduce((total, node) => total + 1 + countFolders(node.children), 0)
}
