import type { components } from '@/api/schema'

type Schemas = components['schemas']

export type Template = Schemas['TemplateResponse']
export type TemplateSummary = Schemas['TemplateSummaryResponse']
export type LibrarySummary = Schemas['LibraryTemplateSummaryResponse']
export type LibraryTemplate = Schemas['LibraryTemplateResponse']
export type TemplateBody = Schemas['CreateTemplateCommand']
export type TaskKind = Schemas['TaskKind']
export type TemplateField = Schemas['TemplateField']
export type FieldType = Schemas['FieldType']
export type FieldOption = Schemas['FieldOption']

export const KIND_LABELS: Record<TaskKind, string> = {
  Checklist: 'Checklist',
  RecordLog: 'Record log',
  Form: 'Form',
}

export const KIND_HINTS: Record<TaskKind, string> = {
  Checklist: 'Completed once per occurrence, for example an opening checklist.',
  RecordLog: 'Readings repeated across the day, for example a fridge temperature log.',
  Form: 'Filled in when something happens, for example an incident report.',
}

export const KINDS: readonly TaskKind[] = ['Checklist', 'RecordLog', 'Form']

export const FIELD_TYPE_LABELS: Record<FieldType, string> = {
  Checkbox: 'Checkbox',
  YesNo: 'Yes / No',
  Number: 'Number',
  Temperature: 'Temperature',
  Text: 'Short text',
  LongText: 'Long text',
  SingleSelect: 'Single choice',
  MultiSelect: 'Multiple choice',
  Date: 'Date',
  Time: 'Time',
  Photo: 'Photo',
  Signature: 'Signature',
  Instruction: 'Instruction',
}

export const FIELD_TYPES = Object.keys(FIELD_TYPE_LABELS) as FieldType[]

/** The mirror of TemplateDefinition / TemplateField limits in Trail.Domain. */
export const LIMITS = {
  name: 200,
  description: 2000,
  key: 40,
  label: 300,
  maxFields: 200,
  minOptions: 2,
  maxOptions: 50,
} as const

export const RANGE_TYPES: readonly FieldType[] = ['Number', 'Temperature']
export const OPTION_TYPES: readonly FieldType[] = ['SingleSelect', 'MultiSelect']
/** Types whose answer can fail, and so can ask for evidence and raise an issue. */
export const FAILABLE_TYPES: readonly FieldType[] = [
  'YesNo',
  'Number',
  'Temperature',
  'SingleSelect',
  'MultiSelect',
]
