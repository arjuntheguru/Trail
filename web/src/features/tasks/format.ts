import { parseDecimal, unitOf } from './evaluate'
import type { ServerAnswer, TaskField } from './types'

/** A short text for a saved answer, or null when it is shown as photos / a signature image instead. */
export function formatAnswer(field: TaskField, answer: ServerAnswer | undefined): string | null {
  const value = answer?.value
  if (value === null || value === undefined) return field.type === 'Photo' ? null : '—'
  switch (field.type) {
    case 'Checkbox':
      return value === true ? 'Done' : 'Not done'
    case 'YesNo':
      return value === true ? 'Yes' : 'No'
    case 'Number':
    case 'Temperature': {
      const n = parseDecimal(value)
      const unit = unitOf(field)
      return n === null ? String(value) : unit ? `${n} ${unit}` : String(n)
    }
    case 'SingleSelect':
      return field.options?.find((o) => o.value === value)?.label ?? String(value)
    case 'MultiSelect':
      return Array.isArray(value)
        ? value.map((v) => field.options?.find((o) => o.value === v)?.label ?? String(v)).join(', ')
        : String(value)
    case 'Signature':
    case 'Photo':
      return null
    default:
      return String(value)
  }
}
