import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/client'
import type { DataTarget, FolderDetail, FolderTreeNode } from '@/shared/api/types'

export const folderKeys = {
  tree: (projectId: string) => ['folders', 'tree', projectId] as const,
  detail: (folderId: string) => ['folders', 'detail', folderId] as const,
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

export function useRenameFolder(projectId: string) {
  const invalidate = useFolderInvalidation(projectId)
  return useMutation({
    mutationFn: async (input: { folderId: string; newName: string }) => {
      await api.put(`/api/folders/${input.folderId}/name`, { newName: input.newName })
    },
    onSuccess: (_result, input) => invalidate(input.folderId),
  })
}

export function useSetFolderTarget(projectId: string) {
  const invalidate = useFolderInvalidation(projectId)
  return useMutation({
    mutationFn: async (input: { folderId: string; target: DataTarget }) => {
      await api.put(`/api/folders/${input.folderId}/target`, { target: input.target })
    },
    onSuccess: (_result, input) => invalidate(input.folderId),
  })
}

export function useUploadFile(projectId: string) {
  const invalidate = useFolderInvalidation(projectId)
  return useMutation({
    mutationFn: async (input: { folderId: string; file: File }) => {
      const form = new FormData()
      form.append('file', input.file)
      // Content-Type is left unset so the browser adds the multipart boundary.
      await api.post(`/api/folders/${input.folderId}/files`, form, {
        headers: { 'Content-Type': undefined },
      })
    },
    onSuccess: (_result, input) => invalidate(input.folderId),
  })
}

export function useDeleteFile(projectId: string) {
  const invalidate = useFolderInvalidation(projectId)
  return useMutation({
    mutationFn: async (input: { fileId: string; folderId: string }) => {
      await api.delete(`/api/folder-files/${input.fileId}`)
    },
    onSuccess: (_result, input) => invalidate(input.folderId),
  })
}

export function useSyncFromDrive(projectId: string) {
  const invalidate = useFolderInvalidation(projectId)
  return useMutation({
    mutationFn: async (input: { startFolderDriveId?: string; downloadFiles: boolean }) => {
      const { data } = await api.post(`/api/projects/${projectId}/drive/sync`, {
        startFolderDriveId: input.startFolderDriveId ?? null,
        downloadFiles: input.downloadFiles,
      })
      return data
    },
    onSuccess: () => invalidate(),
  })
}
