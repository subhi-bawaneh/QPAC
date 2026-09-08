import { createContext } from 'react'
import type { SyncEventName, SyncEvents, HubStatus } from './syncHub'

export type SyncHandler<E extends SyncEventName> = (payload: SyncEvents[E]) => void

export interface SyncHubValue {
  status: HubStatus
  subscribe: <E extends SyncEventName>(event: E, handler: SyncHandler<E>) => () => void
}

export const SyncHubContext = createContext<SyncHubValue>({
  status: 'disconnected',
  subscribe: () => () => undefined,
})
