import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import { ORG_A, ORG_B, problem, sessionResponse } from '@/test/fixtures'
import { renderApp } from '@/test/renderApp'
import { API, server } from '@/test/server'

const REFRESH = `${API}/api/v1/auth/refresh`

const SITE = {
  id: 's1',
  name: 'Harbour Cafe',
  timeZone: 'Europe/London',
  isActive: true,
}

function task(overrides: Record<string, unknown>) {
  return {
    id: crypto.randomUUID(),
    siteId: 's1',
    siteName: 'Harbour Cafe',
    title: 'Task',
    kind: 'Checklist',
    status: 'Open',
    dueDate: '2026-11-07',
    dueAt: '2026-11-07T12:00:00Z',
    availableAt: null,
    isOverdue: false,
    isAwaitingApproval: false,
    requiresApproval: false,
    requiresPhoto: false,
    departmentId: null,
    source: 'Scheduled',
    entryCount: 0,
    completedAt: null,
    completedBy: null,
    completedLate: false,
    hasFailures: false,
    canCompleteLate: false,
    ...overrides,
  }
}

function signedInServer(permissions = ['tasks.view', 'issues.view', 'reports.view']) {
  server.use(http.post(REFRESH, () => HttpResponse.json(sessionResponse({ permissions }))))
}

describe('route guard', () => {
  it('redirects anonymous visitors to /login with the original destination', async () => {
    server.use(
      http.post(REFRESH, () => HttpResponse.json(problem(401, 'x', 'No session'), { status: 401 })),
    )
    const { router } = await renderApp('/tasks')

    expect(router.state.location.pathname).toBe('/login')
    expect(router.state.location.search).toEqual({ redirect: '/tasks' })
    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument()
  })

  it('restores the session on start and shows the requested page', async () => {
    signedInServer()
    const { router } = await renderApp('/tasks')

    expect(router.state.location.pathname).toBe('/tasks')
    expect(await screen.findByRole('heading', { name: 'Tasks' })).toBeInTheDocument()
  })

  it('moves a signed-in visitor off /login', async () => {
    signedInServer()
    server.use(
      http.get(`${API}/api/v1/sites`, () =>
        HttpResponse.json({ items: [], page: 1, pageSize: 100, totalCount: 0 }),
      ),
    )
    const { router } = await renderApp('/login')
    expect(router.state.location.pathname).toBe('/today')
  })

  it('sends the user to /login with the current page when the session expires', async () => {
    signedInServer()
    const { router, auth } = await renderApp('/tasks')
    await screen.findByRole('heading', { name: 'Tasks' })

    auth.store.clear('expired')

    await screen.findByRole('heading', { name: 'Sign in' })
    expect(router.state.location.search).toEqual({ redirect: '/tasks' })
  })
})

describe('app shell', () => {
  it('hides navigation the user has no permission for', async () => {
    signedInServer(['tasks.view'])
    await renderApp('/tasks')
    await screen.findByRole('heading', { name: 'Tasks' })

    const nav = screen.getAllByRole('navigation', { name: 'Main' })[0]!
    expect(within(nav).getByRole('link', { name: 'Today' })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: 'Settings' })).toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Issues' })).not.toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Reports' })).not.toBeInTheDocument()
  })

  it('offers a resend banner until the email is confirmed', async () => {
    server.use(
      http.post(REFRESH, () =>
        HttpResponse.json(
          sessionResponse({
            user: { ...sessionResponse().user, emailConfirmed: false },
          }),
        ),
      ),
    )
    await renderApp('/tasks')
    expect(await screen.findByText(/confirm your email address/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Resend' })).toBeInTheDocument()
  })

  it('shows the organization switcher only with more than one organization', async () => {
    server.use(
      http.post(REFRESH, () =>
        HttpResponse.json(sessionResponse({ organizations: [ORG_A, ORG_B] })),
      ),
    )
    await renderApp('/tasks')
    expect(
      (await screen.findAllByRole('button', { name: /Switch organization/ })).length,
    ).toBeGreaterThan(0)
  })
})

describe('Today', () => {
  it('lists the site tasks grouped by status', async () => {
    signedInServer()
    server.use(
      http.get(`${API}/api/v1/sites`, () =>
        HttpResponse.json({ items: [SITE], page: 1, pageSize: 100, totalCount: 1 }),
      ),
      http.get(`${API}/api/v1/sites/s1/tasks`, () =>
        HttpResponse.json({
          siteId: 's1',
          siteName: 'Harbour Cafe',
          date: '2026-11-07',
          timeZone: 'Europe/London',
          items: [
            task({ title: 'Opening checks', status: 'Open', isOverdue: true }),
            task({
              title: 'Fridge temperatures',
              status: 'Open',
              kind: 'RecordLog',
              entryCount: 2,
            }),
            task({
              title: 'Closing checks',
              status: 'Completed',
              completedAt: '2026-11-07T10:00:00Z',
              completedBy: 'Sam Chef',
            }),
            task({ title: 'Yesterday deep clean', status: 'Missed', canCompleteLate: true }),
          ],
        }),
      ),
    )
    await renderApp('/today')

    expect(await screen.findByText('Opening checks')).toBeInTheDocument()
    const groups = screen.getAllByRole('region').map((r) => r.getAttribute('aria-label'))
    expect(groups).toEqual(['Overdue', 'Due now', 'Missed', 'Completed'])
    expect(screen.getByText(/Done 10:00 AM by Sam Chef|Done 10:00 by Sam Chef/)).toBeInTheDocument()
    expect(screen.getByText(/2 readings/)).toBeInTheDocument()
    expect(screen.getByText('Can still be completed')).toBeInTheDocument()
  })

  it('shows an empty state when nothing is scheduled', async () => {
    signedInServer()
    server.use(
      http.get(`${API}/api/v1/sites`, () =>
        HttpResponse.json({ items: [SITE], page: 1, pageSize: 100, totalCount: 1 }),
      ),
      http.get(`${API}/api/v1/sites/s1/tasks`, () =>
        HttpResponse.json({
          siteId: 's1',
          siteName: 'Harbour Cafe',
          date: '2026-11-07',
          timeZone: 'Europe/London',
          items: [],
        }),
      ),
    )
    await renderApp('/today')
    expect(await screen.findByText('Nothing scheduled today')).toBeInTheDocument()
  })

  it('remembers the chosen site per organization', async () => {
    signedInServer()
    const second = { ...SITE, id: 's2', name: 'Dock Street' }
    const requested: string[] = []
    server.use(
      http.get(`${API}/api/v1/sites`, () =>
        HttpResponse.json({ items: [SITE, second], page: 1, pageSize: 100, totalCount: 2 }),
      ),
      http.get(`${API}/api/v1/sites/:siteId/tasks`, ({ params }) => {
        requested.push(String(params.siteId))
        return HttpResponse.json({
          siteId: params.siteId,
          siteName: 'x',
          date: '2026-11-07',
          timeZone: 'Europe/London',
          items: [],
        })
      }),
    )
    localStorage.setItem(`trail.lastSite.${ORG_A.id}`, 's2')
    await renderApp('/today')

    await screen.findByText('Nothing scheduled today')
    expect(requested).toEqual(['s2'])
    expect(screen.getByLabelText('Site')).toHaveValue('s2')
    localStorage.clear()
  })
})
