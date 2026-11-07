import { evaluateAnswers } from './evaluate'
import type { ServerAnswers, TaskField } from './types'

export interface FormProblems {
  fields: Record<string, string>
  photos?: string
  note?: string
  general: string[]
}

export const NO_PROBLEMS: FormProblems = { fields: {}, general: [] }

/** Moves focus to the first invalid field (in template order), falling back to the photo section. */
export function focusFirstProblem(
  root: HTMLElement | null,
  fields: readonly TaskField[],
  problems: FormProblems,
) {
  if (!root) return
  const key = fields.find((f) => problems.fields[f.key])?.key
  const target = key
    ? root.querySelector<HTMLElement>(`[data-field-key="${CSS.escape(key)}"]`)
    : problems.photos
      ? root.querySelector<HTMLElement>('[data-section="photos"]')
      : problems.note
        ? root.querySelector<HTMLElement>('[data-section="note"]')
        : null
  if (!target) return
  const focusable = target.querySelector<HTMLElement>(
    'input:not([type=hidden]):not([disabled]), textarea:not([disabled]), select:not([disabled]), button:not([disabled]), canvas',
  )
  target.scrollIntoView?.({ block: 'center', behavior: 'smooth' })
  focusable?.focus({ preventScroll: true })
}

/** The first problem per field plus the missing task photo; null when the answers can be submitted. */
export function validateForSubmit(
  fields: readonly TaskField[],
  answers: ServerAnswers,
  taskPhoto: { required: boolean; count: number },
): FormProblems | null {
  const problems: FormProblems = { fields: {}, general: [] }
  for (const { fieldKey, message } of evaluateAnswers(fields, answers, true).problems) {
    problems.fields[fieldKey] ??= message
  }
  if (taskPhoto.required && taskPhoto.count === 0) problems.photos = 'Attach at least one photo.'
  return Object.keys(problems.fields).length > 0 || problems.photos ? problems : null
}
