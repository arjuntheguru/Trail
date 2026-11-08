import type { Query, QueryClient } from '@tanstack/react-query'
import { createAsyncStoragePersister } from '@tanstack/query-async-storage-persister'
import {
  persistQueryClientRestore,
  persistQueryClientSubscribe,
} from '@tanstack/react-query-persist-client'
import { del, get, set } from 'idb-keyval'

import type { SessionStore } from '@/auth/session-store'

/** Persisted data older than this is discarded on restore. */
export const PERSIST_MAX_AGE_MS = 24 * 60 * 60 * 1000

const STORAGE_PREFIX = 'trail-query-cache'

export interface AsyncStringStorage {
  getItem(key: string): Promise<string | null | undefined>
  setItem(key: string, value: string): Promise<void>
  removeItem(key: string): Promise<void>
}

/** IndexedDB through idb-keyval; the persister hands it serialized strings. */
export const idbStorage: AsyncStringStorage = {
  getItem: (key) => get<string>(key),
  setItem: (key, value) => set(key, value),
  removeItem: (key) => del(key),
}

/**
 * Only what today's screens need to open offline: the site list, a site's task list for today, and task
 * detail. Everything else (history, issues, settings, attachment blobs) is never written to disk.
 */
export function isPersistedKey(key: readonly unknown[]): boolean {
  if (key[0] !== 'org') return false
  // ['org', orgId, 'sites']
  if (key.length === 3 && key[2] === 'sites') return true
  // ['org', orgId, 'site', siteId, 'tasks', 'today']
  if (key.length === 6 && key[2] === 'site' && key[4] === 'tasks' && key[5] === 'today') return true
  // ['org', orgId, 'task', taskId]
  return key.length === 4 && key[2] === 'task'
}

export function shouldDehydrateQuery(query: Pick<Query, 'queryKey' | 'state'>): boolean {
  return query.state.status === 'success' && isPersistedKey(query.queryKey)
}

/** One cache per signed-in user and organization, so data never crosses either boundary. */
export const storageKeyFor = (userId: string, organizationId: string) =>
  `${STORAGE_PREFIX}:${userId}:${organizationId}`

export interface OfflineCacheOptions {
  store: SessionStore
  queryClient: QueryClient
  storage?: AsyncStringStorage
  maxAge?: number
  throttleTime?: number
}

/**
 * Keeps the persisted query cache in step with the session: restores it when a user and organization become
 * active, writes allow-listed queries as they change, and deletes it when the user signs out or the
 * organization changes. Returns a stop function.
 */
export function startOfflineCache({
  store,
  queryClient,
  storage = idbStorage,
  maxAge = PERSIST_MAX_AGE_MS,
  throttleTime,
}: OfflineCacheOptions) {
  let active: { key: string; stop: () => void } | null = null

  async function remove(key: string) {
    try {
      await storage.removeItem(key)
    } catch {
      // Storage can be unavailable (private mode); there is nothing left to clean up then.
    }
  }

  function deactivate() {
    if (!active) return
    const { key, stop } = active
    active = null
    stop()
    void remove(key)
  }

  function activate(key: string) {
    let disposed = false
    // After disposal a throttled write still in flight must not bring the entry back.
    const guarded: AsyncStringStorage = {
      getItem: (k) => storage.getItem(k),
      removeItem: (k) => storage.removeItem(k),
      setItem: async (k, value) => {
        if (!disposed) await storage.setItem(k, value)
      },
    }
    const persister = createAsyncStoragePersister({ storage: guarded, key, throttleTime })
    let unsubscribe: (() => void) | undefined
    const entry = {
      key,
      stop: () => {
        disposed = true
        unsubscribe?.()
      },
    }
    active = entry

    void (async () => {
      try {
        await persistQueryClientRestore({ queryClient, persister, maxAge })
      } catch {
        // A corrupt or unreadable entry is simply ignored.
      }
      if (disposed) return
      unsubscribe = persistQueryClientSubscribe({
        queryClient,
        persister,
        dehydrateOptions: { shouldDehydrateQuery },
      })
    })()
  }

  function sync() {
    const state = store.getState()
    if (state.status === 'unknown') return
    const key =
      state.status === 'authenticated' && state.session.organization
        ? storageKeyFor(state.session.user.id, state.session.organization.id)
        : null
    if (active?.key === key) return
    if (active) {
      // Signed out, or a different user or organization: the previous entry goes.
      deactivate()
      queryClient.clear()
    }
    if (key) activate(key)
  }

  sync()
  const unsubscribeStore = store.subscribe(sync)
  return () => {
    unsubscribeStore()
    active?.stop()
    active = null
  }
}
