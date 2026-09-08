import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import type { HubConnection } from '@microsoft/signalr'
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
        for (const handler of handlers.current.get(name) ?? []) handler(payload)
      })
    }

    hub.onreconnecting(() => setStatus('reconnecting'))
    hub.onreconnected(() => {
      setStatus('connected')
      void hub.invoke('JoinProject', projectId)
    })
    hub.onclose(() => setStatus('disconnected'))

    setStatus('connecting')
    hub.start()
      .then(async () => {
        if (cancelled) return
        setStatus('connected')
        await hub.invoke('JoinProject', projectId)
      })
      .catch(() => {
        // Realtime is an enhancement: every page still refetches its queries.
        if (!cancelled) setStatus('disconnected')
      })

    return () => {
      cancelled = true
      void hub.stop()
    }
  }, [projectId, enabled])

  const value = useMemo<SyncHubValue>(() => ({ status, subscribe }), [status, subscribe])
  return <SyncHubContext.Provider value={value}>{children}</SyncHubContext.Provider>
}
