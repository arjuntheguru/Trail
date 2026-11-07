import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider, createMemoryHistory } from '@tanstack/react-router'
import { render } from '@testing-library/react'

import { createAuthService, type AuthServiceOptions } from '@/auth/auth-service'
import { AuthProvider } from '@/auth/context'
import { createAppRouter, watchSessionEnd } from '@/router'

import { API } from './server'

export function createTestAuth(options: Partial<AuthServiceOptions> = {}) {
  return createAuthService({ baseUrl: API, ...options })
}

/** Renders the real route tree at `path` against the MSW server. */
export async function renderApp(path: string, auth = createTestAuth()) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const context = { auth, queryClient }
  const router = createAppRouter(context, createMemoryHistory({ initialEntries: [path] }))
  watchSessionEnd(router, context)
  await router.load()
  render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider service={auth}>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  )
  return { auth, router, queryClient }
}
