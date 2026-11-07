import type { Middleware } from 'openapi-fetch'

import { createApiClient, type ApiClient } from '@/api/client'
import type { components } from '@/api/schema'
import { unwrap } from '@/api/unwrap'

import {
  createSessionStore,
  toSession,
  type AuthSessionResponse,
  type SessionStore,
} from './session-store'

type LoginBody = components['schemas']['LoginCommand']
type RegisterBody = components['schemas']['RegisterOrganizationCommand']
type AcceptInvitationBody = components['schemas']['AcceptInvitationCommand']
type ChangePasswordBody = components['schemas']['ChangePasswordCommand']

/**
 * ok: a new session is in the store. rejected: the server refused the refresh token (session is cleared).
 * unavailable: the server could not be reached or failed; the session is kept and the caller may retry.
 */
export type RefreshOutcome = 'ok' | 'rejected' | 'unavailable'

export interface AuthServiceOptions {
  /** API origin without the /api/v1 prefix; empty for same-origin. */
  baseUrl: string
  store?: SessionStore
  fetch?: (input: Request) => Promise<Response>
  now?: () => number
  /** Serializes refreshes across browser tabs. Defaults to the Web Locks API when available. */
  withLock?: <T>(task: () => Promise<T>) => Promise<T>
  /** How long before expiry the access token is refreshed. */
  refreshLeadMs?: number
}

const REFRESH_LOCK = 'trail-refresh'
const MIN_SCHEDULE_DELAY_MS = 5_000
const RETRY_AFTER_UNAVAILABLE_MS = 15_000

// These endpoints are anonymous: no bearer token is attached and a 401 from them is never retried.
const ANONYMOUS_PATHS = new Set([
  '/api/v1/auth/register',
  '/api/v1/auth/login',
  '/api/v1/auth/refresh',
  '/api/v1/auth/logout',
  '/api/v1/auth/confirm-email',
  '/api/v1/auth/forgot-password',
  '/api/v1/auth/reset-password',
  '/api/v1/auth/accept-invitation',
])

/** Cross-tab mutual exclusion: refresh tokens are single use, so two tabs must never refresh at once. */
const defaultWithLock = <T>(task: () => Promise<T>): Promise<T> => {
  const locks = typeof navigator === 'undefined' ? undefined : navigator.locks
  return locks ? locks.request(REFRESH_LOCK, task) : task()
}

