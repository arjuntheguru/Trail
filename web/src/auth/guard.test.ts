import { isRedirect } from '@tanstack/react-router'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import { problem, sessionResponse } from '@/test/fixtures'
import { createTestAuth } from '@/test/renderApp'
import { API, server } from '@/test/server'

import { toSession } from './session-store'

import { redirectIfAuthenticated, requireAuthenticated, safeRedirect } from './guard'

const REFRESH = `${API}/api/v1/auth/refresh`

async function caught(promise: Promise<unknown>) {
  try {
    await promise
  } catch (error) {
    return error
  }
  return undefined
}

describe('requireAuthenticated', () => {
  it('redirects to /login and remembers where the user was heading', async () => {
    server.use(
      http.post(REFRESH, () =>
        HttpResponse.json(problem(401, 'auth.invalid_refresh_token', 'Expired'), { status: 401 }),
      ),
    )
    const error = await caught(requireAuthenticated(createTestAuth(), '/today?site=1'))

    expect(isRedirect(error)).toBe(true)
    expect((error as { options: unknown }).options).toMatchObject({
      to: '/login',
      search: { redirect: '/today?site=1' },
    })
  })

  it('lets a restorable session through', async () => {
    server.use(http.post(REFRESH, () => HttpResponse.json(sessionResponse())))
    const session = await requireAuthenticated(createTestAuth(), '/today')
    expect(session.user.email).toBe('sam@example.com')
  })

  it('does not call the API when already signed in', async () => {
    const auth = createTestAuth()
    auth.store.setSession(toSession(sessionResponse()))
    // No handlers registered: an unexpected request would fail the test (onUnhandledFrame: error).
    await expect(requireAuthenticated(auth, '/today')).resolves.toBeDefined()
  })
})

describe('redirectIfAuthenticated', () => {
  it('sends signed-in users to /today', async () => {
    server.use(http.post(REFRESH, () => HttpResponse.json(sessionResponse())))
    const error = await caught(redirectIfAuthenticated(createTestAuth()))
    expect(isRedirect(error)).toBe(true)
    expect((error as { options: unknown }).options).toMatchObject({ to: '/today' })
  })

  it('lets anonymous visitors see the page', async () => {
    server.use(
      http.post(REFRESH, () => HttpResponse.json(problem(401, 'x', 'no'), { status: 401 })),
    )
    await expect(redirectIfAuthenticated(createTestAuth())).resolves.toBeUndefined()
  })
})

describe('safeRedirect', () => {
  it.each([
    ['/today', '/today'],
    ['/tasks?site=abc#x', '/tasks?site=abc#x'],
    [undefined, '/today'],
    ['', '/today'],
    ['https://evil.example/', '/today'],
    ['//evil.example/', '/today'],
    ['/\\evil.example', '/today'],
    ['javascript:alert(1)', '/today'],
  ])('%s -> %s', (input, expected) => {
    expect(safeRedirect(input)).toBe(expected)
  })
})
