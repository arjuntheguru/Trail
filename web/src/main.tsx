import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from '@tanstack/react-router'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'

import { ApiError } from '@/api/problem'
import { auth } from '@/auth/instance'
import { AuthProvider } from '@/auth/context'
import { Toaster } from '@/components/ui/sonner'
import { OutboxProvider } from '@/offline/context'
import { createOutbox } from '@/offline/outbox'
import { startOfflineCache } from '@/offline/persistence'

import './index.css'
import { registerServiceWorker } from './pwa'
import { createAppRouter, watchSessionEnd } from './router'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      // Run the first fetch even when offline so cached data shows instead of staying paused; retries still
      // wait for the connection to return.
      networkMode: 'offlineFirst',
      // Do not hammer the API with retries for client errors (the auth layer handles 401 itself).
      retry: (count, error) =>
        !(error instanceof ApiError && error.status < 500 && error.status !== 0) && count < 2,
    },
  },
})

const outbox = createOutbox({ store: auth.store, client: auth.client, queryClient })
const context = { auth, queryClient }
const router = createAppRouter(context)
watchSessionEnd(router, context)
auth.startRefreshScheduler()
startOfflineCache({ store: auth.store, queryClient })
outbox.start()
registerServiceWorker()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <AuthProvider service={auth}>
        <OutboxProvider outbox={outbox}>
          <RouterProvider router={router} />
          <Toaster position="top-center" />
        </OutboxProvider>
      </AuthProvider>
    </QueryClientProvider>
  </StrictMode>,
)
