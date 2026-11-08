import { QueryClient } from '@tanstack/react-query'
import { describe, expect, it } from 'vitest'

import { createSessionStore, toSession } from '@/auth/session-store'
import { ORG_A, ORG_B, SESSION_USER_ID, sessionResponse } from '@/test/fixtures'

import {
  isPersistedKey,
  shouldDehydrateQuery,
  startOfflineCache,
  storageKeyFor,
  type AsyncStringStorage,
} from './persistence'

function memoryStorage() {
  const data = new Map<string, string>()
  const storage: AsyncStringStorage = {
    getItem: async (key) => data.get(key) ?? null,
    setItem: async (key, value) => void data.set(key, value),
    removeItem: async (key) => void data.delete(key),
  }
  return { data, storage }
}

const flush = () => new Promise((resolve) => setTimeout(resolve, 20))

describe('isPersistedKey / shouldDehydrateQuery', () => {
  it('matches only sites, today task lists and task detail', () => {
    expect(isPersistedKey(['org', 'o1', 'sites'])).toBe(true)
    expect(isPersistedKey(['org', 'o1', 'site', 's1', 'tasks', 'today'])).toBe(true)
    expect(isPersistedKey(['org', 'o1', 'task', 't1'])).toBe(true)

    expect(isPersistedKey(['org', 'o1', 'attachment', 'a1', 'content'])).toBe(false)
    expect(isPersistedKey(['org', 'o1', 'tasks', 'list', {}])).toBe(false)
    expect(isPersistedKey(['org', 'o1', 'issues'])).toBe(false)
    expect(isPersistedKey(['user', 'o1', 'sites'])).toBe(false)
    expect(isPersistedKey(['org', 'o1', 'site', 's1', 'tasks'])).toBe(false)
  })

  it('persists only successful queries', () => {
    const key = ['org', 'o1', 'task', 't1']
    const state = (status: string) => ({ status }) as never
    expect(shouldDehydrateQuery({ queryKey: key, state: state('success') })).toBe(true)
    expect(shouldDehydrateQuery({ queryKey: key, state: state('error') })).toBe(false)
    expect(shouldDehydrateQuery({ queryKey: key, state: state('pending') })).toBe(false)
    expect(
      shouldDehydrateQuery({ queryKey: ['org', 'o1', 'issues'], state: state('success') }),
    ).toBe(false)
  })
})

describe('startOfflineCache', () => {
  const taskKey = ['org', ORG_A.id, 'task', 't1']
  const key = storageKeyFor(SESSION_USER_ID, ORG_A.id)

  function setup(initial?: Record<string, string>) {
    const { data, storage } = memoryStorage()
    for (const [k, v] of Object.entries(initial ?? {})) data.set(k, v)
    const store = createSessionStore()
    const queryClient = new QueryClient()
    const stop = startOfflineCache({ store, queryClient, storage, throttleTime: 0 })
    return { data, store, queryClient, stop }
  }

  it('writes only allow-listed queries under a key scoped to user and organization', async () => {
    const { data, store, queryClient, stop } = setup()
    store.setSession(toSession(sessionResponse()))
    await flush()

    queryClient.setQueryData(taskKey, { id: 't1' })
    queryClient.setQueryData(['org', ORG_A.id, 'issues'], { items: [] })
    await flush()

    const saved = data.get(key)!
    expect(saved).toContain('"t1"')
    expect(saved).not.toContain('issues')
    expect([...data.keys()]).toEqual([key])
    stop()
  })

  it('restores the cache for the same user and organization', async () => {
    const first = setup()
    first.store.setSession(toSession(sessionResponse()))
    await flush()
    first.queryClient.setQueryData(taskKey, { id: 't1' })
    await flush()
    first.stop()

    const second = setup(Object.fromEntries(first.data))
    second.store.setSession(toSession(sessionResponse()))
    await flush()
    expect(second.queryClient.getQueryData(taskKey)).toEqual({ id: 't1' })
    second.stop()
  })

  it('does not restore another user or organization', async () => {
    const other = storageKeyFor('someone-else', ORG_A.id)
    const { store, queryClient, stop } = setup({ [other]: '{"not":"read"}' })
    store.setSession(toSession(sessionResponse()))
    await flush()
    expect(queryClient.getQueryCache().getAll()).toHaveLength(0)
    stop()
  })

  it('deletes the entry on sign-out and ignores writes still in flight', async () => {
    const { data, store, queryClient, stop } = setup()
    store.setSession(toSession(sessionResponse()))
    await flush()
    queryClient.setQueryData(taskKey, { id: 't1' })
    await flush()
    expect(data.has(key)).toBe(true)

    store.clear('signed-out')
    queryClient.setQueryData(taskKey, { id: 't2' })
    await flush()

    expect(data.size).toBe(0)
    stop()
  })

  it('deletes the previous entry when the organization changes', async () => {
    const { data, store, queryClient, stop } = setup()
    store.setSession(toSession(sessionResponse()))
    await flush()
    queryClient.setQueryData(taskKey, { id: 't1' })
    await flush()

    store.setSession(
      toSession(sessionResponse({ organization: ORG_B, organizations: [ORG_A, ORG_B] })),
    )
    await flush()
    queryClient.setQueryData(['org', ORG_B.id, 'task', 't9'], { id: 't9' })
    await flush()

    expect(data.has(key)).toBe(false)
    expect([...data.keys()]).toEqual([storageKeyFor(SESSION_USER_ID, ORG_B.id)])
    stop()
  })

  it('keeps the entry across token refreshes', async () => {
    const { data, store, queryClient, stop } = setup()
    store.setSession(toSession(sessionResponse()))
    await flush()
    queryClient.setQueryData(taskKey, { id: 't1' })
    await flush()

    store.setSession(toSession(sessionResponse({ accessToken: 'access-2' })))
    await flush()

    expect(data.has(key)).toBe(true)
    expect(queryClient.getQueryData(taskKey)).toEqual({ id: 't1' })
    stop()
  })
})
