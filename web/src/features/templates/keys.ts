import { LIMITS } from './types'

/** A stable identifier from free text: lower-case letters and digits joined by underscores. */
export function slugify(text: string, fallback = 'field'): string {
  const slug = text
    .normalize('NFKD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .slice(0, LIMITS.key)
    .replace(/_+$/, '')
  return slug || fallback
}

/** `base`, or `base_2`, `base_3`... (kept within the key length) until it is not in `taken`. */
export function uniqueKey(base: string, taken: Iterable<string>): string {
  const used = new Set(taken)
  if (!used.has(base)) return base
  for (let n = 2; ; n++) {
    const suffix = `_${n}`
    const candidate = `${base.slice(0, LIMITS.key - suffix.length)}${suffix}`
    if (!used.has(candidate)) return candidate
  }
}
