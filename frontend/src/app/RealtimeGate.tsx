import type { ReactNode } from 'react'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import { useAuth } from '@/shared/auth/useAuth'
import { SyncHubProvider } from '@/shared/realtime/SyncHubProvider'

// The hub needs a token, so the connection only opens once the user is signed in.
export function RealtimeGate({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  return (
    <SyncHubProvider projectId={QPAC_PROJECT_ID} enabled={user !== null}>
      {children}
    </SyncHubProvider>
  )
}
