import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { renderApp } from '@/test/renderApp'
import { server } from '@/test/server'
import {
  DEFAULT_PREFERENCES,
  notification,
  notificationUrl,
  pagedNotifications,
  serveUnreadCount,
  type PreferencesFixture,
} from '@/test/notificationFixtures'
import { API } from '@/test/server'
import { recordBodies, signIn, url } from '@/test/settingsFixtures'

const ISSUE_ID = 'i0000000-0000-0000-0000-000000000001'
const TASK_ID = 't0000000-0000-0000-0000-000000000001'

const ISSUE_NOTE = notification({ id: 'n1', title: 'Issue assigned to you', subjectId: ISSUE_ID })
const TASK_NOTE = notification({
  id: 'n2',
  kind: 'TaskAwaitingApproval',
  title: 'Task needs approval',
  subjectType: 'Task',
  subjectId: TASK_ID,
})
const READ_NOTE = notification({
  id: 'n3',
  title: 'Old news',
  readAt: new Date().toISOString(),
})

/** The pages the app lands on after a click answer "not found"; these tests only check where it went. */
function serveTargets() {
  server.use(
    http.get(`${API}/api/v1/issues/:id`, () => HttpResponse.json({ status: 404 }, { status: 404 })),
    http.get(`${API}/api/v1/tasks/:id`, () => HttpResponse.json({ status: 404 }, { status: 404 })),
  )
}

/** Serves list requests, recording each query string. */
function serveList(respond: (params: URLSearchParams) => object) {
  const requests: URLSearchParams[] = []
  server.use(
    http.get(notificationUrl(''), ({ request }) => {
      const params = new URL(request.url).searchParams
      requests.push(params)
      return HttpResponse.json(respond(params))
    }),
  )
  return requests
}

// The bell is rendered in both the sidebar and the mobile top bar; CSS hides one, jsdom hides neither.
const bell = async () => (await screen.findAllByRole('button', { name: /^Notifications/ }))[0]!

afterEach(() => {
  vi.useRealTimers()
  Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true })
})

