import type { TemplateField } from './types'

/** The fields that can be previewed: a usable, first-seen key is all the sandbox form needs. */
export function previewable(fields: readonly TemplateField[]): TemplateField[] {
  const seen = new Set<string>()
  return fields
    .filter((f) => {
      if (f.key.trim() === '' || seen.has(f.key)) return false
      seen.add(f.key)
      return true
    })
    .map((f) => (f.label.trim() === '' ? { ...f, label: 'Untitled field' } : f))
}
