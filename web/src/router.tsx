import type { QueryClient } from '@tanstack/react-query'
import { createRouter, type RouterHistory } from '@tanstack/react-router'

import type { AuthService } from '@/auth/auth-service'

import { routeTree } from './routeTree.gen'

export interface RouterContext {
  auth: AuthService
  queryClient: QueryClient
}

export function createAppRouter(context: RouterContext, history?: RouterHistory) {
  return createRouter({
    routeTree,
    context,
    history,
    defaultPreload: 'intent',
    defaultPreloadStaleTime: 0,
  })
}

export type AppRouter = ReturnType<typeof createAppRouter>

declare module '@tanstack/react-router' {
  interface Register {
    router: AppRouter
  }
}

/**
 * When a signed-in session ends (refresh token rejected, or sign-out from elsewhere in the app), drop cached
 * data and send the user to /login, remembering the page they were on when the session expired.
 */
export function watchSessionEnd(router: AppRouter, { auth, queryClient }: RouterContext) {
  let previous = auth.store.getState().status
  return auth.store.subscribe(() => {
    const state = auth.store.getState()
    if (previous === 'authenticated' && state.status === 'anonymous') {
      queryClient.clear()
      const onProtectedPage = router.state.matches.some((m) =>
        m.routeId.startsWith('/_authenticated'),
      )
      if (onProtectedPage) {
        void router.navigate({
          to: '/login',
          search: state.reason === 'expired' ? { redirect: router.state.location.href } : {},
          replace: true,
        })
      }
    }
    previous = state.status
  })
}
