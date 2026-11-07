import type { components } from '@/api/schema'

export type AuthSessionResponse = components['schemas']['AuthSessionResponse']
export type UserSummary = components['schemas']['UserSummary']
export type OrganizationSummary = components['schemas']['OrganizationSummary']

/** The in-memory view of a signed-in session. The refresh token never reaches page scripts. */
export interface Session {
  accessToken: string
  /** Epoch milliseconds. */
  accessTokenExpiresAt: number
  user: UserSummary
  organization: OrganizationSummary | null
  organizations: OrganizationSummary[]
  permissions: string[]
}

export type SignedOutReason = 'signed-out' | 'expired'

export type AuthState =
  | { status: 'unknown'; session: null; reason: null }
  | { status: 'authenticated'; session: Session; reason: null }
  | { status: 'anonymous'; session: null; reason: SignedOutReason | null }

/** The API serializes UTC instants; treat a value without an offset as UTC rather than local time. */
export function parseInstant(value: string): number {
  const hasZone = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(value)
  return Date.parse(hasZone ? value : `${value}Z`)
}

export function toSession(response: AuthSessionResponse): Session {
  return {
    accessToken: response.accessToken,
    accessTokenExpiresAt: parseInstant(response.accessTokenExpiresAt),
    user: response.user,
    organization: response.organization,
    organizations: response.organizations,
    permissions: response.permissions,
  }
}

export interface SessionStore {
  getState(): AuthState
  subscribe(listener: () => void): () => void
  setSession(session: Session): void
  /** Updates user-facing profile data without touching the tokens (e.g. after a profile edit). */
  patchSession(
    patch: Partial<Pick<Session, 'user' | 'organization' | 'organizations' | 'permissions'>>,
  ): void
  clear(reason?: SignedOutReason): void
}

/** A tiny external store so non-React code (the API client, the router) can read and update the session. */
export function createSessionStore(): SessionStore {
  let state: AuthState = { status: 'unknown', session: null, reason: null }
  const listeners = new Set<() => void>()

  const set = (next: AuthState) => {
    state = next
    listeners.forEach((listener) => listener())
  }

  return {
    getState: () => state,
    subscribe(listener) {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
    setSession: (session) => set({ status: 'authenticated', session, reason: null }),
    patchSession(patch) {
      if (state.status === 'authenticated') {
        set({ status: 'authenticated', session: { ...state.session, ...patch }, reason: null })
      }
    },
    clear: (reason) => set({ status: 'anonymous', session: null, reason: reason ?? null }),
  }
}
