import type { FieldFailure, ServerAnswer, ServerAnswers, TaskField } from './types'

/**
 * Client-side mirror of the domain's `AnswerEvaluator` (Trail.Domain/Tasks/Answers.cs) so people get immediate
 * feedback. The server stays authoritative: it re-evaluates everything on save and completion.
 */

export const LIMITS = { text: 500, longText: 5000, comment: 2000, photosPerAnswer: 10 } as const

export interface Problem {
  fieldKey: string
  message: string
}

export interface Evaluation {
  problems: Problem[]
  failures: FieldFailure[]
  isValid: boolean
}

export const isAnswerable = (field: TaskField) => field.type !== 'Instruction'

/** Fields are required unless the template says otherwise (the API omits the default). */
export const isRequired = (field: TaskField) => field.required !== false

export function unitOf(field: TaskField): string {
  return field.unit ?? (field.type === 'Temperature' ? '°C' : '')
}

/** Accepts numbers and typed text ("4.5", "4,5", "-3"); null when it is not a finite decimal. */
export function parseDecimal(raw: unknown): number | null {
  if (typeof raw === 'number') return Number.isFinite(raw) ? raw : null
  if (typeof raw !== 'string') return null
  const text = raw.trim().replace(',', '.')
  if (!/^[+-]?(\d+\.?\d*|\.\d+)$/.test(text)) return null
  const value = Number(text)
  return Number.isFinite(value) ? value : null
}

const isNullish = (value: unknown) => value === null || value === undefined

const photoCount = (answer: ServerAnswer) => answer.photoIds?.length ?? 0

export function isAnswered(field: TaskField, answer: ServerAnswer | undefined): boolean {
  if (!answer) return false
  switch (field.type) {
    case 'Photo':
      return photoCount(answer) > 0
    case 'Checkbox':
      return answer.value === true
    case 'MultiSelect':
      return Array.isArray(answer.value) && answer.value.length > 0
    case 'Text':
    case 'LongText':
      return typeof answer.value === 'string' && answer.value.trim() !== ''
    default:
      return !isNullish(answer.value)
  }
}

function formatNumber(value: number, field: TaskField) {
  const unit = unitOf(field)
  return unit ? `${value} ${unit}` : String(value)
}

/** "0–5 °C", "at least 0 °C", "at most 5 °C" or null when the field has no range. */
export function describeRange(field: TaskField): string | null {
  const unit = unitOf(field)
  const withUnit = (n: number) => (unit ? `${n} ${unit}` : String(n))
  const { min, max } = field
  if (!isNullish(min) && !isNullish(max)) return `${min}–${max}${unit ? ` ${unit}` : ''}`
  if (!isNullish(min)) return `at least ${withUnit(min!)}`
  if (!isNullish(max)) return `at most ${withUnit(max!)}`
  return null
}

/** The rule shown under an input so people know what passes before they answer. */
export function ruleHint(field: TaskField): string | null {
  switch (field.type) {
    case 'Number':
    case 'Temperature': {
      const range = describeRange(field)
      return range ? `Expected ${range}` : null
    }
    case 'YesNo':
      return isNullish(field.expectedAnswer)
        ? null
        : `Expected answer: ${field.expectedAnswer ? 'Yes' : 'No'}`
    default:
      return null
  }
}

function outOfRange(field: TaskField, value: number): string | null {
  const below = !isNullish(field.min) && value < field.min!
  const above = !isNullish(field.max) && value > field.max!
  if (!below && !above) return null
  return `Out of range: ${formatNumber(value, field)}, expected ${describeRange(field)}`
}

function isValidDate(value: string) {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  if (!match) return false
  const [y, m, d] = [Number(match[1]), Number(match[2]), Number(match[3])]
  const date = new Date(Date.UTC(y, m - 1, d))
  return date.getUTCFullYear() === y && date.getUTCMonth() === m - 1 && date.getUTCDate() === d
}

