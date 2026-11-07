import { parseDecimal } from '@/features/tasks/evaluate'

import { slugify, uniqueKey } from './keys'
import {
  FAILABLE_TYPES,
  LIMITS,
  OPTION_TYPES,
  RANGE_TYPES,
  type FieldType,
  type TemplateField,
} from './types'

/**
 * What the field builder edits: every input is text or a plain flag, ids are client-only (React keys, error
 * lookup) and `keyTouched` records whether the key still follows the label.
 */
export interface DraftOption {
  id: string
  value: string
  label: string
  isFailure: boolean
  /** True once the value has been typed by hand (or came from the server); it then stops following the label. */
  valueTouched: boolean
}

export type ExpectedAnswer = 'any' | 'yes' | 'no'

export interface DraftField {
  id: string
  key: string
  keyTouched: boolean
  label: string
  type: FieldType
  required: boolean
  helpText: string
  section: string
  min: string
  max: string
  unit: string
  expectedAnswer: ExpectedAnswer
  options: DraftOption[]
  photoRequiredOnFailure: boolean
  commentRequiredOnFailure: boolean
  raiseIssueOnFailure: boolean
}

const newId = () => crypto.randomUUID()

export function newOption(label = '', isFailure = false): DraftOption {
  return { id: newId(), value: '', label, isFailure, valueTouched: false }
}

/** A blank field of `type` with the same defaults the API applies. */
export function newField(type: FieldType, existing: readonly DraftField[]): DraftField {
  return {
    id: newId(),
    key: uniqueKey(
      'field',
      existing.map((f) => f.key),
    ),
    keyTouched: false,
    label: '',
    type,
    required: type !== 'Instruction',
    helpText: '',
    section: '',
    min: '',
    max: '',
    unit: '',
    expectedAnswer: 'any',
    options: OPTION_TYPES.includes(type) ? [newOption(), newOption()] : [],
    photoRequiredOnFailure: false,
    commentRequiredOnFailure: true,
    raiseIssueOnFailure: true,
  }
}

const text = (value: number | null | undefined) =>
  value === null || value === undefined ? '' : String(value)

/** Existing keys and option values are identifiers other records may refer to, so they never follow edits. */
export function toDraftField(field: TemplateField): DraftField {
  return {
    id: newId(),
    key: field.key,
    keyTouched: true,
    label: field.label,
    type: field.type,
    required: field.required ?? true,
    helpText: field.helpText ?? '',
    section: field.section ?? '',
    min: text(field.min),
    max: text(field.max),
    unit: field.unit ?? '',
    expectedAnswer:
      field.expectedAnswer === true ? 'yes' : field.expectedAnswer === false ? 'no' : 'any',
    options: (field.options ?? []).map((o) => ({
      id: newId(),
      value: o.value,
      label: o.label,
      isFailure: o.isFailure ?? false,
      valueTouched: true,
    })),
    photoRequiredOnFailure: field.photoRequiredOnFailure ?? false,
    commentRequiredOnFailure: field.commentRequiredOnFailure ?? true,
    raiseIssueOnFailure: field.raiseIssueOnFailure ?? true,
  }
}

const orNull = (value: string) => (value.trim() === '' ? null : value.trim())

/** The wire field. Settings that do not apply to the type are dropped so a type change cannot leave stale rules. */
export function toTemplateField(draft: DraftField): TemplateField {
  const ranged = RANGE_TYPES.includes(draft.type)
  const failable = FAILABLE_TYPES.includes(draft.type)
  return {
    key: draft.key,
    label: draft.label.trim(),
    type: draft.type,
    required: draft.type === 'Instruction' ? false : draft.required,
    helpText: orNull(draft.helpText),
    section: orNull(draft.section),
    min: ranged ? parseDecimal(draft.min) : null,
    max: ranged ? parseDecimal(draft.max) : null,
    unit: ranged ? orNull(draft.unit) : null,
    expectedAnswer:
      draft.type === 'YesNo' && draft.expectedAnswer !== 'any'
        ? draft.expectedAnswer === 'yes'
        : null,
    options: OPTION_TYPES.includes(draft.type)
      ? draft.options.map((o) => ({
          value: o.value.trim(),
          label: o.label.trim(),
          isFailure: o.isFailure,
        }))
      : [],
    photoRequiredOnFailure: failable && draft.photoRequiredOnFailure,
    commentRequiredOnFailure: failable ? draft.commentRequiredOnFailure : true,
    raiseIssueOnFailure: failable ? draft.raiseIssueOnFailure : true,
  }
}

