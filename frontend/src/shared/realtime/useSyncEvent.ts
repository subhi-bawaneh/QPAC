import { useContext, useEffect, useRef } from 'react'
import { SyncHubContext, type SyncHandler, type SyncHubValue } from './syncHubContext'
import type { SyncEventName, SyncEvents } from './syncHub'

export function useSyncHub(): SyncHubValue {
  return useContext(SyncHubContext)
}

/** Subscribes for the lifetime of the component. */
export function useSyncEvent<E extends SyncEventName>(event: E, handler: SyncHandler<E>) {
  const { subscribe } = useSyncHub()
  const latest = useRef(handler)
  latest.current = handler

  useEffect(
    () => subscribe(event, ((payload: SyncEvents[E]) => latest.current(payload)) as SyncHandler<E>),
    [event, subscribe],
  )
}
