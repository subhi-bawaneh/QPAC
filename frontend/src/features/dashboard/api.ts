import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/client'
import { QPAC_PROJECT_ID } from '@/shared/api/project'

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
 * The API is chunked because shared hosting has no background workers, so the
 * caller drives the loop and the offset comes back in each response.
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