describe('notification bell', () => {
  it('shows the unread count', async () => {
    signIn()
    serveUnreadCount(3)
    serveList(() => pagedNotifications([]))
    await renderApp('/today')

    expect(await screen.findAllByLabelText('Notifications, 3 unread')).not.toHaveLength(0)
    expect((await screen.findAllByTestId('notification-count'))[0]).toHaveTextContent('3')
  })

  it('shows no badge at zero and caps the count at 99+', async () => {
    signIn()
    const state = serveUnreadCount(0)
    serveList(() => pagedNotifications([]))
    const { queryClient } = await renderApp('/today')

    await screen.findAllByRole('button', { name: 'Notifications' })
    expect(screen.queryByTestId('notification-count')).not.toBeInTheDocument()

    state.count = 150
    await queryClient.invalidateQueries({ queryKey: ['org'] })
    expect((await screen.findAllByTestId('notification-count'))[0]).toHaveTextContent('99+')
  })

  it('polls every minute and pauses while the tab is hidden', async () => {
    signIn()
    const state = serveUnreadCount(1)
    serveList(() => pagedNotifications([]))
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] })
    await renderApp('/today')
    await vi.waitFor(() => expect(state.requests).toBe(1))

    await vi.advanceTimersByTimeAsync(60_000)
    await vi.waitFor(() => expect(state.requests).toBe(2))

    Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true })
    document.dispatchEvent(new Event('visibilitychange'))
    await vi.advanceTimersByTimeAsync(180_000)
    expect(state.requests).toBe(2)

    Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true })
    await vi.advanceTimersByTimeAsync(60_000)
    await vi.waitFor(() => expect(state.requests).toBeGreaterThan(2))
  })

  it('lists the latest ten, marks one read and opens its issue', async () => {
    signIn()
    const state = serveUnreadCount(2)
    const requests = serveList(() => pagedNotifications([ISSUE_NOTE, TASK_NOTE, READ_NOTE]))
    let read: string | undefined
    server.use(
      http.post(notificationUrl('/:id/read'), ({ params }) => {
        read = String(params.id)
        state.count = 1
        return new HttpResponse(null, { status: 204 })
      }),
    )
    serveTargets()
    const user = userEvent.setup()
    const { router } = await renderApp('/today')

    await user.click(await bell())
    const list = await screen.findByRole('list', { name: 'Recent notifications' })
    expect(within(list).getAllByRole('listitem')).toHaveLength(3)
    expect(requests[0]?.get('PageSize')).toBe('10')
    expect(requests[0]?.get('Page')).toBe('1')

    await user.click(within(list).getByRole('button', { name: /Issue assigned to you/ }))

    await waitFor(() => expect(router.state.location.pathname).toBe(`/issues/${ISSUE_ID}`))
    expect(read).toBe('n1')
    await waitFor(() =>
      expect(screen.getAllByTestId('notification-count')[0]).toHaveTextContent('1'),
    )
    expect(screen.queryByRole('list', { name: 'Recent notifications' })).not.toBeInTheDocument()
  })

  it('opens the task of a task notification without marking a read one', async () => {
    signIn()
    serveUnreadCount(1)
    serveList(() => pagedNotifications([{ ...TASK_NOTE, readAt: new Date().toISOString() }]))
    const reads = recordBodies('post', '/notifications/n2/read')
    serveTargets()
    const user = userEvent.setup()
    const { router } = await renderApp('/today')

    await user.click(await bell())
    await user.click(await screen.findByRole('button', { name: /Task needs approval/ }))

    await waitFor(() => expect(router.state.location.pathname).toBe(`/tasks/${TASK_ID}`))
    expect(reads).toHaveLength(0)
  })

  it('marks all read and refreshes the count', async () => {
    signIn()
    const state = serveUnreadCount(2)
    serveList(() => pagedNotifications(state.count ? [ISSUE_NOTE, TASK_NOTE] : []))
    let calls = 0
    server.use(
      http.post(notificationUrl('/read-all'), () => {
        calls += 1
        state.count = 0
        return HttpResponse.json({ count: 2 })
      }),
    )
    const user = userEvent.setup()
    await renderApp('/today')

    await user.click(await bell())
    await user.click(await screen.findByRole('button', { name: 'Mark all read' }))

    await waitFor(() => expect(calls).toBe(1))
    await waitFor(() => expect(screen.queryByTestId('notification-count')).not.toBeInTheDocument())
  })

  it('links to the full page', async () => {
    signIn()
    serveUnreadCount(0)
    serveList(() => pagedNotifications([]))
    const user = userEvent.setup()
    const { router } = await renderApp('/today')

    await user.click(await bell())
    expect(await screen.findByText('You are all caught up.')).toBeInTheDocument()
    await user.click(screen.getByRole('link', { name: 'See all' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/notifications'))
  })
})

describe('notifications page', () => {
  it('groups notifications by day and marks unread ones', async () => {
    signIn()
    serveUnreadCount(1)
    const yesterday = new Date(Date.now() - 36 * 3600_000).toISOString()
    serveList(() =>
      pagedNotifications([ISSUE_NOTE, { ...READ_NOTE, id: 'old', createdAt: yesterday }]),
    )
    await renderApp('/notifications')

    const today = await screen.findByRole('region', { name: 'Today' })
    expect(within(today).getByRole('button', { name: /Issue assigned to you/ })).toHaveAttribute(
      'data-unread',
      'true',
    )
    const older = screen
      .getAllByRole('region')
      .find((r) => r.getAttribute('aria-label') !== 'Today')
    expect(older).toBeDefined()
    expect(within(older!).getByRole('button', { name: /Old news/ })).toHaveAttribute(
      'data-unread',
      'false',
    )
  })

  it('loads more pages', async () => {
    signIn()
    serveUnreadCount(0)
    const requests = serveList((params) =>
      params.get('Page') === '2'
        ? pagedNotifications([notification({ id: 'p2', title: 'Second page item' })], {
            page: 2,
            totalCount: 2,
          })
        : pagedNotifications([ISSUE_NOTE], { totalCount: 2, hasNextPage: true }),
    )
    const user = userEvent.setup()
    await renderApp('/notifications')

    expect(await screen.findByText('Showing 1 of 2')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Load more' }))

    expect(await screen.findByText('Second page item')).toBeInTheDocument()
    expect(screen.getByText('Showing 2 of 2')).toBeInTheDocument()
    expect(requests.map((r) => r.get('Page'))).toEqual(['1', '2'])
    expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument()
  })

  it('filters to unread through the server', async () => {
    signIn()
    serveUnreadCount(1)
    const requests = serveList((params) =>
      pagedNotifications(
        params.get('UnreadOnly') === 'true' ? [ISSUE_NOTE] : [ISSUE_NOTE, READ_NOTE],
      ),
    )
    const user = userEvent.setup()
    const { router } = await renderApp('/notifications')

    expect(await screen.findByText('Old news')).toBeInTheDocument()
    expect(requests[0]?.get('UnreadOnly')).toBeNull()
    await user.click(screen.getByRole('button', { name: 'Unread' }))

    await waitFor(() => expect(screen.queryByText('Old news')).not.toBeInTheDocument())
    expect(requests.at(-1)?.get('UnreadOnly')).toBe('true')
    expect(router.state.location.search).toEqual({ unread: true })
    expect(screen.getByRole('button', { name: 'Unread' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('shows an empty state for no unread notifications', async () => {
    signIn()
    serveUnreadCount(0)
    serveList(() => pagedNotifications([]))
    await renderApp('/notifications?unread=true')

    expect(await screen.findByText('No unread notifications')).toBeInTheDocument()
  })
})

describe('notification preferences', () => {
  const servePreferences = (preferences: PreferencesFixture = DEFAULT_PREFERENCES) => {
    server.use(http.get(url('/notifications/preferences'), () => HttpResponse.json(preferences)))
  }

  it('loads the saved choices', async () => {
    signIn()
    serveUnreadCount(0)
    servePreferences({ emailKinds: ['IssueRaised'], digestEnabled: false, digestHour: 9 })
    await renderApp('/settings/notifications')

    expect(await screen.findByRole('checkbox', { name: 'Issue raised' })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'Issue assigned to me' })).not.toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'Send me a daily digest' })).not.toBeChecked()
    expect(screen.getByLabelText('Send from')).toHaveValue('9')
    expect(screen.getByLabelText('Send from')).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Save changes' })).toBeDisabled()
  })

  it('saves the changes with a PUT in enum order', async () => {
    signIn()
    serveUnreadCount(0)
    servePreferences()
    const bodies = recordBodies<PreferencesFixture>('put', '/notifications/preferences', (body) =>
      HttpResponse.json(body),
    )
    const user = userEvent.setup()
    await renderApp('/settings/notifications')

    await user.click(await screen.findByRole('checkbox', { name: 'Issue commented' }))
    await user.click(screen.getByRole('checkbox', { name: 'Issue overdue' }))
    await user.selectOptions(screen.getByLabelText('Send from'), '18')
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toEqual({
      emailKinds: ['IssueAssigned', 'IssueCommented', 'IssueEscalated', 'TaskAwaitingApproval'],
      digestEnabled: true,
      digestHour: 18,
    })
    await waitFor(() => expect(screen.getByRole('button', { name: 'Save changes' })).toBeDisabled())
  })

  it('shows the server error when saving fails', async () => {
    signIn()
    serveUnreadCount(0)
    servePreferences()
    server.use(
      http.put(url('/notifications/preferences'), () =>
        HttpResponse.json(
          { status: 409, code: 'notification.preferences_busy', detail: 'Try again.' },
          { status: 409 },
        ),
      ),
    )
    const user = userEvent.setup()
    await renderApp('/settings/notifications')

    await user.click(await screen.findByRole('checkbox', { name: 'Issue raised' }))
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Try again.')
  })
})
