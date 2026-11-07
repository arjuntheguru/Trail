import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { ApiError } from '@/api/problem'
import { unwrap } from '@/api/unwrap'
import { problem, sessionResponse } from '@/test/fixtures'
import { createTestAuth } from '@/test/renderApp'
import { API, server } from '@/test/server'

import { toSession } from './session-store'

const REFRESH = `${API}/api/v1/auth/refresh`
const ME = `${API}/api/v1/me`

function signedIn(auth = createTestAuth(), token = 'access-1') {
  auth.store.setSession(toSession(sessionResponse({ accessToken: token })))
  return auth
}

/** A /me endpoint that only accepts the given bearer token. */
function meAccepts(token: string, hits: string[] = []) {
  return http.get(ME, ({ request }) => {
    const header = request.headers.get('authorization') ?? ''
    hits.push(header)
    return header === `Bearer ${token}`
      ? HttpResponse.json({ ok: true })
      : HttpResponse.json(problem(401, 'auth.unauthorized', 'Unauthorized'), { status: 401 })
  })
}

afterEach(() => vi.useRealTimers())

describe('session restore on start', () => {
  it('restores the session from the refresh cookie with an empty body and the cookie-transport header', async () => {
    let seen: { header: string | null; body: string } | undefined
    server.use(
      http.post(REFRESH, async ({ request }) => {
        seen = {
          header: request.headers.get('x-refresh-token-transport'),
          body: await request.text(),
        }
        return HttpResponse.json(sessionResponse({ accessToken: 'restored' }))
      }),
    )
    const credentials: RequestCredentials[] = []
    const auth = createTestAuth({
      fetch: (request) => {
        credentials.push(request.credentials)
        return fetch(request)
      },
    })

    expect(auth.store.getState().status).toBe('unknown')
    await auth.initialize()

    const state = auth.store.getState()
    expect(state.status).toBe('authenticated')
    expect(state.session?.accessToken).toBe('restored')
    expect(state.session?.user.email).toBe('sam@example.com')
    expect(seen).toEqual({ header: 'cookie', body: '' })
    expect(credentials).toEqual(['include'])
  })

  it('becomes anonymous when there is no valid refresh cookie', async () => {
    server.use(
      http.post(REFRESH, () =>
        HttpResponse.json(problem(401, 'auth.invalid_refresh_token', 'Expired'), { status: 401 }),
      ),
    )
    const auth = createTestAuth()
    await auth.initialize()
    expect(auth.store.getState().status).toBe('anonymous')
  })

  it('becomes anonymous (without throwing) when the server is unreachable', async () => {
    server.use(http.post(REFRESH, () => HttpResponse.error()))
    const auth = createTestAuth()
    await auth.initialize()
    expect(auth.store.getState().status).toBe('anonymous')
  })

  it('only restores once, however often it is called', async () => {
    let calls = 0
    server.use(
      http.post(REFRESH, () => {
        calls++
        return HttpResponse.json(sessionResponse())
      }),
    )
    const auth = createTestAuth()
    await Promise.all([auth.initialize(), auth.initialize()])
    await auth.initialize()
    expect(calls).toBe(1)
  })
})

