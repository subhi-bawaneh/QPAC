import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/client'
import { QPAC_PROJECT_ID } from '@/shared/api/project'

export interface UserListItem {
  id: string
  email: string
  fullName: string
  isActive: boolean
  roles: string[]
  disciplineCodes: string[]
}

export interface RoleDto {
  name: string
  permissions: string[]
}

export interface ProjectSettings {
  id: string
  code: string
  name: string
  client: string
  organisation: string
  approver: string
  scheduleMode: 'Baseline' | 'WorkingPlan'
  workingPlanApprovalDays: number
  reportDate: string | null
  baselineStartDate: string
  weightPending: number
  weightSub1: number
  weightSub2: number
  weightApproved: number
}

export function useUsers(search: string) {
  return useQuery({
    queryKey: ['users', search],
    queryFn: async () => {
      const { data } = await api.get<UserListItem[]>('/api/users', {
        params: { search: search || undefined },
      })
      return data
    },
    placeholderData: (previous) => previous,
  })
}

export function useRoles() {
  return useQuery({
    queryKey: ['roles'],
    queryFn: async () => {
      const { data } = await api.get<RoleDto[]>('/api/roles')
      return data
    },
  })
}

export function useCreateUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: {
      email: string
      password: string
      fullName: string
      roles: string[]
    }) => {
      const { data } = await api.post<string>('/api/users', input)
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users'] }),
  })
}

export function useAssignRoles() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { userId: string; roles: string[] }) => {
      await api.put(`/api/users/${input.userId}/roles`, { roles: input.roles })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users'] }),
  })
}

export function useSetDisciplines() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { userId: string; disciplineCodes: string[] }) => {
      await api.put(`/api/users/${input.userId}/disciplines`, {
        disciplineCodes: input.disciplineCodes,
      })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users'] }),
  })
}

export function useProjectSettings() {
  return useQuery({
    queryKey: ['project-settings', QPAC_PROJECT_ID],
    queryFn: async () => {
      const { data } = await api.get<ProjectSettings>(
        `/api/projects/${QPAC_PROJECT_ID}/settings`,
      )
      return data
    },
  })
}

export function useUpdateProjectSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: {
      scheduleMode: string
      workingPlanApprovalDays: number
      reportDate: string | null
      weightPending: number
      weightSub1: number
      weightSub2: number
      weightApproved: number
    }) => {
      const { data } = await api.put<{
        settings: ProjectSettings
        recalculationRequired: boolean
      }>(`/api/projects/${QPAC_PROJECT_ID}/settings`, input)
      return data
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['project-settings'] })
      // The settings feed every engine, so the reports on screen are now stale.
      void queryClient.invalidateQueries({ queryKey: ['summary'] })
    },
  })
}
