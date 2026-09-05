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

export interface FolderNode {
  id: string
  parentId: string | null
  name: string
  path: string
  target: DataTarget
  children: FolderNode[]
}

export interface FolderFileSummary {
  id: string
  name: string
  kind: FileKind
  state: ImportState
  sizeBytes: number
  lastImportBatchId: string | null
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
