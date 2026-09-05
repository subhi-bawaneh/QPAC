import { DocumentTablePage } from '@/features/tracker/DocumentTablePage'
import { midpColumns } from './midpColumns'

export function MidpPage() {
  return (
    <DocumentTablePage
      title="MIDP"
      description="The master delivery plan: every document, its numbering and its planned delivery."
      columns={midpColumns}
      exportKind="Tracker"
      showStatusFilter={false}
    />
  )
}
