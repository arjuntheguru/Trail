/** One field's change, ready to render as "field: old → new". */
export interface ChangeLine {
  field: string
  old: string | null
  next: string | null
}

/** Human text for a JSON value; null means "nothing" and is rendered as a dash by the caller. */
export function formatValue(value: unknown): string | null {
  if (value === null || value === undefined) return null
  if (typeof value === 'string') return value === '' ? '(empty)' : value
  if (typeof value === 'number' || typeof value === 'boolean') return String(value)
  return JSON.stringify(value)
}

/** "firstName" -> "First name"; leaves snake_case and dotted paths readable. */
export function fieldLabel(field: string): string {
  const spaced = field
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[_.]+/g, ' ')
    .trim()
    .toLowerCase()
  return spaced.charAt(0).toUpperCase() + spaced.slice(1)
}

export function changeLines(
  changes: Record<string, { old: unknown; new: unknown }> | undefined,
): ChangeLine[] {
  return Object.entries(changes ?? {})
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([field, change]) => ({
      field,
      old: formatValue(change?.old),
      next: formatValue(change?.new),
    }))
}
