import { Loader2 } from 'lucide-react'
import { cn } from '@/shared/lib/utils'

export function Spinner({ label, className }: { label?: string; className?: string }) {
  return (
    <div className={cn('flex items-center justify-center gap-3 p-10 text-sm text-muted-foreground', className)}>
      <Loader2 className="h-4 w-4 animate-spin" aria-hidden />
      <span role="status">{label ?? 'Loading…'}</span>
    </div>
  )
}

/** Placeholder block for content that is still loading, sized by the caller. */
export function Skeleton({ className }: { className?: string }) {
  return <div className={cn('animate-pulse rounded-md bg-muted', className)} />
}
