import type { PicklistField } from '@/shared/api/types'

// The 17 lists of the Picklists workbook, in the order the sheet lays them out
// (refactor-plan § 7), with the wording the sheet itself uses.
export const picklistFields: PicklistField[] = [
  'Project', 'Originator', 'Contract', 'DocType', 'Discipline', 'Zone', 'Building',
  'DrawingType', 'Level', 'AuthoringSoftware', 'ExchangeFormat', 'ScopeArea',
  'SuitabilityCode', 'Scale', 'Classification', 'CorporateDiscipline', 'Author',
]

export const picklistLabels: Record<PicklistField, string> = {
  Project: 'Project',
  Originator: 'Originator',
  Contract: 'Contract',
  DocType: 'Document types',
  Discipline: 'Discipline',
  Zone: 'Area / Zone',
  Building: 'Venue / Building',
  DrawingType: 'Drawing type',
  Level: 'Level',
  AuthoringSoftware: 'Authoring software',
  ExchangeFormat: 'File / exchange format',
  ScopeArea: 'Scope area',
  SuitabilityCode: 'Suitability status',
  Scale: 'Scale',
  Classification: 'Classification',
  CorporateDiscipline: 'Corporate discipline',
  Author: 'Author',
}

/** Single-column lists in the workbook: the code is the value, there is no description. */
export const codeOnlyFields = new Set<PicklistField>([
  'AuthoringSoftware', 'ExchangeFormat', 'ScopeArea', 'Scale', 'CorporateDiscipline', 'Author',
])

export const STATUS_MAPPING_TAB = 'StatusMapping'
