import { evaluateAnswers, isAnswerable, parseDecimal } from './evaluate'
import type { FormAnswer, ServerAnswer, ServerAnswers, TaskField, TaskFormValues } from './types'

/** The empty form state for a field: what a controlled input of its type holds when unanswered. */
export function emptyAnswer(field: TaskField): FormAnswer {
  let value: unknown = ''
  switch (field.type) {
    case 'Checkbox':
      value = false
      break
    case 'YesNo':
      value = null
      break
    case 'MultiSelect':
      value = []
      break
  }
  return { value, photoIds: [], comment: '' }
}

/** Form state from saved answers (a draft or a submitted entry); numbers become the text people edit. */
export function toFormAnswers(
  fields: readonly TaskField[],
  saved: ServerAnswers | undefined,
): Record<string, FormAnswer> {
  const result: Record<string, FormAnswer> = {}
  for (const field of fields.filter(isAnswerable)) {
    const base = emptyAnswer(field)
    const answer = saved?.[field.key]
    if (answer) {
      const { value } = answer
      if (value !== null && value !== undefined) {
        if (typeof value === 'number') base.value = String(value)
        else if (field.type === 'Checkbox') base.value = value === true
        else base.value = value
      }
      base.photoIds = [...(answer.photoIds ?? [])]
      base.comment = answer.comment ?? ''
    }
    result[field.key] = base
  }
  return result
}

export function emptyFormValues(fields: readonly TaskField[]): TaskFormValues {
  return { answers: toFormAnswers(fields, undefined), note: '', photoIds: [] }
}

/** The wire value for a form value; empty inputs are `undefined` (not answered). Bad numbers pass through as text. */
function toWireValue(field: TaskField, value: unknown): unknown {
  switch (field.type) {
    case 'Checkbox':
      return value === true ? true : undefined
    case 'YesNo':
      return typeof value === 'boolean' ? value : undefined
    case 'Number':
    case 'Temperature': {
      if (typeof value !== 'string') return undefined
      if (value.trim() === '') return undefined
      return parseDecimal(value) ?? value
    }
    case 'MultiSelect':
      return Array.isArray(value) && value.length > 0 ? value : undefined
    case 'Photo':
      return undefined
    default:
      return typeof value === 'string' && value.trim() !== '' ? value : undefined
  }
}

/**
 * The answers to send: unanswered fields are left out entirely. Values that cannot be parsed (a half-typed
 * number) are kept so completion checks can report them.
 */
export function toServerAnswers(
  fields: readonly TaskField[],
  form: Record<string, FormAnswer>,
): ServerAnswers {
  const result: ServerAnswers = {}
  for (const field of fields.filter(isAnswerable)) {
    const raw = form[field.key]
    if (!raw) continue
    const value = toWireValue(field, raw.value)
    const comment = raw.comment.trim()
    if (value === undefined && raw.photoIds.length === 0 && comment === '') continue
    const answer: ServerAnswer = { photoIds: [...raw.photoIds] }
    if (value !== undefined) answer.value = value
    if (comment !== '') answer.comment = comment
    result[field.key] = answer
  }
  return result
}

/** Like `toServerAnswers`, but values that do not pass the shape checks are dropped so a draft is never rejected. */
export function toDraftAnswers(
  fields: readonly TaskField[],
  form: Record<string, FormAnswer>,
): ServerAnswers {
  const answers = toServerAnswers(fields, form)
  for (const { fieldKey } of evaluateAnswers(fields, answers, false).problems) {
    const answer = answers[fieldKey]
    if (!answer) continue
    delete answer.value
    if ((answer.photoIds?.length ?? 0) === 0 && !answer.comment) delete answers[fieldKey]
  }
  return answers
}

/** Groups consecutive fields under their section heading, keeping the template's order. */
export function groupBySection(fields: readonly TaskField[]) {
  const groups: { section: string | null; fields: TaskField[] }[] = []
  for (const field of fields) {
    const section = field.section?.trim() || null
    const last = groups[groups.length - 1]
    if (last && last.section === section) last.fields.push(field)
    else groups.push({ section, fields: [field] })
  }
  return groups
}