/**
 * Problems only the text inputs can have: a range bound that is not a number never reaches the wire format, so
 * `validateFields` cannot see it.
 */
export function draftIssues(fields: readonly DraftField[]): { index: number; message: string }[] {
  const issues: { index: number; message: string }[] = []
  fields.forEach((f, index) => {
    if (!RANGE_TYPES.includes(f.type)) return
    if (f.min.trim() !== '' && parseDecimal(f.min) === null)
      issues.push({ index, message: 'Minimum must be a number.' })
    if (f.max.trim() !== '' && parseDecimal(f.max) === null)
      issues.push({ index, message: 'Maximum must be a number.' })
  })
  return issues
}

// --- list operations (pure) ---

export function addField(fields: readonly DraftField[], type: FieldType): DraftField[] {
  if (fields.length >= LIMITS.maxFields) return [...fields]
  return [...fields, newField(type, fields)]
}

export function removeField(fields: readonly DraftField[], id: string): DraftField[] {
  return fields.filter((f) => f.id !== id)
}

/** Moves a field `delta` places (clamped to the list); returns the same order when it cannot move. */
export function moveField(fields: readonly DraftField[], id: string, delta: number): DraftField[] {
  const from = fields.findIndex((f) => f.id === id)
  const to = Math.min(Math.max(from + delta, 0), fields.length - 1)
  if (from < 0 || to === from) return [...fields]
  const next = [...fields]
  const [moved] = next.splice(from, 1)
  next.splice(to, 0, moved!)
  return next
}

/** Applies a change to one field. While the key has not been touched it follows the label, kept unique. */
export function updateField(
  fields: readonly DraftField[],
  id: string,
  patch: Partial<Omit<DraftField, 'id'>>,
): DraftField[] {
  return fields.map((f) => {
    if (f.id !== id) return f
    const next = { ...f, ...patch }
    if ('key' in patch) next.keyTouched = true
    else if ('label' in patch && !next.keyTouched) {
      const others = fields.filter((o) => o.id !== id).map((o) => o.key)
      next.key = uniqueKey(slugify(next.label), others)
    }
    if (OPTION_TYPES.includes(next.type) && next.options.length === 0)
      next.options = [newOption(), newOption()]
    return next
  })
}

/** Applies a change to one option; an untouched value follows the label, unique within the field. */
export function updateOption(
  field: DraftField,
  optionId: string,
  patch: Partial<Omit<DraftOption, 'id'>>,
): DraftField {
  return {
    ...field,
    options: field.options.map((o) => {
      if (o.id !== optionId) return o
      const next = { ...o, ...patch }
      if ('value' in patch) next.valueTouched = true
      else if ('label' in patch && !next.valueTouched) {
        const others = field.options.filter((x) => x.id !== optionId).map((x) => x.value)
        next.value =
          next.label.trim() === '' ? '' : uniqueKey(slugify(next.label, 'option'), others)
      }
      return next
    }),
  }
}

export function addOption(field: DraftField): DraftField {
  if (field.options.length >= LIMITS.maxOptions) return field
  return { ...field, options: [...field.options, newOption()] }
}

export function removeOption(field: DraftField, optionId: string): DraftField {
  return { ...field, options: field.options.filter((o) => o.id !== optionId) }
}

export function moveOption(field: DraftField, optionId: string, delta: number): DraftField {
  const from = field.options.findIndex((o) => o.id === optionId)
  const to = Math.min(Math.max(from + delta, 0), field.options.length - 1)
  if (from < 0 || to === from) return field
  const options = [...field.options]
  const [moved] = options.splice(from, 1)
  options.splice(to, 0, moved!)
  return { ...field, options }
}

/** A comparable form of the builder state (client ids left out) for unsaved-change detection. */
export function snapshotOf(fields: readonly DraftField[]) {
  return JSON.stringify(fields, (key, value: unknown) => (key === 'id' ? undefined : value))
}