/** An error when the value has the wrong shape, otherwise the failure reason when the answer fails. */
function check(field: TaskField, answer: ServerAnswer): { error?: string; failure?: string } {
  const value = answer.value
  const options = field.options ?? []
  switch (field.type) {
    case 'Checkbox':
    case 'Photo':
      return {}

    case 'YesNo': {
      if (typeof value !== 'boolean') return { error: 'Answer yes or no.' }
      const expected = field.expectedAnswer
      if (!isNullish(expected) && expected !== value) {
        return {
          failure: `Answered ${value ? 'yes' : 'no'}, expected ${expected ? 'yes' : 'no'}`,
        }
      }
      return {}
    }

    case 'Number':
    case 'Temperature': {
      const number = parseDecimal(value)
      if (number === null) return { error: 'Enter a number.' }
      const failure = outOfRange(field, number)
      return failure ? { failure } : {}
    }

    case 'Text':
    case 'LongText': {
      if (typeof value !== 'string') return { error: 'Enter text.' }
      const limit = field.type === 'Text' ? LIMITS.text : LIMITS.longText
      return value.length > limit ? { error: `Text is limited to ${limit} characters.` } : {}
    }

    case 'SingleSelect': {
      if (typeof value !== 'string') return { error: 'Choose an option.' }
      const chosen = options.find((o) => o.value === value)
      if (!chosen) return { error: 'Choose one of the listed options.' }
      return chosen.isFailure ? { failure: `'${chosen.label}' was selected` } : {}
    }

    case 'MultiSelect': {
      if (!Array.isArray(value)) return { error: 'Choose one or more options.' }
      const picked = []
      for (const item of value) {
        const option = typeof item === 'string' ? options.find((o) => o.value === item) : undefined
        if (!option) return { error: 'Choose only the listed options.' }
        picked.push(option)
      }
      const failing = picked.filter((o) => o.isFailure).map((o) => `'${o.label}'`)
      return failing.length > 0 ? { failure: `${failing.join(', ')} selected` } : {}
    }

    case 'Date':
      return typeof value === 'string' && isValidDate(value) ? {} : { error: 'Enter a valid date.' }

    case 'Time':
      return typeof value === 'string' && /^([01]\d|2[0-3]):[0-5]\d$/.test(value)
        ? {}
        : { error: 'Enter a valid time.' }

    case 'Signature':
      return typeof value === 'string' && /^[0-9a-f-]{36}$/i.test(value)
        ? {}
        : { error: 'Add a signature.' }

    default:
      return { error: 'This field cannot be answered.' }
  }
}

/**
 * Checks answers against a task's fields.
 * @param requireAll true on completion/submission: required fields must be answered and failing answers must
 * carry the evidence the field asks for. While drafting, only the shape of what was entered is checked.
 */
export function evaluateAnswers(
  fields: readonly TaskField[],
  answers: ServerAnswers,
  requireAll: boolean,
): Evaluation {
  const problems: Problem[] = []
  const failures: FieldFailure[] = []
  const known = new Map(fields.map((f) => [f.key, f]))

  for (const key of Object.keys(answers)) {
    const field = known.get(key)
    if (!field || !isAnswerable(field)) {
      problems.push({ fieldKey: key, message: 'This field does not exist on the task.' })
    }
  }

  for (const field of fields.filter(isAnswerable)) {
    const answer = answers[field.key]
    if (!answer || !isAnswered(field, answer)) {
      if (requireAll && isRequired(field)) {
        problems.push({ fieldKey: field.key, message: `'${field.label}' is required.` })
      }
      continue
    }

    if ((answer.comment?.length ?? 0) > LIMITS.comment) {
      problems.push({
        fieldKey: field.key,
        message: `Comments are limited to ${LIMITS.comment} characters.`,
      })
    }
    if (photoCount(answer) > LIMITS.photosPerAnswer) {
      problems.push({
        fieldKey: field.key,
        message: `Attach at most ${LIMITS.photosPerAnswer} photos.`,
      })
    }

    const { error, failure } = check(field, answer)
    if (error) {
      problems.push({ fieldKey: field.key, message: error })
      continue
    }
    if (!failure) continue
    failures.push({ fieldKey: field.key, label: field.label, reason: failure })

    if (!requireAll) continue
    if (field.photoRequiredOnFailure && photoCount(answer) === 0) {
      problems.push({
        fieldKey: field.key,
        message: `'${field.label}' failed: add a photo as evidence.`,
      })
    }
    if (field.commentRequiredOnFailure !== false && !answer.comment?.trim()) {
      problems.push({
        fieldKey: field.key,
        message: `'${field.label}' failed: describe the corrective action taken.`,
      })
    }
  }

  return { problems, failures, isValid: problems.length === 0 }
}
