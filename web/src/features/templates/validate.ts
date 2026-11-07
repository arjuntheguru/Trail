import { LIMITS, OPTION_TYPES, RANGE_TYPES, type TemplateField } from './types'

/**
 * Client-side mirror of `TemplateDefinition.Validate` (Trail.Domain/Tasks/TemplateDefinition.cs). The messages
 * are the server's own so a rejected save reads the same as a blocked one. The server stays authoritative.
 */

export interface FieldIssue {
  message: string
  /** Positions of the fields the issue is about; empty for problems with the list as a whole. */
  indexes: number[]
}

const KEY_PATTERN = /^[A-Za-z0-9_-]+$/
const blank = (value: string | null | undefined) =>
  value === null || value === undefined || value.trim() === ''

export function isValidKey(key: string) {
  return !blank(key) && key.length <= LIMITS.key && KEY_PATTERN.test(key)
}

export function validateFields(fields: readonly TemplateField[]): FieldIssue[] {
  const issues: FieldIssue[] = []
  const add = (message: string, ...indexes: number[]) => issues.push({ message, indexes })

  if (fields.length === 0) add('A template needs at least one field.')
  if (fields.length > LIMITS.maxFields)
    add(`A template can have at most ${LIMITS.maxFields} fields.`)
  if (!fields.some((f) => f.type !== 'Instruction'))
    add('A template needs at least one field that can be answered.')

  const positions = new Map<string, number[]>()
  fields.forEach((f, i) => positions.set(f.key, [...(positions.get(f.key) ?? []), i]))
  for (const [key, at] of positions) {
    if (at.length > 1) add(`Field key '${key}' is used more than once.`, ...at)
  }

  fields.forEach((field, i) => {
    const name = blank(field.label) ? field.key : field.label
    const options = field.options ?? []

    if (!isValidKey(field.key))
      add(`Field '${name}': key must be 1-${LIMITS.key} letters, digits, '_' or '-'.`, i)
    if (blank(field.label) || field.label.length > LIMITS.label)
      add(`Field '${field.key}': label is required (max ${LIMITS.label} characters).`, i)

    if (RANGE_TYPES.includes(field.type)) {
      if (field.min != null && field.max != null && field.min > field.max)
        add(`Field '${name}': minimum cannot be greater than maximum.`, i)
    } else if (field.min != null || field.max != null) {
      add(`Field '${name}': only number and temperature fields have a range.`, i)
    }

    if (OPTION_TYPES.includes(field.type)) {
      if (options.length < LIMITS.minOptions || options.length > LIMITS.maxOptions)
        add(
          `Field '${name}': give between ${LIMITS.minOptions} and ${LIMITS.maxOptions} options.`,
          i,
        )
      if (options.some((o) => blank(o.value) || blank(o.label)))
        add(`Field '${name}': every option needs a value and a label.`, i)
      if (new Set(options.map((o) => o.value)).size !== options.length)
        add(`Field '${name}': option values must be unique.`, i)
    } else if (options.length > 0) {
      add(`Field '${name}': only select fields have options.`, i)
    }

    if (field.expectedAnswer != null && field.type !== 'YesNo')
      add(`Field '${name}': only yes/no fields have an expected answer.`, i)
  })

  return issues
}