export function createAuthService(options: AuthServiceOptions) {
  const store = options.store ?? createSessionStore()
  const now = options.now ?? Date.now
  const withLock = options.withLock ?? defaultWithLock
  const refreshLeadMs = options.refreshLeadMs ?? 60_000
  const doFetch = options.fetch ?? ((request: Request) => globalThis.fetch(request))

  // Requests in flight that carried a bearer token, kept so a 401 can be retried once.
  const sent = new Map<string, { token: string; clone: Request }>()

  let inflight: Promise<RefreshOutcome> | null = null
  let initialization: Promise<void> | null = null

  const middleware: Middleware = {
    async onRequest({ request, schemaPath, id }) {
      if (ANONYMOUS_PATHS.has(schemaPath)) return undefined
      let state = store.getState()
      if (state.status === 'authenticated' && state.session.accessTokenExpiresAt - now() < 5_000) {
        await refresh()
        state = store.getState()
      }
      if (state.status !== 'authenticated') return undefined
      request.headers.set('Authorization', `Bearer ${state.session.accessToken}`)
      sent.set(id, { token: state.session.accessToken, clone: request.clone() })
      return request
    },
    async onResponse({ id, response }) {
      const entry = sent.get(id)
      sent.delete(id)
      if (!entry || response.status !== 401) return undefined

      // Another request may already have refreshed while this one was in flight; then just retry.
      const current = store.getState()
      const alreadyRefreshed =
        current.status === 'authenticated' && current.session.accessToken !== entry.token
      const outcome = alreadyRefreshed ? 'ok' : await refresh()
      const after = store.getState()
      if (outcome !== 'ok' || after.status !== 'authenticated') return undefined

      const headers = new Headers(entry.clone.headers)
      headers.set('Authorization', `Bearer ${after.session.accessToken}`)
      // Retried once only: whatever this returns, including another 401, goes back to the caller.
      return doFetch(new Request(entry.clone, { headers }))
    },
    onError({ id }) {
      sent.delete(id)
      return undefined
    },
  }

  const client: ApiClient = createApiClient({
    baseUrl: options.baseUrl,
    fetch: doFetch,
    middleware: [middleware],
  })

  async function runRefresh(): Promise<RefreshOutcome> {
    try {
      const { data, response } = await client.POST('/api/v1/auth/refresh')
      if (response.ok && data) {
        store.setSession(toSession(data))
        return 'ok'
      }
      // 408/429 and 5xx are transient; anything else means the refresh token is no longer accepted.
      if (response.status >= 500 || response.status === 408 || response.status === 429) {
        return 'unavailable'
      }
      store.clear('expired')
      return 'rejected'
    } catch {
      return 'unavailable'
    }
  }

  /** Single flight: concurrent callers share one refresh in this tab, and tabs queue on a lock. */
  function refresh(): Promise<RefreshOutcome> {
    inflight ??= withLock(runRefresh).finally(() => {
      inflight = null
    })
    return inflight
  }

  /** Restores the session on app start from the refresh cookie. Safe to call repeatedly. */
  function initialize(): Promise<void> {
    initialization ??= (async () => {
      if (store.getState().status !== 'unknown') return
      const outcome = await refresh()
      if (outcome === 'unavailable' && store.getState().status === 'unknown') store.clear()
    })()
    return initialization
  }

  async function startSession(
    request: Promise<{ data?: AuthSessionResponse; error?: unknown; response: Response }>,
  ) {
    const data = await unwrap(request)
    store.setSession(toSession(data))
    return data
  }

  /** Keeps the access token fresh: a timer shortly before expiry, plus a check when a tab wakes up. */
  function startRefreshScheduler(): () => void {
    let timer: ReturnType<typeof setTimeout> | undefined
    let scheduledFor: string | undefined

    const run = async () => {
      timer = undefined
      scheduledFor = undefined
      const outcome = await refresh()
      if (outcome === 'unavailable' && store.getState().status === 'authenticated') {
        timer = setTimeout(() => void run(), RETRY_AFTER_UNAVAILABLE_MS)
        scheduledFor = 'retry'
      }
    }

    const schedule = () => {
      const state = store.getState()
      if (state.status !== 'authenticated') {
        clearTimeout(timer)
        timer = undefined
        scheduledFor = undefined
        return
      }
      const key = state.session.accessToken
      if (scheduledFor === key) return
      clearTimeout(timer)
      const delay = Math.max(
        state.session.accessTokenExpiresAt - refreshLeadMs - now(),
        MIN_SCHEDULE_DELAY_MS,
      )
      scheduledFor = key
      timer = setTimeout(() => void run(), delay)
    }

    const onVisible = () => {
      if (document.visibilityState !== 'visible') return
      const state = store.getState()
      if (
        state.status === 'authenticated' &&
        state.session.accessTokenExpiresAt - now() < refreshLeadMs
      ) {
        clearTimeout(timer)
        void run()
      }
    }

    const unsubscribe = store.subscribe(schedule)
    document.addEventListener('visibilitychange', onVisible)
    schedule()
    return () => {
      unsubscribe()
      document.removeEventListener('visibilitychange', onVisible)
      clearTimeout(timer)
    }
  }

  return {
    store,
    client,
    initialize,
    refresh,
    startRefreshScheduler,

    login: (body: LoginBody) => startSession(client.POST('/api/v1/auth/login', { body })),
    register: (body: RegisterBody) => startSession(client.POST('/api/v1/auth/register', { body })),
    acceptInvitation: (body: AcceptInvitationBody) =>
      startSession(client.POST('/api/v1/auth/accept-invitation', { body })),
    switchOrganization: (organizationId: string) =>
      startSession(client.POST('/api/v1/auth/switch-organization', { body: { organizationId } })),
    changePassword: (body: ChangePasswordBody) =>
      startSession(client.POST('/api/v1/me/change-password', { body })),

    /** Ends this session. The cookie is cleared by the API; local state is cleared even if that call fails. */
    async logout() {
      try {
        await client.POST('/api/v1/auth/logout')
      } catch {
        // The server is unreachable; the local session still ends.
      }
      store.clear('signed-out')
    },

    /** Revokes every session of the user (all devices), then ends this one. */
    async logoutEverywhere() {
      await unwrap(client.POST('/api/v1/me/sessions/revoke-all'))
      try {
        await client.POST('/api/v1/auth/logout')
      } catch {
        // Ignored: the refresh token is already revoked server side.
      }
      store.clear('signed-out')
    },
  }
}

export type AuthService = ReturnType<typeof createAuthService>
