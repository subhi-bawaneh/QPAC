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

export interface ImportBatchSummary {
  id: string
  projectId: string
  kind: string
  tidpFileId: string | null
  fileName: string
  importedAt: string
  uploadedBy: string
  rowsRead: number
  rowsInserted: number
  rowsUpdated: number
  rowsSkipped: number
  // Lines an Aconex append dropped because it already held them. The figure that
  // tells an operator a re-upload of an overlapping export did nothing.
  rowsDuplicate: number
  status: 'Queued' | 'Running' | 'Completed' | 'Failed'
  log: string | null
}

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
  totalPages: number
}

// ---------------------------------------------------------------- reports

export type UnifiedStatus = 'Approved' | 'Rejected' | 'UnderReview' | 'Withdrawn'
export type BaselineActivityType = 'Submittal' | 'Approval'

export interface TrackerRow {
  documentId: string
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
  isEdited: boolean
  editedBy: string | null
  editedAt: string | null
  exchanges: DocumentExchange[]
}

export interface DocumentExchange {
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
  // "Edited" marks a row a person changed, so typed data is never mistaken for
  // imported data.
  state: string | null
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
  discipline: string
  uploadedAt: string
  sheets: string[]
  sheet: WorkbookSheet
}

// One uploaded TIDP workbook. EditedCount is what the replace dialog shows before it
// destroys the file's rows, so it is a real number rather than a warning string.
export interface TidpFile {
  id: string
  projectId: string
  disciplineId: string
  disciplineCode: string
  disciplineName: string
  fileName: string
  documentReference: string
  revisionNumber: string
  rowsRead: number
  rowsImported: number
  rowsSkipped: number
  documentCount: number
  editedCount: number
  uploadedBy: string
  uploadedAt: string
  status: 'Importing' | 'Imported' | 'Failed'
  error: string | null
}
