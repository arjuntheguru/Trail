import type { components } from '@/api/schema'

type Schemas = components['schemas']

export type TaskDetail = Schemas['TaskDetailResponse']
export type TaskField = Schemas['TemplateField']
export type FieldType = Schemas['FieldType']
export type FieldOption = Schemas['FieldOption']
export type TaskEntry = Schemas['TaskEntryResponse']
export type FieldFailure = Schemas['FieldFailure']

/** An answer as the API exchanges it. `value` is raw JSON whose shape depends on the field type. */
export type ServerAnswer = Schemas['FieldAnswer']
export type ServerAnswers = Record<string, ServerAnswer>

/** An answer as the form holds it: inputs are controlled, so every field has a value of its input's type. */
export interface FormAnswer {
  /** boolean | string | string[] | null depending on the field type (numbers stay text while typing). */
  value: unknown
  photoIds: string[]
  comment: string
}

export interface TaskFormValues {
  answers: Record<string, FormAnswer>
  note: string
  /** Task-level photos, required when the task has `requiresPhoto`. */
  photoIds: string[]
}

export type TaskStatus = NonNullable<Schemas['SiteTaskStatus']>
export type TaskListItem = Schemas['TaskListItemResponse']
