import { redirect } from '@tanstack/react-router'

import type { AuthService } from './auth-service'

/** Only same-origin absolute paths are honoured, so a crafted link cannot bounce users to another site. */
export function safeRedirect(target: string | undefined, fallback = '/today'): string {
  if (!target || !target.startsWith('/') || target.startsWith('//') || target.startsWith('/\\')) {
    return fallback
  }
  return target
}

type GuardAuth = Pick<AuthService, 'initialize' | 'store'>

/**
 * Route guard for signed-in pages. Restores the session from the refresh cookie on first use and redirects to
 * /login (remembering where the user was heading) when there is none.
 */
export async function requireAuthenticated(auth: GuardAuth, from: string) {
  await auth.initialize()
  const state = auth.store.getState()
  if (state.status !== 'authenticated') {
    throw redirect({ to: '/login', search: { redirect: from } })
  }
  return state.session
}

/** Keeps signed-in users away from the sign-in and sign-up pages. */
export async function redirectIfAuthenticated(auth: GuardAuth) {
  await auth.initialize()
  if (auth.store.getState().status === 'authenticated') throw redirect({ to: '/today' })
}
