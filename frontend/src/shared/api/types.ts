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

// ---------------------------------------------------------------- drafts

export type DraftRowState = 'New' | 'Modified' | 'Unchanged' | 'Deleted' | 'Conflict'

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
  totalPages: number
}

export interface DraftExchange {
  number: number
  stage: string | null
  programmeRef: string | null
  author: string | null
  geometrical: string | null
  nonGeometrical: string | null
  durationDays: number | null
  predecessor: string | null
  exchangeDate: string | null
}

export interface DraftDocument {
  id: string
  projectId: string
  tidpDraftId: string
  disciplineId: string
  folderFileId: string
  importBatchId: string
  documentNumber: string
  title: string
  extractedFromModel: string | null
  scopeArea: string | null
  authoringSoftware: string | null
  exchangeFormat: string | null
  scale: string | null
  deliveryMilestone: string | null
  packageName: string | null
  activityId: string | null
  classificationCode: string | null
  f01Project: string
  f02Originator: string
  f03Contract: string
  f04DocType: string
  f05Discipline: string
  f06Zone: string
  f07Building: string
  f08ADrawingType: string
  f08BLevel: string
  f08CSequence: string
  corporateDiscipline: string
  budgetWeight: number
  state: DraftRowState
  liveDocumentId: string | null
  conflictReason: string | null
  isDuplicate: boolean
  updatedAt: string
  updatedBy: string
  exchanges: DraftExchange[]
}

/** The editable half of a draft row — what the full-replace PUT accepts. */
export type DraftDocumentEdit = Pick<
  DraftDocument,
  | 'title' | 'extractedFromModel' | 'scopeArea' | 'authoringSoftware' | 'exchangeFormat'
  | 'scale' | 'deliveryMilestone' | 'packageName' | 'activityId' | 'classificationCode'
  | 'f01Project' | 'f02Originator' | 'f03Contract' | 'f04DocType' | 'f05Discipline'
  | 'f06Zone' | 'f07Building' | 'f08ADrawingType' | 'f08BLevel' | 'f08CSequence'
  | 'corporateDiscipline'
>

export interface DraftFieldChange {
  field: string
  oldValue: string | null
  newValue: string | null
}

export interface PromoteRow {
  documentNumber: string
  draftId: string | null
  liveId: string | null
  title: string | null
  changes: DraftFieldChange[]
  reason: string | null
}

export interface PromoteDiff {
  folderFileId: string
  importBatchId: string | null
  importedAt: string | null
  added: number
  modified: number
  unchanged: number
  deleted: number
  conflicts: number
  maxRows: number
  addedRows: PromoteRow[]
  modifiedRows: PromoteRow[]
  deletedRows: PromoteRow[]
  conflictRows: PromoteRow[]
}

export interface PromoteResult {
  promoteBatchId: string
  folderFileId: string
  added: number
  updated: number
  deleted: number
  skipped: number
  conflicts: number
  recalculationRequired: boolean
}

export interface RollbackResult {
  promoteBatchId: string
  removed: number
  restored: number
  reinserted: number
  recalculationRequired: boolean
}
