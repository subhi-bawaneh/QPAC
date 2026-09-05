import type { DraftDocument, DraftDocumentEdit } from '@/shared/api/types'

/**
 * Everything the update endpoint accepts, pulled off the row.
 *
 * The PUT is a full replace: a field missing from the payload is cleared, so an
 * edit always starts from every editable value the row currently holds.
 */
export function toEdit(row: DraftDocument): DraftDocumentEdit {
  return {
    title: row.title,
    extractedFromModel: row.extractedFromModel,
    scopeArea: row.scopeArea,
    authoringSoftware: row.authoringSoftware,
    exchangeFormat: row.exchangeFormat,
    scale: row.scale,
    deliveryMilestone: row.deliveryMilestone,
    packageName: row.packageName,
    activityId: row.activityId,
    classificationCode: row.classificationCode,
    f01Project: row.f01Project,
    f02Originator: row.f02Originator,
    f03Contract: row.f03Contract,
    f04DocType: row.f04DocType,
    f05Discipline: row.f05Discipline,
    f06Zone: row.f06Zone,
    f07Building: row.f07Building,
    f08ADrawingType: row.f08ADrawingType,
    f08BLevel: row.f08BLevel,
    f08CSequence: row.f08CSequence,
    corporateDiscipline: row.corporateDiscipline,
  }
}

/** Only the fields the user actually filled in travel — the API leaves the rest alone. */
export function buildBulkFields(values: Record<string, string>): Record<string, string> {
  return Object.fromEntries(Object.entries(values).filter(([, value]) => value.trim().length > 0))
}
