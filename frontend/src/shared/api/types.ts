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
  isCompany: boolean
  authorName: string | null
  fileCount: number
  /** Drive holds rows the Live layer has not taken yet, here or below. */
  hasNewerDraft: boolean
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
  isCompany: boolean
  authorId: string | null
  authorName: string | null
  childCount: number
  fileCount: number
  hasNewerDraft: boolean
}

export interface FolderDetail {
  folder: FolderNode
  subfolders: FolderNode[]
  files: FolderFileSummary[]
}

export interface FolderFileSummary {
  id: string
  name: string
  kind: FileKind
  contentSource: FileSource
  contentModifiedAt: string
  driveFileId: string | null
  driveModifiedAt: string | null
  sizeBytes: number
  state: ImportState
  importError: string | null
  lastImportedAt: string | null
  /** Draft or Live rows this file produced, whichever layer its folder targets. */
  rowCount: number | null
  hasNewerDraft: boolean
  effectiveLayer: DataTarget
}

/** 202 body of POST /api/folders/{id}/files — the import runs in the worker. */
export interface UploadResult {
  fileId: string
  name: string
  replaced: boolean
}

export interface DriveStatus {
  isRunning: boolean
  lastRunStartedAt: string | null
  lastRunFinishedAt: string | null
  lastRunError: string | null
  nextRunAt: string | null
  queuedImports: number
}

export interface TriggerSyncResult {
  queued: boolean
  alreadyRunning: boolean
}

export interface SetTargetResult {
  folderId: string
  target: DataTarget
  foldersUpdated: number
}

export interface ConvertedFile {
  fileId: string
  name: string
  added: number
  updated: number
  deleted: number
  conflicts: number
}

export interface ConvertToLiveResult {
  folderId: string
  filesConverted: number
  added: number
  updated: number
  deleted: number
  skipped: number
  conflicts: number
  perFile: ConvertedFile[]
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

// ---------------------------------------------------------------- reports

export type UnifiedStatus = 'Approved' | 'Rejected' | 'UnderReview' | 'Withdrawn'
export type BaselineActivityType = 'Submittal' | 'Approval'

export interface TrackerRow {
  documentId: string
  layer: DataTarget
  documentNumber: string
  type: string
  discipline: string
  title: string
  deliveryMilestone: string | null
  activityId: string | null
  packageName: string | null
  building: string
  level: string
  trade: string
  author: string | null
  submissionsCount: number | null
  revision: string | null
  aconexStatus: string | null
  status: UnifiedStatus | null
  submissionDate: string | null
  dateModified: string | null
  transmittal: string | null
  plannedStart: string | null
  plannedFinish: string | null
  actualStart: string | null
  actualFinish: string | null
}

export interface TrackerRevision {
  id: string
  revision: string
  aconexStatus: string
  status: UnifiedStatus | null
  reviewStatus: string | null
  dateModified: string
  transmittalIn: string | null
  fileType: string
  fileName: string
  isLatest: boolean
  isTerminated: boolean
}

export interface TrackerDocument {
  row: TrackerRow
  revisions: TrackerRevision[]
}

export interface BaselineActivityRow {
  id: string
  activityCode: string
  package: string
  type: BaselineActivityType
  originalDuration: number
  start: string
  finish: string
  documentCount: number
  used: boolean
}

/** The 17 lists of the Picklists workbook (refactor-plan § 7). */
export type PicklistField =
  | 'Project' | 'Originator' | 'Contract' | 'DocType' | 'Discipline' | 'Zone' | 'Building'
  | 'DrawingType' | 'Level' | 'AuthoringSoftware' | 'ExchangeFormat' | 'ScopeArea'
  | 'SuitabilityCode' | 'Scale' | 'Classification' | 'CorporateDiscipline' | 'Author'

export interface PicklistItem {
  id: string
  field: PicklistField
  code: string
  description: string
  sortOrder: number
  isDeleted: boolean
  deletedAt: string | null
}

export interface PicklistGroup {
  field: PicklistField
  items: PicklistItem[]
}

export interface StatusMappingRow {
  id: string
  aconexStatus: string
  status: UnifiedStatus
  isLegacy: boolean
  isDeleted: boolean
  deletedAt: string | null
}

/** The editable Live document row (PUT /api/documents/{id}). */
export interface DocumentRow {
  id: string
  projectId: string
  tidpId: string
  disciplineId: string
  folderFileId: string | null
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
  updatedAt: string
  updatedBy: string
  exchanges: DraftExchange[]
}

// ------------------------------------------------------------- workbook view

export type WorkbookColumnKind = 'text' | 'date' | 'int'

export interface WorkbookColumn {
  letter: string
  title: string
  key: string
  width: number
  editable: boolean
  kind: WorkbookColumnKind
}

export interface WorkbookHeaderCell {
  label: string
  value: string | null
}

export interface WorkbookRow {
  rowId: string
  rowNumber: number
  cells: (string | null)[]
  state: DraftRowState | null
}

export interface WorkbookSheet {
  name: string
  headerBlock: WorkbookHeaderCell[]
  columns: WorkbookColumn[]
  totalRows: number
  page: number
  pageSize: number
  rows: WorkbookRow[]
}

export interface Workbook {
  fileId: string
  fileName: string
  kind: FileKind
  layer: DataTarget
  contentSource: FileSource
  contentModifiedAt: string
  hasNewerDraft: boolean
  sheets: string[]
  sheet: WorkbookSheet
}
