import { describe, expect, it } from 'vitest'
import { toEdit } from '../draftEdit'
import type { DraftDocument } from '@/shared/api/types'

const row: DraftDocument = {
  id: 'draft-1',
  projectId: 'p',
  tidpDraftId: 't',
  disciplineId: 'd',
  folderFileId: 'f',
  importBatchId: 'b',
  documentNumber: 'QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004',
  title: 'BLADE 4 - Welding Details',
  extractedFromModel: null,
  scopeArea: 'ZONE-A',
  authoringSoftware: 'Tekla',
  exchangeFormat: '.dwg,.pdf',
  scale: '1:100',
  deliveryMilestone: '2025-12-19T00:00:00',
  packageName: null,
  activityId: 'QP.E.ST.GEN.GEN.1000',
  classificationCode: 'FI_60_25',
  f01Project: 'QF01012',
  f02Originator: 'NES',
  f03Contract: 'C04518',
  f04DocType: 'SDW',
  f05Discipline: 'STL',
  f06Zone: '00',
  f07Building: 'Z00000',
  f08ADrawingType: '0',
  f08BLevel: 'ZZ',
  f08CSequence: '0004',
  corporateDiscipline: 'Structural',
  budgetWeight: 1,
  state: 'New',
  liveDocumentId: null,
  conflictReason: null,
  isDuplicate: false,
  updatedAt: '2026-09-05T00:00:00',
  updatedBy: 'admin',
  exchanges: [],
}

describe('toEdit', () => {
  // The endpoint is a full replace: a field missing from the payload is cleared,
  // so the editor must start from every editable value on the row.
  it('carries every field the update accepts', () => {
    expect(Object.keys(toEdit(row)).sort()).toEqual([
      'activityId', 'authoringSoftware', 'classificationCode', 'corporateDiscipline',
      'deliveryMilestone', 'exchangeFormat', 'extractedFromModel',
      'f01Project', 'f02Originator', 'f03Contract', 'f04DocType', 'f05Discipline',
      'f06Zone', 'f07Building', 'f08ADrawingType', 'f08BLevel', 'f08CSequence',
      'packageName', 'scale', 'scopeArea', 'title',
    ])
  })

  it('keeps nulls as nulls rather than turning them into empty strings', () => {
    const edit = toEdit(row)

    expect(edit.packageName).toBeNull()
    expect(edit.extractedFromModel).toBeNull()
  })

  it('does not carry the derived document number', () => {
    expect(toEdit(row)).not.toHaveProperty('documentNumber')
  })

  it('preserves the leading zeros of the numbering fields', () => {
    const edit = toEdit(row)

    expect(edit.f06Zone).toBe('00')
    expect(edit.f08CSequence).toBe('0004')
  })
})