describe('401 handling', () => {
  it('shares one refresh between concurrent 401s and retries each request once with the new token', async () => {
    let refreshCalls = 0
    const hits: string[] = []
    server.use(
      meAccepts('access-2', hits),
      http.post(REFRESH, async () => {
        refreshCalls++
        await new Promise((r) => setTimeout(r, 30)) // let the other 401s pile up behind it
        return HttpResponse.json(sessionResponse({ accessToken: 'access-2' }))
      }),
    )
    const auth = signedIn()

    const results = await Promise.all([
      auth.client.GET('/api/v1/me'),
      auth.client.GET('/api/v1/me'),
      auth.client.GET('/api/v1/me'),
    ])

    expect(results.map((r) => r.response.status)).toEqual([200, 200, 200])
    expect(refreshCalls).toBe(1)
    // 3 original attempts with the old token, then exactly 3 retries with the new one.
    expect(hits.filter((h) => h === 'Bearer access-1')).toHaveLength(3)
    expect(hits.filter((h) => h === 'Bearer access-2')).toHaveLength(3)
    expect(auth.store.getState().session?.accessToken).toBe('access-2')
  })

  it('does not refresh again when a request that failed with the old token finds a newer one', async () => {
    let refreshCalls = 0
    server.use(
      http.get(ME, async ({ request }) => {
        const ok = request.headers.get('authorization') === 'Bearer access-2'
        if (!ok) await new Promise((r) => setTimeout(r, 40))
        return ok
          ? HttpResponse.json({ ok: true })
          : HttpResponse.json(problem(401, 'x', 'no'), { status: 401 })
      }),
      http.post(REFRESH, () => {
        refreshCalls++
        return HttpResponse.json(sessionResponse({ accessToken: 'access-2' }))
      }),
    )
    const auth = signedIn()

    const slow = auth.client.GET('/api/v1/me') // will 401 after the refresh below completed
    await auth.refresh()
    const result = await slow

    expect(result.response.status).toBe(200)
    expect(refreshCalls).toBe(1)
  })

  it('retries only once: a second 401 is returned to the caller', async () => {
    let refreshCalls = 0
    let meCalls = 0
    server.use(
      http.get(ME, () => {
        meCalls++
        return HttpResponse.json(problem(401, 'auth.unauthorized', 'Unauthorized'), { status: 401 })
      }),
      http.post(REFRESH, () => {
        refreshCalls++
        return HttpResponse.json(sessionResponse({ accessToken: 'access-2' }))
      }),
    )
    const auth = signedIn()

    const { response } = await auth.client.GET('/api/v1/me')

    expect(response.status).toBe(401)
    expect(meCalls).toBe(2)
    expect(refreshCalls).toBe(1)
  })

  it('replays the request body on retry', async () => {
    const bodies: string[] = []
    server.use(
      http.put(ME, async ({ request }) => {
        bodies.push(await request.text())
        return request.headers.get('authorization') === 'Bearer access-2'
          ? HttpResponse.json({ ok: true })
          : HttpResponse.json(problem(401, 'x', 'no'), { status: 401 })
      }),
      http.post(REFRESH, () => HttpResponse.json(sessionResponse({ accessToken: 'access-2' }))),
    )
    const auth = signedIn()

    const { response } = await auth.client.PUT('/api/v1/me', {
      body: { firstName: 'Sam', lastName: 'Chef' },
    })

    expect(response.status).toBe(200)
    expect(bodies).toEqual([
      JSON.stringify({ firstName: 'Sam', lastName: 'Chef' }),
      JSON.stringify({ firstName: 'Sam', lastName: 'Chef' }),
    ])
  })

  it('clears the session when the refresh token is rejected, and reports why', async () => {
    server.use(
      meAccepts('never'),
      http.post(REFRESH, () =>
        HttpResponse.json(problem(401, 'auth.invalid_refresh_token', 'Expired'), { status: 401 }),
      ),
    )
    const auth = signedIn()

    const { response } = await auth.client.GET('/api/v1/me')

    expect(response.status).toBe(401)
    const state = auth.store.getState()
    expect(state.status).toBe('anonymous')
    expect(state.reason).toBe('expired')
  })

  it('keeps the session when the refresh fails transiently (server error)', async () => {
    server.use(
      meAccepts('never'),
      http.post(REFRESH, () => HttpResponse.json(problem(503, 'x', 'down'), { status: 503 })),
    )
    const auth = signedIn()

    const { response } = await auth.client.GET('/api/v1/me')

    expect(response.status).toBe(401)
    expect(auth.store.getState().status).toBe('authenticated')
  })

  it('refreshes before sending when the access token is about to expire', async () => {
    const hits: string[] = []
    let refreshCalls = 0
    server.use(
      meAccepts('access-2', hits),
      http.post(REFRESH, () => {
        refreshCalls++
        return HttpResponse.json(sessionResponse({ accessToken: 'access-2' }))
      }),
    )
    const auth = createTestAuth()
    auth.store.setSession(
      toSession(sessionResponse({ accessToken: 'access-1', expiresInMs: 1_000 })),
    )

    const { response } = await auth.client.GET('/api/v1/me')

    expect(response.status).toBe(200)
    expect(refreshCalls).toBe(1)
    expect(hits).toEqual(['Bearer access-2']) // the stale token was never sent
  })

  it('never retries or refreshes for anonymous endpoints such as login', async () => {
    let refreshCalls = 0
    server.use(
      http.post(`${API}/api/v1/auth/login`, () =>
        HttpResponse.json(problem(401, 'auth.invalid_credentials', 'Wrong'), { status: 401 }),
      ),
      http.post(REFRESH, () => {
        refreshCalls++
        return HttpResponse.json(sessionResponse())
      }),
    )
    const auth = signedIn()

    await expect(auth.login({ email: 'a@b.co', password: 'x' })).rejects.toMatchObject({
      status: 401,
      code: 'auth.invalid_credentials',
    })
    expect(refreshCalls).toBe(0)
  })
})

