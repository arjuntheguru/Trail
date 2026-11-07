import createClient, { type Middleware } from 'openapi-fetch'

import type { paths } from './schema'

export type ApiClient = ReturnType<typeof createClient<paths>>

export interface ApiClientOptions {
  /** Origin the API lives on, without the /api/v1 prefix (paths in the OpenAPI document include it). */
  baseUrl: string
  fetch?: (input: Request) => Promise<Response>
  middleware?: Middleware[]
}

/** Browser transport for the refresh token: the API sets it as an httpOnly cookie instead of the body. */
export const REFRESH_TRANSPORT_HEADER = 'X-Refresh-Token-Transport'

export function createApiClient({ baseUrl, fetch, middleware = [] }: ApiClientOptions): ApiClient {
  const client = createClient<paths>({
    baseUrl,
    fetch,
    credentials: 'include',
    headers: { [REFRESH_TRANSPORT_HEADER]: 'cookie' },
  })
  client.use(...middleware)
  return client
}

/**
 * The configured API origin. `VITE_API_BASE_URL` ends in `/api/v1` (default `/api/v1`, same-origin); the
 * generated paths already include that prefix, so only the part before it is used as the client base URL.
 */
export function resolveApiBaseUrl(configured: string | undefined): string {
  const value = (configured ?? '/api/v1').replace(/\/+$/, '')
  return value.replace(/\/api\/v1$/, '')
}
