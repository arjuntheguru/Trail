/**
 * RFC 7807 ProblemDetails as the API sends it: `code` is a stable machine-readable extension
 * (e.g. `auth.invalid_credentials`) and `errors` maps property names to messages for validation failures.
 */
export class ApiError extends Error {
  readonly status: number
  readonly code: string | undefined
  readonly detail: string | undefined
  readonly errors: Record<string, string[]>

  constructor(init: {
    status: number
    code?: string
    title?: string
    detail?: string
    errors?: Record<string, string[]>
  }) {
    super(init.detail ?? init.title ?? `Request failed with status ${init.status}`)
    this.name = 'ApiError'
    this.status = init.status
    this.code = init.code
    this.detail = init.detail
    this.errors = init.errors ?? {}
  }

  /** True when the server could not be reached or answered with a 5xx. */
  get isTransient() {
    return this.status === 0 || this.status >= 500
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

/** Builds an ApiError from a parsed error body (which may be anything, or undefined for empty bodies). */
export function toApiError(status: number, body: unknown): ApiError {
  if (!isRecord(body)) return new ApiError({ status })
  const rawErrors = isRecord(body.errors) ? body.errors : {}
  const errors: Record<string, string[]> = {}
  for (const [key, value] of Object.entries(rawErrors)) {
    if (Array.isArray(value)) errors[key] = value.filter((v) => typeof v === 'string')
  }
  return new ApiError({
    status,
    code: typeof body.code === 'string' ? body.code : undefined,
    title: typeof body.title === 'string' ? body.title : undefined,
    detail: typeof body.detail === 'string' ? body.detail : undefined,
    errors,
  })
}

/** Friendly message for the error, falling back for network failures and unexpected statuses. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 0) return 'Cannot reach the server. Check your connection and try again.'
    if (error.status === 429) return 'Too many attempts. Please wait a moment and try again.'
    if (error.status >= 500) return 'Something went wrong on our side. Please try again.'
    return error.detail ?? error.message
  }
  return 'Something went wrong. Please try again.'
}

/** "FirstName" / "firstName" -> "firstName", used to match server property names to form fields. */
function camel(key: string) {
  return key.length === 0 ? key : key.charAt(0).toLowerCase() + key.slice(1)
}

/**
 * Applies the server's validation errors to a form through `setError`. Returns the messages that could not
 * be attached to a known field so the caller can show them as a form-level error.
 */
export function applyFieldErrors<TField extends string>(
  error: unknown,
  fields: readonly TField[],
  setError: (field: TField, error: { type: string; message: string }) => void,
): string[] {
  if (!(error instanceof ApiError)) return [errorMessage(error)]
  const unmatched: string[] = []
  for (const [key, messages] of Object.entries(error.errors)) {
    const field = fields.find((f) => f === camel(key))
    for (const message of messages) {
      if (field) setError(field, { type: 'server', message })
      else unmatched.push(message)
    }
  }
  if (Object.keys(error.errors).length === 0) unmatched.push(errorMessage(error))
  return unmatched
}
