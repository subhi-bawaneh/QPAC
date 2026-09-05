import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Card, CardBody, CardHeader } from '@/shared/ui/card'
import { Badge } from '@/shared/ui/badge'
import { Spinner } from '@/shared/ui/spinner'
import { Tabs, TabPanel } from '@/shared/ui/tabs'
import { api, apiErrorMessage } from '@/shared/api/client'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import type { PicklistGroup, StatusMappingRow } from '@/shared/api/types'
import { StatusBadge } from '@/features/tracker/StatusBadge'

export function ListsPage() {
  const [tab, setTab] = useState('picklists')

  const picklists = useQuery({
    queryKey: ['picklists', QPAC_PROJECT_ID],
    queryFn: async () => {
      const { data } = await api.get<PicklistGroup[]>(`/api/projects/${QPAC_PROJECT_ID}/picklists`)
      return data
    },
  })

  const mappings = useQuery({
    queryKey: ['status-mappings', QPAC_PROJECT_ID],
    queryFn: async () => {
      const { data } = await api.get<StatusMappingRow[]>(
        `/api/projects/${QPAC_PROJECT_ID}/status-mappings`,
      )
      return data
    },
  })

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">Lists</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          The allowed values behind document numbering, and the table that maps every
          Aconex status onto one of the four unified ones.
        </p>
      </div>

      <Tabs
        active={tab}
        onChange={setTab}
        items={[
          { id: 'picklists', label: 'Picklists', count: picklists.data?.length },
          { id: 'statuses', label: 'Status mapping', count: mappings.data?.length },
        ]}
      />

      <TabPanel id="picklists" active={tab}>
        {picklists.isPending ? <Spinner /> : null}
        {picklists.isError ? (
          <p className="text-sm text-destructive">{apiErrorMessage(picklists.error)}</p>
        ) : null}

        {picklists.data?.length === 0 ? (
          <Card><CardBody>
            <p className="text-sm text-muted-foreground">
              No picklists imported yet. Import a picklists workbook from the TIDPs page.
            </p>
          </CardBody></Card>
        ) : null}

        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {picklists.data?.map((group) => (
            <Card key={group.field}>
              <CardHeader title={group.field} description={`${group.items.length} value(s)`} />
              <CardBody className="max-h-64 overflow-y-auto p-0">
                <ul className="divide-y divide-border text-sm">
                  {group.items.map((item) => (
                    <li key={item.id} className="flex items-baseline gap-2 px-5 py-1.5">
                      <span className="font-mono text-xs">{item.code}</span>
                      <span className="truncate text-muted-foreground">{item.description}</span>
                    </li>
                  ))}
                </ul>
              </CardBody>
            </Card>
          ))}
        </div>
      </TabPanel>

      <TabPanel id="statuses" active={tab}>
        <Card>
          <CardBody className="p-0">
            {mappings.isPending ? <Spinner /> : null}
            {mappings.isError ? (
              <p className="px-5 py-4 text-sm text-destructive">{apiErrorMessage(mappings.error)}</p>
            ) : null}

            {mappings.data?.length ? (
              <table className="w-full text-sm">
                <thead className="border-b border-border text-left text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="px-5 py-2 font-medium">Aconex status</th>
                    <th className="px-5 py-2 font-medium">Unified status</th>
                    <th className="px-5 py-2 font-medium">Note</th>
                  </tr>
                </thead>
                <tbody>
                  {mappings.data.map((mapping) => (
                    <tr key={mapping.id} className="border-b border-border last:border-0">
                      <td className="px-5 py-2">{mapping.aconexStatus}</td>
                      <td className="px-5 py-2"><StatusBadge status={mapping.status} /></td>
                      <td className="px-5 py-2">
                        {/* Older exports spell some statuses differently; those rows stay
                            so a backfill still maps, but they are not the current form. */}
                        {mapping.isLegacy ? <Badge tone="neutral">Legacy spelling</Badge> : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : null}
          </CardBody>
        </Card>
      </TabPanel>
    </div>
  )
}
