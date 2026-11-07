import { ApiError, toApiError } from './problem'

interface ClientResult<T> {
  data?: T
  error?: unknown
  response: Response
}

/** Resolves with the response data or throws an ApiError built from the ProblemDetails body. */
export async function unwrap<T>(request: Promise<ClientResult<T>>): Promise<T> {
  let result: ClientResult<T>
  try {
    result = await request
  } catch (cause) {
    if (cause instanceof ApiError) throw cause
    throw new ApiError({ status: 0, detail: 'Cannot reach the server.' })
  }
  if (result.response.ok) return result.data as T
  throw toApiError(result.response.status, result.error)
}
