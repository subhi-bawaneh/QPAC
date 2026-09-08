import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/client'
import type {
  ConvertToLiveResult, DataTarget, DriveStatus, FolderDetail, FolderTreeNode,
  PromoteDiff, SetTargetResult, TriggerSyncResult, UploadResult,
} from '@/shared/api/types'

export const folderKeys = {
  tree: (projectId: string) => ['folders', 'tree', projectId] as const,
  detail: (folderId: string) => ['folders', 'detail', folderId] as const,
  driveStatus: (projectId: string) => ['drive-status', projectId] as const,
}

export function useFolderTree(projectId: string) {
  return useQuery({
    queryKey: folderKeys.tree(projectId),
    queryFn: async () => {
      const { data } = await api.get<FolderTreeNode[]>(`/api/projects/${projectId}/folders/tree`)
      return data
    },
  })
}

export function useFolderDetail(folderId: string | null) {
  return useQuery({
    queryKey: folderKeys.detail(folderId ?? 'none'),
    enabled: folderId !== null,
    queryFn: async () => {
      const { data } = await api.get<FolderDetail>(`/api/folders/${folderId}`)
      return data
    },
  })
}

export function useDriveStatus(projectId: string) {
  return useQuery({
    queryKey: folderKeys.driveStatus(projectId),
    queryFn: async () => {
      const { data } = await api.get<DriveStatus>(`/api/projects/${projectId}/drive/status`)
      return data
    },
    // The hub pushes the interesting transitions; this is the fallback and the
    // first paint.
    refetchInterval: 60_000,
  })
}

/** Invalidates the tree and the affected folder, so counts and badges follow a change. */
function useFolderInvalidation(projectId: string) {
  const queryClient = useQueryClient()
  return (folderId?: string) => {
    void queryClient.invalidateQueries({ queryKey: folderKeys.tree(projectId) })
    if (folderId) void queryClient.invalidateQueries({ queryKey: folderKeys.detail(folderId) })
  }
}

export function useCreateFolder(projectId: string) {
  const invalidate = useFolderInvalidation(projectId)
  return useMutation({
    mutationFn: async (input: { parentId: string | null; name: string }) => {
      const { data } = await api.post<string>('/api/folders', { projectId, ...input })
      return data
    },
    onSuccess: (_id, input) => invalidate(input.parentId ?? undefined),
  })
}

export function useSetFolderTarget(projectId: string) {
  const invalidate = useFolderInvalidation(projectId)
  return useMutation({
    mutationFn: async (input: { folderId: string; target: DataTarget }) => {
      const { data } = await api.put<SetTargetResult>(
        `/api/folders/${input.folderId}/target`, { target: input.target })
      return data
    },
    onSuccess: (_result, input) => invalidate(input.folderId),
  })
}

export function useSetFolderCompany(projectId: string) {
  const invalidate = useFolderInvalidation(projectId)
  return useMutation({
    mutationFn: async (input: { folderId: string; isCompany: boolean; authorId: string | null }) => {
      const { data } = await api.put(`/api/folders/${input.folderId}/company`, {
        isCompany: input.isCompany,
        authorId: input.authorId,
      })
      return data
    },
    onSuccess: (_result, input) => invalidate(input.folderId),
  })
}

export function useConvertToLive(projectId: string) {
  const invalidate = useFolderInvalidation(projectId)
  return useMutation({
    mutationFn: async (folderId: string) => {
      const { data } = await api.post<ConvertToLiveResult>(`/api/folders/${folderId}/convert-to-live`)
      return data
    },
    onSuccess: (_result, folderId) => invalidate(folderId),
  })
}

export function useUploadFile(projectId: string) {
  const invalidate = useFolderInvalidation(projectId)
  return useMutation({
    mutationFn: async (input: { folderId: string; file: File }) => {
      const form = new FormData()
      form.append('file', input.file)
      // Content-Type is left unset so the browser adds the multipart boundary.
      const { data } = await api.post<UploadResult>(`/api/folders/${input.folderId}/files`, form, {
        headers: { 'Content-Type': undefined },
      })
      return data
    },
    onSuccess: (_result, input) => invalidate(input.folderId),
  })
}

export function useTriggerSync(projectId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => {
      const { data } = await api.post<TriggerSyncResult>(`/api/projects/${projectId}/drive/sync`)
      return data
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: folderKeys.driveStatus(projectId) })
    },
  })
}

export function usePromoteDiff(folderFileId: string | null) {
  return useQuery({
    queryKey: ['drafts', 'diff', folderFileId ?? 'none'],
    enabled: folderFileId !== null,
    queryFn: async () => {
      const { data } = await api.get<PromoteDiff>(
        `/api/drafts/promote-diff?folderFileId=${folderFileId}`)
      return data
    },
  })
}

export function downloadUrl(fileId: string) {
  return `/api/folder-files/${fileId}/download`
}
