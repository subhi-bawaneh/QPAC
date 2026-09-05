import { DocumentTablePage } from './DocumentTablePage'
import { trackerColumns } from './trackerColumns'

export function TrackerPage() {
  return (
    <DocumentTablePage
      title="Tracker"
      description="Every planned document with its latest Aconex state. Select a row for its full revision history."
      columns={trackerColumns}
      exportKind="Tracker"
      showStatusFilter
    />
  )
}
