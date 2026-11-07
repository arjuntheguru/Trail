import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from '@tanstack/react-router'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'

import { ApiError } from '@/api/problem'
import { auth } from '@/auth/instance'
import { AuthProvider } from '@/auth/context'
import { Toaster } from '@/components/ui/sonner'

import './index.css'
import { createAppRouter, watchSessionEnd } from './router'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      // Do not hammer the API with retries for client errors (the auth layer handles 401 itself).
      retry: (count, error) =>
        !(error instanceof ApiError && error.status < 500 && error.status !== 0) && count < 2,
    },
  },
})

const context = { auth, queryClient }
const router = createAppRouter(context)
watchSessionEnd(router, context)
auth.startRefreshScheduler()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <AuthProvider service={auth}>
        <RouterProvider router={router} />
        <Toaster position="top-center" />
      </AuthProvider>
    </QueryClientProvider>
  </StrictMode>,
)
