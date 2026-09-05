// Hand-written mirrors of the API DTOs. Phase 6.x replaces these with
// `npm run gen:api` output from swagger.json (PLAN.md § 8).

export interface UserSummary {
  id: string
  email: string
  fullName: string
  roles: string[]
  permissions: string[]
  disciplineCodes: string[]
}

export interface AuthResult {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
  user: UserSummary
}

export type DataTarget = 'Live' | 'Draft'
export type ImportState = 'NotImported' | 'Imported' | 'Outdated' | 'Failed'
export type FileKind = 'Unknown' | 'Tidp' | 'Midp' | 'Baseline' | 'AconexHistory' | 'Lists' | 'Picklists'

export type FileSource = 'Drive' | 'Upload'
export type ImportKind = 'Tidp' | 'Midp' | 'AconexHistory' | 'Baseline' | 'Picklists' | 'Lists'

/** A node of the folder tree (GET /api/projects/{id}/folders/tree). */
export interface FolderTreeNode {
  id: string
  parentId: string | null
  name: string
  path: string
  target: DataTarget
  children: FolderTreeNode[]
}

/** A folder with its counts (GET /api/folders/{id}). */
export interface FolderNode {
  id: string
  parentId: string | null
  name: string
  path: string
  target: DataTarget
  driveFolderId: string | null
  lastSyncedAt: string | null
  childCount: number
  fileCount: number
}

export interface FolderDetail {
  folder: FolderNode
  subfolders: FolderNode[]
  files: FolderFileSummary[]
}

export interface StartImportResult {
  importBatchId: string
  totalRows: number
}

export interface RunImportStepResult {
  importBatchId: string
  processed: number
  total: number
  done: boolean
  batch: ImportBatchSummary
}

export interface FolderFileSummary {
  id: string
  name: string
  kind: FileKind
  source: FileSource
  state: ImportState
  sizeBytes: number
  driveModifiedAt: string | null
  driveFileId: string | null
  md5: string | null
}

export interface ImportBatchSummary {
  id: string
  projectId: string
  kind: string
  target: DataTarget
  folderFileId: string | null
  fileName: string
  importedAt: string
  importedBy: string
  rowsRead: number
  rowsInserted: number
  rowsUpdated: number
  rowsSkipped: number
  completed: boolean
  log: string | null
}

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}
