import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import { problem, sessionResponse } from '@/test/fixtures'
import { createTestAuth } from '@/test/renderApp'
import { API, server } from '@/test/server'

import { createLocalStorageProfileStore } from './profile-store'

const REFRESH = `${API}/api/v1/auth/refresh`
const SAVED_KEY = 'trail-last-session'

function memoryLocalStorage(): Storage {
  const data = new Map<string, string>()
  return {
    get length() {
      return data.size
    },
    clear: () => data.clear(),
    getItem: (k) => data.get(k) ?? null,
    key: (i) => [...data.keys()][i] ?? null,
    removeItem: (k) => void data.delete(k),
    setItem: (k, v) => void data.set(k, String(v)),
  }
}

/** Signs in once on a device, so its storage remembers the profile. */
async function rememberedDevice() {
  const storage = memoryLocalStorage()
  server.use(http.post(REFRESH, () => HttpResponse.json(sessionResponse())))
  await createTestAuth({ profileStore: createLocalStorageProfileStore(storage) }).initialize()
  return () => createTestAuth({ profileStore: createLocalStorageProfileStore(storage) })
}

describe('offline startup', () => {
  it('remembers the profile, without tokens, while signed in', async () => {
    const storage = memoryLocalStorage()
    server.use(http.post(REFRESH, () => HttpResponse.json(sessionResponse())))
    const auth = createTestAuth({ profileStore: createLocalStorageProfileStore(storage) })
    await auth.initialize()

    const saved = storage.getItem(SAVED_KEY)!
    expect(saved).toContain('sam@example.com')
    expect(saved).not.toContain('access-1')
    expect(saved).not.toContain('accessToken')
  })

  it('opens with the remembered profile when the server cannot be reached', async () => {
    const restart = await rememberedDevice()
    server.use(http.post(REFRESH, () => HttpResponse.error()))
    const auth = restart()
    await auth.initialize()

    const state = auth.store.getState()
    expect(state.status).toBe('authenticated')
    expect(state.session?.user.email).toBe('sam@example.com')
    expect(state.session?.permissions).toContain('tasks.view')
    expect(state.session?.accessToken).toBe('')
  })

  it('is anonymous when offline with nothing remembered', async () => {
    server.use(http.post(REFRESH, () => HttpResponse.error()))
    const auth = createTestAuth({
      profileStore: createLocalStorageProfileStore(memoryLocalStorage()),
    })
    await auth.initialize()
    expect(auth.store.getState().status).toBe('anonymous')
  })

  it('signs out and forgets the profile when the refresh token is rejected', async () => {
    const storage = memoryLocalStorage()
    server.use(http.post(REFRESH, () => HttpResponse.json(sessionResponse())))
    await createTestAuth({ profileStore: createLocalStorageProfileStore(storage) }).initialize()

    server.use(
      http.post(REFRESH, () => HttpResponse.json(problem(401, 'x', 'Expired'), { status: 401 })),
    )
    const auth = createTestAuth({ profileStore: createLocalStorageProfileStore(storage) })
    await auth.initialize()

    expect(auth.store.getState().status).toBe('anonymous')
    expect(storage.getItem(SAVED_KEY)).toBeNull()
  })

  it('does not use the remembered profile when the server answers with an error', async () => {
    const restart = await rememberedDevice()
    server.use(http.post(REFRESH, () => new HttpResponse(null, { status: 503 })))
    const auth = restart()
    await auth.initialize()
    expect(auth.store.getState().status).toBe('anonymous')
  })

  it('replaces the offline session with a real one on the next refresh', async () => {
    const restart = await rememberedDevice()
    server.use(http.post(REFRESH, () => HttpResponse.error()))
    const auth = restart()
    await auth.initialize()
    expect(auth.store.getState().session?.accessToken).toBe('')

    server.use(
      http.post(REFRESH, () => HttpResponse.json(sessionResponse({ accessToken: 'access-9' }))),
    )
    expect(await auth.refresh()).toBe('ok')
    expect(auth.store.getState().session?.accessToken).toBe('access-9')
  })
})
