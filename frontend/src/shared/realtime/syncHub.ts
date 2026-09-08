import {
  HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel,
} from '@microsoft/signalr'
import { apiBaseUrl } from '@/shared/api/client'
import { tokenStorage } from '@/shared/auth/tokenStorage'
import type { ImportBatchSummary } from '@/shared/api/types'

// Server -> client events of Dip.Api/Hubs/ISyncNotifier.
export interface SyncEvents {
  syncStarted: { projectId: string; at: string }
  folderSynced: { projectId: string; folderId: string; path: string; files: number }
  syncFinished: { projectId: string; foldersSynced: number; filesQueued: number; error: string | null }
  fileQueued: { projectId: string; fileId: string; folderId: string }
  fileImportStarted: { projectId: string; fileId: string }
  fileImported: { projectId: string; fileId: string; folderId: string; batch: ImportBatchSummary }
  fileFailed: { projectId: string; fileId: string; folderId: string; error: string }
  recalculationFinished: { projectId: string; documents: number }
}

export type SyncEventName = keyof SyncEvents

export const syncEventNames: SyncEventName[] = [
  'syncStarted', 'folderSynced', 'syncFinished', 'fileQueued',
  'fileImportStarted', 'fileImported', 'fileFailed', 'recalculationFinished',
]

export type HubStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

// One connection per app session. accessTokenFactory is read on every (re)connect,
// so a refreshed token is picked up without rebuilding the connection.
export function createSyncConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${apiBaseUrl}/hubs/sync`, {
      accessTokenFactory: () => tokenStorage.getAccessToken() ?? '',
      withCredentials: false,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()
}

export function isConnected(connection: HubConnection | null): boolean {
  return connection?.state === HubConnectionState.Connected
}
