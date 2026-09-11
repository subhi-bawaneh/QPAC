import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/client'
import { QPAC_PROJECT_ID } from '@/shared/api/project'
import type { ImportBatchSummary, UnifiedStatus } from '@/shared/api/types'

export interface SummaryGroup {
  name: string
  total: number
  planned: number
  submitted: number
  approved: number
  qualityApproved: number
  rejected: number
  underReview: number
  withdrawn: number
  totalRevisions: number
  quality: number | null
  pvPending: number
  pvSub1: number
  pvSub2: number
  pvApproved: number
  plannedPercent: number
  evPending: number
  evSub1: number
  evSub2: number
  evApproved: number
  completedPercent: number
}

export interface SummaryWeek {
  number: number
  from: string
  to: string
  planned: number
  submitted: number
  approved: number
  cumulativePlanned: number
  cumulativeSubmitted: number
  cumulativeApproved: number
}

export interface CorporateSummary {
  reportDate: string
  currentWeek: string
  startWeek: string
  endWeek: string
  weeks: SummaryWeek[]
  disciplines: SummaryGroup[]
  authors: SummaryGroup[]
  total: SummaryGroup
}

export interface BaselineDisciplineRow {
  name: string
  total: number
  submitted: number
  approved: number
  cRevise: number
  dRejected: number
  underReview: number
}

export interface PackageStatusCount {
  status: 'Unused' | 'Pending' | 'Partial' | 'Submitted'
  packages: number
  drawings: number
}

export interface BaselineSummary {
  total: BaselineDisciplineRow
  disciplines: BaselineDisciplineRow[]
  packages: unknown[]
  packageStatuses: PackageStatusCount[]
  totalPackages: number
  totalPackageDrawings: number
}

export interface EvmRow {
  name: string
  documents: number
  plannedValue: number
  earnedValue: number
  budgetAtCompletion: number
  schedulePerformanceIndex: number | null
  scheduleVariance: number
  plannedPercent: number
  earnedPercent: number
  costPerformanceIndex: number | null
}

export interface EvmSummary {
  reportDate: string
  total: EvmRow
  disciplines: EvmRow[]
}

// ---------------------------------------------------------------- control findings

// What a finding points at, so a row can be opened. Two of the five have no document
// to open — a delivered-but-unplanned row IS an Aconex revision, an unused package IS a
// baseline activity — so the kind travels with the id.
export type FindingSource = 'Document' | 'AconexRevision' | 'BaselineActivity'

export interface DeliveredButUnplanned {
  sourceId: string
  sourceKind: FindingSource
  documentNumber: string
  revision: string
  title: string
  aconexStatus: string
  status: UnifiedStatus | null
  dateModified: string
}

export interface UnplannedDocument {
  sourceId: string
  sourceKind: FindingSource
  documentId: string
  type: string
  discipline: string
  documentNumber: string
  title: string
  plannedStart: string | null
  author: string | null
}

export interface UnusedPackage {
  sourceId: string
  sourceKind: FindingSource
  package: string
  activityCode: string
  originalDuration: number
  finish: string
  documentCount: number
}

export interface DuplicateDocument {
  sourceId: string
  sourceKind: FindingSource
  documentId: string
  type: string
  discipline: string
  documentNumber: string
  title: string
  plannedStart: string | null
  author: string | null
  count: number
}

/** A numbering field holding a value that is in no picklist: named field, named value. */
export interface OffListSegment {
  sourceId: string
  sourceKind: FindingSource
  documentId: string
  documentNumber: string
  field: string
  value: string
  discipline: string
  title: string
}

export interface ControlFindings {
  deliveredButUnplanned: DeliveredButUnplanned[]
  unplanned: UnplannedDocument[]
  unusedPackages: UnusedPackage[]
  duplicates: DuplicateDocument[]
  offList: OffListSegment[]
}

export function useControlFindings() {
  return useQuery({
    queryKey: ['findings', QPAC_PROJECT_ID],
    queryFn: async () => {
      const { data } = await api.get<{ findings: ControlFindings; recalculationRequired: boolean }>(
        `/api/projects/${QPAC_PROJECT_ID}/control-findings`,
      )
      return data
    },
  })
}

export function useRecentImports(take = 8) {
  return useQuery({
    queryKey: ['imports', QPAC_PROJECT_ID, take],
    queryFn: async () => {
      const { data } = await api.get<ImportBatchSummary[]>(
        `/api/projects/${QPAC_PROJECT_ID}/imports`,
        { params: { take } },
      )
      return data
    },
  })
}

interface Envelope<T> {
  summary: T
  recalculationRequired: boolean
  documentsWithoutSnapshot: number
}

export function useCorporateSummary(reportDate?: string) {
  return useQuery({
    queryKey: ['summary', 'corporate', QPAC_PROJECT_ID, reportDate ?? null],
    queryFn: async () => {
      const { data } = await api.get<Envelope<CorporateSummary>>(
        `/api/projects/${QPAC_PROJECT_ID}/summaries/corporate`,
        { params: { reportDate } },
      )
      return data
    },
  })
}

export function useBaselineSummary() {
  return useQuery({
    queryKey: ['summary', 'baseline', QPAC_PROJECT_ID],
    queryFn: async () => {
      const { data } = await api.get<Envelope<BaselineSummary>>(
        `/api/projects/${QPAC_PROJECT_ID}/summaries/baseline`,
      )
      return data
    },
  })
}

export function useEvmSummary(reportDate?: string) {
  return useQuery({
    queryKey: ['summary', 'evm', QPAC_PROJECT_ID, reportDate ?? null],
    queryFn: async () => {
      const { data } = await api.get<Envelope<EvmSummary>>(
        `/api/projects/${QPAC_PROJECT_ID}/summaries/evm`,
        { params: { reportDate } },
      )
      return data
    },
  })
}

/**
 * Rebuilds the snapshots the reports read, one chunk at a time.
 *
 * The worker normally recalculates after every import; this is the admin fallback,
 * chunked so a step never outlives a request, with the caller driving the loop.
 */
export function useRecalculate() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async () => {
      let offset = 0
      for (let step = 0; step < 500; step++) {
        const { data } = await api.post<{ nextOffset: number; done: boolean; total: number }>(
          `/api/projects/${QPAC_PROJECT_ID}/recalculate/step`,
          { offset, take: 500 },
        )
        offset = data.nextOffset
        if (data.done) return data
      }
      throw new Error('The recalculation did not finish after 500 steps')
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['summary'] })
      void queryClient.invalidateQueries({ queryKey: ['tracker'] })
      void queryClient.invalidateQueries({ queryKey: ['findings'] })
    },
  })
}
