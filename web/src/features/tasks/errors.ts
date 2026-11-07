import { ApiError, errorMessage } from '@/api/problem'

export interface MappedErrors {
  /** Messages for answers, by template field key. */
  fields: Record<string, string>
  photos?: string
  note?: string
  /** Everything else, shown at the top of the form. */
  general: string[]
}

const ANSWER_PREFIX = /^answers\./i

/**
 * Maps a 400 from save/entries/complete onto the form. The API keys answer problems as `answers.<fieldKey>`
 * (TaskRules.InvalidAnswers); `photoIds` and `note` are the command's own properties.
 */
export function mapServerErrors(error: unknown): MappedErrors {
  const mapped: MappedErrors = { fields: {}, general: [] }
  if (!(error instanceof ApiError) || Object.keys(error.errors).length === 0) {
    mapped.general.push(errorMessage(error))
    return mapped
  }
  for (const [key, messages] of Object.entries(error.errors)) {
    const text = messages.join(' ')
    if (ANSWER_PREFIX.test(key)) mapped.fields[key.replace(ANSWER_PREFIX, '')] = text
    else if (/^photoIds/i.test(key)) mapped.photos = text
    else if (/^note$/i.test(key)) mapped.note = text
    else mapped.general.push(text)
  }
  return mapped
}

/** Conflicts mean the task changed under the person (done by someone else, closed, not open yet). */
export const isConflict = (error: unknown) => error instanceof ApiError && error.status === 409

export const isOffline = (error: unknown) => error instanceof ApiError && error.status === 0
