import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import type { HubConnection } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { invalidateFor } from './invalidation'
import { createSyncConnection, syncEventNames, type HubStatus, type SyncEventName } from './syncHub'
import { SyncHubContext, type SyncHubValue } from './syncHubContext'

// The connection is opened once, after auth, and joins the project group. Handlers
// register against a local map rather than against the connection, so a component
// mounting mid-reconnect does not lose its subscription.
export function SyncHubProvider({ projectId, enabled, children }: {
  projectId: string
  enabled: boolean
  children: ReactNode
}) {
  const [status, setStatus] = useState<HubStatus>('disconnected')
  const handlers = useRef(new Map<SyncEventName, Set<(payload: unknown) => void>>())
  const queryClient = useQueryClient()

  const subscribe = useCallback<SyncHubValue['subscribe']>((event, handler) => {
    const set = handlers.current.get(event) ?? new Set()
    set.add(handler as (payload: unknown) => void)
    handlers.current.set(event, set)
    return () => {
      set.delete(handler as (payload: unknown) => void)
    }
  }, [])

  useEffect(() => {
    if (!enabled) {
      setStatus('disconnected')
      return
    }

    const hub: HubConnection = createSyncConnection()
    let cancelled = false

    for (const name of syncEventNames) {
      hub.on(name, (payload: unknown) => {
        // Every screen's cache goes stale on these events, whichever page is open.
        invalidateFor(queryClient, name)
        for (const handler of handlers.current.get(name) ?? []) handler(payload)
      })
    }

    hub.onreconnecting(() => setStatus('reconnecting'))
    hub.onreconnected(() => {
      setStatus('connected')
      void hub.invoke('JoinProject', projectId)
    })
    hub.onclose(() => setStatus('disconnected'))

    // withAutomaticReconnect only covers drops after a successful start, so a cold
    // API at page load is retried here with a capped back-off.
    let retry: number | undefined
    let attempt = 0
    const start = () => {
      setStatus('connecting')
      hub.start()
        .then(async () => {
          if (cancelled) return
          attempt = 0
          setStatus('connected')
          await hub.invoke('JoinProject', projectId)
        })
        .catch(() => {
          // Realtime is an enhancement: every page still refetches its queries.
          if (cancelled) return
          setStatus('disconnected')
          const delay = Math.min(60_000, 5_000 * 2 ** attempt)
          attempt += 1
          retry = window.setTimeout(start, delay)
        })
    }
    start()

    return () => {
      cancelled = true
      window.clearTimeout(retry)
      void hub.stop()
    }
  }, [projectId, enabled, queryClient])

  const value = useMemo<SyncHubValue>(() => ({ status, subscribe }), [status, subscribe])
  return <SyncHubContext.Provider value={value}>{children}</SyncHubContext.Provider>
}
