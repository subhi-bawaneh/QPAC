import { useState } from 'react'
import { Card, CardBody } from '@/shared/ui/card'
import { Checkbox } from '@/shared/ui/checkbox'
import { Spinner } from '@/shared/ui/spinner'
import { Tabs, TabPanel } from '@/shared/ui/tabs'
import { apiErrorMessage } from '@/shared/api/client'
import { useAuth } from '@/shared/auth/useAuth'
import { Permissions } from '@/shared/auth/permissions'
import type { PicklistField } from '@/shared/api/types'
import { PicklistTable } from './PicklistTable'
import { StatusMappingTable } from './StatusMappingTable'
import { picklistFields, picklistLabels, STATUS_MAPPING_TAB } from './listLabels'
import { usePicklists, useStatusMappings } from './api'

// One tab per list in the Picklists workbook plus the status mapping — 18 in all.
export function ListsPage() {
  const { can } = useAuth()
  const canManage = can(Permissions.listsManage)

  const [tab, setTab] = useState<string>(picklistFields[0])
  const [showDeleted, setShowDeleted] = useState(false)

  const picklists = usePicklists(showDeleted)
  const mappings = useStatusMappings(showDeleted)

  const itemsFor = (field: PicklistField) =>
    picklists.data?.find((group) => group.field === field)?.items ?? []

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold">Lists</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Every dropdown the Picklists workbook defines, and the table that maps each
            Aconex status onto one of the four unified ones. A value deleted here is not
            brought back by re-importing the workbook.
          </p>
        </div>

        <label className="flex items-center gap-2 text-sm">
          <Checkbox
            checked={showDeleted}
            onCheckedChange={(value) => setShowDeleted(value === true)}
            aria-label="Show deleted"
          />
          Show deleted
        </label>
      </div>

      <div className="overflow-x-auto pb-1">
        <Tabs
          active={tab}
          onChange={setTab}
          items={[
            ...picklistFields.map((field) => ({
              id: field,
              label: picklistLabels[field],
              count: picklists.data ? itemsFor(field).filter((item) => !item.isDeleted).length : undefined,
            })),
            {
              id: STATUS_MAPPING_TAB,
              label: 'Status mapping',
              count: mappings.data?.filter((row) => !row.isDeleted).length,
            },
          ]}
        />
      </div>

      {picklists.isError ? (
        <p className="text-sm text-destructive">{apiErrorMessage(picklists.error)}</p>
      ) : null}

      {picklistFields.map((field) => (
        <TabPanel key={field} id={field} active={tab}>
          {picklists.isPending ? <Spinner /> : (
            <PicklistTable
              field={field}
              items={itemsFor(field)}
              canManage={canManage}
              showDeleted={showDeleted}
            />
          )}
        </TabPanel>
      ))}

      <TabPanel id={STATUS_MAPPING_TAB} active={tab}>
        {mappings.isPending ? <Spinner /> : null}
        {mappings.isError ? (
          <Card><CardBody>
            <p className="text-sm text-destructive">{apiErrorMessage(mappings.error)}</p>
          </CardBody></Card>
        ) : null}
        {mappings.data ? (
          <StatusMappingTable rows={mappings.data} canManage={canManage} />
        ) : null}
      </TabPanel>
    </div>
  )
}
