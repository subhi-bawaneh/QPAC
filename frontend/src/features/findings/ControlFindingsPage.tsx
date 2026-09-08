import { ControlFindingsPanel } from './ControlFindingsPanel'

export function ControlFindingsPage() {
  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">Control Findings</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          The four anomaly reports from the Engineering Tracker, computed over the documents
          every targeted company contributes.
        </p>
      </div>
      <ControlFindingsPanel />
    </div>
  )
}
