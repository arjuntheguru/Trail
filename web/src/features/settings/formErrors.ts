import { ApiError, errorMessage } from '@/api/problem'

/**
 * Attaches a server failure to the form. 400 validation errors are keyed by the command's property names
 * (`Name`, `Address.Line1`), conflicts carry a stable `code`. Anything that cannot be tied to a field is
 * returned for a form-level banner.
 *
 * @param codeFields error code -> field, for failures that name no property (e.g. `tag.name_taken`)
 * @param aliases server property (any case) -> field, for nested properties such as `Address.Line1`
 */
export function applyServerError<TField extends string>(
  error: unknown,
  fields: readonly TField[],
  setError: (field: TField, error: { type: string; message: string }) => void,
  codeFields: Record<string, TField> = {},
  aliases: Record<string, TField> = {},
): string[] {
  if (!(error instanceof ApiError)) return [errorMessage(error)]

  const coded = error.code ? codeFields[error.code] : undefined
  if (coded) {
    setError(coded, { type: 'server', message: errorMessage(error) })
    return []
  }

  const lowered = Object.fromEntries(Object.entries(aliases).map(([k, v]) => [k.toLowerCase(), v]))
  const general: string[] = []
  for (const [key, messages] of Object.entries(error.errors)) {
    const field =
      lowered[key.toLowerCase()] ?? fields.find((f) => f.toLowerCase() === key.toLowerCase())
    for (const message of messages) {
      if (field) setError(field, { type: 'server', message })
      else general.push(message)
    }
  }
  if (Object.keys(error.errors).length === 0) general.push(errorMessage(error))
  return general
}
