import { Card, CardBody, CardHeader } from '@/shared/ui/card'

/** Stands in for a screen a later phase builds, so the navigation is honest about it. */
export function PlaceholderPage({ title, phase, description }: {
  title: string
  phase: string
  description: string
}) {
  return (
    <Card>
      <CardHeader title={title} description={`Planned for phase ${phase}`} />
      <CardBody>
        <p className="text-sm text-muted-foreground">{description}</p>
      </CardBody>
    </Card>
  )
}
