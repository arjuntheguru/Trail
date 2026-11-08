import type { Session } from './session-store'

/**
 * What the app may show while it cannot reach the server: who the user is and what they may do. Never a
 * token. It is written while signed in and deleted when the session ends.
 */
export type SessionProfile = Pick<
  Session,
  'user' | 'organization' | 'organizations' | 'permissions'
>

export interface ProfileStore {
  load(): SessionProfile | null
  save(profile: SessionProfile): void
  clear(): void
}

const KEY = 'trail-last-session'

export function createLocalStorageProfileStore(storage: Storage = localStorage): ProfileStore {
  return {
    load() {
      try {
        const raw = storage.getItem(KEY)
        if (!raw) return null
        const value = JSON.parse(raw) as Partial<SessionProfile>
        if (
          !value.user?.id ||
          !Array.isArray(value.organizations) ||
          !Array.isArray(value.permissions)
        ) {
          return null
        }
        return value as SessionProfile
      } catch {
        return null
      }
    },
    save(profile) {
      try {
        storage.setItem(KEY, JSON.stringify(profile))
      } catch {
        // Storage may be full or blocked; offline startup is then unavailable, nothing else breaks.
      }
    },
    clear() {
      try {
        storage.removeItem(KEY)
      } catch {
        // Nothing to do.
      }
    },
  }
}
