import 'fake-indexeddb/auto'

import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider, createMemoryHistory } from '@tanstack/react-router'
import { render } from '@testing-library/react'
import { onTestFinished } from 'vitest'

import { createAuthService, type AuthServiceOptions } from '@/auth/auth-service'
import { AuthProvider } from '@/auth/context'
import { OutboxProvider } from '@/offline/context'
import { createOutbox, type OutboxOptions } from '@/offline/outbox'
import { createIdbOutboxStorage } from '@/offline/outbox-storage'
import { createAppRouter, watchSessionEnd } from '@/router'

import { API } from './server'

export function createTestAuth(options: Partial<AuthServiceOptions> = {}) {
  return createAuthService({ baseUrl: API, ...options })
}

/** Renders the real route tree at `path` against the MSW server. */
export async function renderApp(
  path: string,
  auth = createTestAuth(),
  outboxOptions: Partial<OutboxOptions> = {},
) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const context = { auth, queryClient }
  const router = createAppRouter(context, createMemoryHistory({ initialEntries: [path] }))
  watchSessionEnd(router, context)
  // A database of its own per render, so queued changes never leak from one test into the next.
  const outbox = createOutbox({
    store: auth.store,
    client: auth.client,
    queryClient,
    storage: createIdbOutboxStorage(`trail-outbox-test-${crypto.randomUUID()}`),
    ...outboxOptions,
  })
  onTestFinished(outbox.start())
  await router.load()
  render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider service={auth}>
        <OutboxProvider outbox={outbox}>
          <RouterProvider router={router} />
        </OutboxProvider>
      </AuthProvider>
    </QueryClientProvider>,
  )
  return { auth, router, queryClient, outbox }
}
