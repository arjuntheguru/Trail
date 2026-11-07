import { resolveApiBaseUrl } from '@/api/client'

import { createAuthService } from './auth-service'

/** The app-wide auth service, configured from the build environment. */
export const auth = createAuthService({
  baseUrl: resolveApiBaseUrl(import.meta.env.VITE_API_BASE_URL),
})
