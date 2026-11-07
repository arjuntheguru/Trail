import { ApiError, errorMessage } from '@/api/problem'

import type { DraftField } from './draft'

export interface MappedTemplateError {
  /** Messages for the header inputs. */
  name?: string
  description?: string
  /** Messages about one field, keyed by the draft field id. */
  byField: Record<string, string[]>
  /** Everything that cannot be tied to an input. */
  general: string[]
}

const FIELD_PREFIX = /^Field '(.*?)':\s*/
const INDEXED = /^fields\[(\d+)\]/i

/** A field problem without its "Field 'x': " lead, which the field's own card already says. */
export function bareMessage(message: string) {
  const bare = message.replace(FIELD_PREFIX, '')
  return bare.charAt(0).toUpperCase() + bare.slice(1)
}

/**
 * Ties a failed save to the form. The template validator reports problems with the field list under a single
 * `fields` key as sentences ("Field 'Fridge': give between 2 and 50 options."), so those are matched back to a
 * field by its label or key; FluentValidation's own `Fields[3].Label` keys name the position directly.
 */
export function mapTemplateError(
  error: unknown,
  fields: readonly DraftField[],
): MappedTemplateError {
  const result: MappedTemplateError = { byField: {}, general: [] }
  if (!(error instanceof ApiError)) {
    result.general.push(errorMessage(error))
    return result
  }

  if (error.code === 'template.name_taken') {
    result.name = errorMessage(error)
    return result
  }

  const attach = (field: DraftField | undefined, message: string) => {
    if (field) (result.byField[field.id] ??= []).push(message)
    else result.general.push(message)
  }

  for (const [key, messages] of Object.entries(error.errors)) {
    const lower = key.toLowerCase()
    for (const message of messages) {
      if (lower === 'name') result.name = message
      else if (lower === 'description') result.description = message
      else if (INDEXED.test(key)) attach(fields[Number(INDEXED.exec(key)![1])], message)
      else if (lower === 'fields') {
        const name = FIELD_PREFIX.exec(message)?.[1]
        const match = name
          ? (fields.find((f) => f.label.trim() === name) ?? fields.find((f) => f.key === name))
          : undefined
        attach(match, match ? bareMessage(message) : message)
      } else result.general.push(message)
    }
  }
  if (Object.keys(error.errors).length === 0) result.general.push(errorMessage(error))
  return result
}
