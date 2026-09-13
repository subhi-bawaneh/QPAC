import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/client'
import type { Discipline, DeleteResult, ReplacePreview, TidpFile, UploadAccepted } from '@/shared/api/types'
import type { TidpFolderManifest } from './manifestBuilder'

export interface TidpFolderSyncFile {
  relativePath: string
  action: 'Added' | 'Updated' | 'Skipped' | 'Missing' | 'Failed'
  ownerName?: string
  ownerType?: string
  disciplineCode?: string
  disciplineName?: string
  error?: string
  tidpFileId?: string
  batchId?: string
  movedTo?: string
  movedFrom?: string
  rowsImported?: number
  rowsRead?: number
}

export interface TidpFolderSyncResult {
  syncId: string
  projectId: string
  rootName: string
  startedAt: string
  totalFiles: number
  added: number
  updated: number
  skipped: number
  missing: number
  failed: number
  files: TidpFolderSyncFile[]
}

// Every uploaded TIDP workbook, with its discipline and row counts. The explorer
// groups by discipline client-side; disciplines with no file still appear there, so
// the grouping is done where the discipline list is known rather than here.
export function useTidpFiles(projectId: string) {
  return useQuery({
    queryKey: ['tidp-files', projectId],
    queryFn: async () => {
      const { data } = await api.get<TidpFile[]>(`/api/projects/${projectId}/tidp-files`)
      return data
    },
  })
}

// Every discipline, whether or not a TIDP has been uploaded for it: an empty discipline
// is exactly the thing an operator needs to notice, so the explorer shows it as an empty
// folder rather than hiding it.
export function useDisciplines(projectId: string) {
  return useQuery({
    queryKey: ['disciplines', projectId],
    queryFn: async () => {
      const { data } = await api.get<Discipline[]>(`/api/projects/${projectId}/disciplines`)
      return data
    },
  })
}

export function useReplacePreview(tidpFileId: string | null) {
  return useQuery({
    queryKey: ['tidp-files', 'replace-preview', tidpFileId],
    enabled: tidpFileId !== null,
    queryFn: async () => {
      const { data } = await api.get<ReplacePreview>(`/api/tidp-files/${tidpFileId}/replace-preview`)
      return data
    },
  })
}

export function useUploadTidp(projectId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (file: File) => {
      const body = new FormData()
      body.append('file', file, file.name)
      const { data } = await api.post<UploadAccepted>(
        `/api/projects/${projectId}/tidp-files`, body)
      return data
    },
    onSuccess: () => invalidate(queryClient),
  })
}

export function useReplaceTidp() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ tidpFileId, file }: { tidpFileId: string; file: File }) => {
      const body = new FormData()
      body.append('file', file, file.name)
      const { data } = await api.put<UploadAccepted>(`/api/tidp-files/${tidpFileId}`, body)
      return data
    },
    onSuccess: () => invalidate(queryClient),
  })
}

export function useDeleteTidp() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (tidpFileId: string) => {
      const { data } = await api.delete<DeleteResult>(`/api/tidp-files/${tidpFileId}`)
      return data
    },
    onSuccess: () => invalidate(queryClient),
  })
}

export function useSyncTidpFolder(projectId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({
      manifest,
      files,
    }: {
      manifest: TidpFolderManifest
      files: Map<string, File>
    }) => {
      const body = new FormData()
      body.append('manifest', JSON.stringify(manifest))

      for (const [field, file] of files.entries()) {
        body.append(field, file)
      }

      const { data } = await api.post<TidpFolderSyncResult>(
        `/api/projects/${projectId}/tidp-folder/sync`,
        body
      )
      return data
    },
    onSuccess: () => invalidate(queryClient),
  })
}

export function useTidpFolderSyncStatus(projectId: string, syncId: string | null) {
  return useQuery({
    queryKey: ['tidp-folder-sync', projectId, syncId],
    enabled: syncId !== null,
    refetchInterval: syncId ? 500 : false,
    queryFn: async () => {
      const { data } = await api.get<TidpFolderSyncResult>(
        `/api/projects/${projectId}/tidp-folder/syncs/${syncId}`
      )
      return data
    },
  })
}

export interface TidpFolderFileDto {
  id: string
  relativePath: string
  fileName: string
  disciplineTag?: string
  sequence?: string
  lastModifiedUtc?: string
  folderStatus: string
  missingSince?: string
  status: string
  error?: string
  rowsImported?: number
  rowsRead?: number
}

export interface TidpDisciplineFolderDto {
  id: string
  relativePath: string
  folderName: string
  disciplineCode: string
  disciplineName: string
  folderStatus: string
  missingSince?: string
  files: TidpFolderFileDto[]
}

export interface TidpOwnerDto {
  id: string
  relativePath: string
  folderName: string
  sortOrder?: number
  ownerName?: string
  ownerType: string
  folderStatus: string
  missingSince?: string
  disciplines: TidpDisciplineFolderDto[]
  files: TidpFolderFileDto[]
}

export interface TidpFolderTreeDto {
  projectId: string
  ownerCount: number
  fileCount: number
  missingCount: number
  owners: TidpOwnerDto[]
}

export function useTidpFolderTree(projectId: string) {
  return useQuery({
    queryKey: ['tidp-folder-tree', projectId],
    queryFn: async () => {
      const { data } = await api.get<TidpFolderTreeDto>(
        `/api/projects/${projectId}/tidp-folder`
      )
      return data
    },
  })
}

function invalidate(queryClient: ReturnType<typeof useQueryClient>) {
  for (const key of ['tidp-files', 'disciplines', 'imports', 'documents', 'tracker', 'summary', 'findings']) {
    void queryClient.invalidateQueries({ queryKey: [key] })
  }
}
