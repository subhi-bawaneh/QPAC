import { describe, expect, it } from 'vitest'
import { createSyncConnection, syncEventNames } from '../syncHub'
import { apiBaseUrl } from '@/shared/api/client'

describe('createSyncConnection', () => {
  it('targets the API hub and knows every server event', () => {
    const connection = createSyncConnection()

    expect(connection.baseUrl).toBe(`${apiBaseUrl}/hubs/sync`)
    expect(syncEventNames).toEqual([
      'syncStarted', 'folderSynced', 'syncFinished', 'fileQueued',
      'fileImportStarted', 'fileImported', 'fileFailed', 'recalculationFinished',
    ])
  })
})
