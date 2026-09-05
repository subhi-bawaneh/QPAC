import { useEffect, useState } from 'react'
import { Button } from '@/shared/ui/button'
import { Card, CardBody, CardHeader } from '@/shared/ui/card'
import { Input } from '@/shared/ui/input'
import { Select } from '@/shared/ui/select'
import { Spinner } from '@/shared/ui/spinner'
import { Badge } from '@/shared/ui/badge'
import { apiErrorMessage } from '@/shared/api/client'
import { formatDate } from '@/shared/lib/utils'
import { useRoles, useProjectSettings, useUpdateProjectSettings } from './api'

interface FormState {
  scheduleMode: 'Baseline' | 'WorkingPlan'
  workingPlanApprovalDays: string
  reportDate: string
  weightPending: string
  weightSub1: string
  weightSub2: string
  weightApproved: string
}

export function SettingsPage() {
  const settings = useProjectSettings()
  const roles = useRoles()
  const update = useUpdateProjectSettings()

  const [form, setForm] = useState<FormState | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState<{ recalculationRequired: boolean } | null>(null)

  useEffect(() => {
    if (!settings.data) return
    setForm({
      scheduleMode: settings.data.scheduleMode,
      workingPlanApprovalDays: String(settings.data.workingPlanApprovalDays),
      reportDate: settings.data.reportDate ? settings.data.reportDate.slice(0, 10) : '',
      weightPending: String(settings.data.weightPending),
      weightSub1: String(settings.data.weightSub1),
      weightSub2: String(settings.data.weightSub2),
      weightApproved: String(settings.data.weightApproved),
    })
  }, [settings.data])

  const set = (key: keyof FormState, value: string) =>
    setForm((current) => (current ? { ...current, [key]: value } : current))

  const onSave = async () => {
    if (!form) return
    setError(null)
    setSaved(null)
    try {
      const result = await update.mutateAsync({
        scheduleMode: form.scheduleMode,
        workingPlanApprovalDays: Number(form.workingPlanApprovalDays),
        // An empty report date means "as at today", which is what the engines do.
        reportDate: form.reportDate ? `${form.reportDate}T00:00:00` : null,
        weightPending: Number(form.weightPending),
        weightSub1: Number(form.weightSub1),
        weightSub2: Number(form.weightSub2),
        weightApproved: Number(form.weightApproved),
      })
      setSaved({ recalculationRequired: result.recalculationRequired })
    } catch (caught) {
      setError(apiErrorMessage(caught, 'Could not save the settings'))
    }
  }

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">Settings</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          What the engines assume when they compute every report.
        </p>
      </div>

      {settings.isPending ? <Spinner /> : null}
      {settings.isError ? (
        <p className="text-sm text-destructive">{apiErrorMessage(settings.error)}</p>
      ) : null}

      {settings.data && form ? (
        <>
          <Card>
            <CardHeader title="Project" description={`${settings.data.code} — ${settings.data.name}`} />
            <CardBody>
              <dl className="grid gap-3 text-sm sm:grid-cols-3">
                <div>
                  <dt className="text-xs text-muted-foreground">Client</dt>
                  <dd>{settings.data.client || '—'}</dd>
                </div>
                <div>
                  <dt className="text-xs text-muted-foreground">Organisation</dt>
                  <dd>{settings.data.organisation || '—'}</dd>
                </div>
                <div>
                  <dt className="text-xs text-muted-foreground">Baseline start</dt>
                  <dd>{formatDate(settings.data.baselineStartDate)}</dd>
                </div>
              </dl>
            </CardBody>
          </Card>

          <Card>
            <CardHeader
              title="Schedule"
              description="Baseline takes planned dates from the programme; Working Plan derives them from each document's delivery milestone."
            />
            <CardBody className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-3">
                <label className="space-y-1 text-sm">
                  <span className="text-xs text-muted-foreground">Schedule mode</span>
                  <Select
                    aria-label="Schedule mode"
                    value={form.scheduleMode}
                    onChange={(event) => set('scheduleMode', event.target.value)}
                  >
                    <option value="Baseline">Baseline</option>
                    <option value="WorkingPlan">Working Plan</option>
                  </Select>
                </label>

                <label className="space-y-1 text-sm">
                  <span className="text-xs text-muted-foreground">Approval days (Working Plan)</span>
                  <Input
                    type="number"
                    min={1}
                    max={365}
                    aria-label="Approval days"
                    value={form.workingPlanApprovalDays}
                    onChange={(event) => set('workingPlanApprovalDays', event.target.value)}
                  />
                </label>

                <label className="space-y-1 text-sm">
                  <span className="text-xs text-muted-foreground">Report date</span>
                  <Input
                    type="date"
                    aria-label="Report date"
                    value={form.reportDate}
                    onChange={(event) => set('reportDate', event.target.value)}
                  />
                </label>
              </div>
              <p className="text-xs text-muted-foreground">
                Leave the report date empty to report as at today.
              </p>
            </CardBody>
          </Card>

          <Card>
            <CardHeader
              title="Progress weights"
              description="How much credit a document earns at each stage, for Planned and Earned Value. They must not decrease."
            />
            <CardBody>
              <div className="grid gap-4 sm:grid-cols-4">
                {([
                  ['weightPending', 'Pending'],
                  ['weightSub1', 'Submitted once'],
                  ['weightSub2', 'Submitted twice or more'],
                  ['weightApproved', 'Approved'],
                ] as const).map(([key, label]) => (
                  <label key={key} className="space-y-1 text-sm">
                    <span className="text-xs text-muted-foreground">{label}</span>
                    <Input
                      type="number"
                      step="0.05"
                      min={0}
                      max={1}
                      aria-label={label}
                      value={form[key]}
                      onChange={(event) => set(key, event.target.value)}
                    />
                  </label>
                ))}
              </div>
            </CardBody>
          </Card>

          {error ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{error}</p>
          ) : null}

          {saved ? (
            <p className="rounded-md bg-emerald-100 px-3 py-2 text-sm text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200">
              Settings saved.
              {saved.recalculationRequired
                ? ' The planned dates stored against each document are now out of date — recalculate from the Summary page.'
                : ' The reports pick this up immediately.'}
            </p>
          ) : null}

          <div className="flex justify-end">
            <Button onClick={() => void onSave()} disabled={update.isPending}>
              {update.isPending ? 'Saving…' : 'Save settings'}
            </Button>
          </div>

          <Card>
            <CardHeader title="Roles" description="Seeded from RoleDefinitions; each grants these permissions." />
            <CardBody className="p-0">
              {roles.isPending ? <Spinner /> : null}
              {roles.data?.length ? (
                <table className="w-full text-sm">
                  <thead className="border-b border-border text-left text-xs uppercase text-muted-foreground">
                    <tr>
                      <th className="px-5 py-2 font-medium">Role</th>
                      <th className="px-5 py-2 font-medium">Permissions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {roles.data.map((role) => (
                      <tr key={role.name} className="border-b border-border last:border-0">
                        <td className="px-5 py-2 align-top font-medium">{role.name}</td>
                        <td className="px-5 py-2">
                          <span className="flex flex-wrap gap-1">
                            {role.permissions.map((permission) => (
                              <Badge key={permission}>{permission}</Badge>
                            ))}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : null}
            </CardBody>
          </Card>
        </>
      ) : null}
    </div>
  )
}