describe('cross-tab refresh', () => {
  it('serializes refreshes from different tabs through the shared lock', async () => {
    // Two services stand in for two tabs; a promise chain stands in for navigator.locks.
    let tail: Promise<unknown> = Promise.resolve()
    const lock = <T>(task: () => Promise<T>) => {
      const run = tail.then(task, task)
      tail = run.catch(() => undefined)
      return run
    }
    let active = 0
    let maxActive = 0
    let calls = 0
    server.use(
      http.post(REFRESH, async () => {
        active++
        maxActive = Math.max(maxActive, active)
        calls++
        await new Promise((r) => setTimeout(r, 20))
        active--
        return HttpResponse.json(sessionResponse({ accessToken: `token-${calls}` }))
      }),
    )
    const tabA = createTestAuth({ withLock: lock })
    const tabB = createTestAuth({ withLock: lock })

    await Promise.all([tabA.refresh(), tabB.refresh()])

    expect(calls).toBe(2)
    expect(maxActive).toBe(1)
  })

  it('uses the Web Locks API when the browser has it', async () => {
    const request = vi.fn(async (_name: string, task: () => Promise<unknown>) => task())
    vi.stubGlobal('navigator', { ...navigator, locks: { request } })
    try {
      server.use(http.post(REFRESH, () => HttpResponse.json(sessionResponse())))
      await createTestAuth().refresh()
      expect(request).toHaveBeenCalledWith('trail-refresh', expect.any(Function))
    } finally {
      vi.unstubAllGlobals()
    }
  })
})

describe('refresh scheduler', () => {
  it('refreshes shortly before the access token expires, then schedules the next one', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    let calls = 0
    server.use(
      http.post(REFRESH, () => {
        calls++
        return HttpResponse.json(sessionResponse({ accessToken: `access-${calls + 1}` }))
      }),
    )
    const auth = createTestAuth()
    const stop = auth.startRefreshScheduler()
    auth.store.setSession(toSession(sessionResponse({ expiresInMs: 15 * 60_000 })))

    await vi.advanceTimersByTimeAsync(13 * 60_000)
    expect(calls).toBe(0)

    await vi.advanceTimersByTimeAsync(90_000) // past the 60 s lead time
    await vi.waitFor(() => expect(calls).toBe(1))
    expect(auth.store.getState().session?.accessToken).toBe('access-2')

    // The new token (15 min again) gets its own timer.
    await vi.advanceTimersByTimeAsync(14 * 60_000)
    await vi.waitFor(() => expect(calls).toBe(2))
    stop()
  })

  it('stops refreshing after sign-out', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    let calls = 0
    server.use(
      http.post(REFRESH, () => {
        calls++
        return HttpResponse.json(sessionResponse())
      }),
      http.post(`${API}/api/v1/auth/logout`, () => new HttpResponse(null, { status: 204 })),
    )
    const auth = createTestAuth()
    const stop = auth.startRefreshScheduler()
    auth.store.setSession(toSession(sessionResponse()))
    await auth.logout()

    await vi.advanceTimersByTimeAsync(20 * 60_000)
    expect(calls).toBe(0)
    stop()
  })
})

describe('sign-in and sign-out', () => {
  it('stores the session after login and sends the cookie-transport header', async () => {
    let header: string | null = null
    server.use(
      http.post(`${API}/api/v1/auth/login`, async ({ request }) => {
        header = request.headers.get('x-refresh-token-transport')
        expect(await request.json()).toEqual({ email: 'sam@example.com', password: 'pw-123456789' })
        return HttpResponse.json(sessionResponse({ accessToken: 'fresh' }))
      }),
    )
    const auth = createTestAuth()

    await auth.login({ email: 'sam@example.com', password: 'pw-123456789' })

    expect(header).toBe('cookie')
    expect(auth.store.getState().session?.accessToken).toBe('fresh')
    // The refresh token is an httpOnly cookie; nothing resembling it is held in page memory.
    expect(JSON.stringify(auth.store.getState())).not.toContain('refreshToken')
  })

  it('logout clears local state even when the server is unreachable', async () => {
    server.use(http.post(`${API}/api/v1/auth/logout`, () => HttpResponse.error()))
    const auth = signedIn()
    await auth.logout()
    const state = auth.store.getState()
    expect(state.status).toBe('anonymous')
    expect(state.reason).toBe('signed-out')
  })

  it('switching organization replaces the session', async () => {
    server.use(
      http.post(`${API}/api/v1/auth/switch-organization`, async ({ request }) => {
        expect(request.headers.get('authorization')).toBe('Bearer access-1')
        expect(await request.json()).toEqual({ organizationId: 'org-2' })
        return HttpResponse.json(sessionResponse({ accessToken: 'org2-token' }))
      }),
    )
    const auth = signedIn()
    await auth.switchOrganization('org-2')
    expect(auth.store.getState().session?.accessToken).toBe('org2-token')
  })

  it('unwrap turns a ProblemDetails body into an ApiError and network failures into status 0', async () => {
    server.use(
      http.get(ME, () =>
        HttpResponse.json(problem(404, 'site.not_found', 'Site not found.'), { status: 404 }),
      ),
    )
    const auth = signedIn()
    const failure = await unwrap(auth.client.GET('/api/v1/me')).catch((e: unknown) => e)
    expect(failure).toBeInstanceOf(ApiError)
    expect(failure).toMatchObject({
      status: 404,
      code: 'site.not_found',
      message: 'Site not found.',
    })

    server.use(http.get(ME, () => HttpResponse.error()))
    await expect(unwrap(auth.client.GET('/api/v1/me'))).rejects.toMatchObject({ status: 0 })
  })
})
