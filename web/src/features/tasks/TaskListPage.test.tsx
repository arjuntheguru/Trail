import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { ISSUE_SITE_ID, problem, sessionResponse, taskDetail } from '@/test/fixtures'
import { renderApp } from '@/test/renderApp'
import { API, server } from '@/test/server'
import { taskListItem } from '@/test/taskListFixture'

const REFRESH = `${API}/api/v1/auth/refresh`
const SITE_2 = '55555555-0000-0000-0000-000000000002'

beforeEach(() => {
  // Only Date is faked, so timers and MSW keep working.
  vi.setSystemTime(new Date('2026-11-07T10:00:00'))
})
afterEach(() => {
  vi.useRealTimers()
})

function page(items: ReturnType<typeof taskListItem>[], extra: object = {}) {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    totalPages: 1,
    hasNextPage: false,
    ...extra,
  }
}

function signIn(permissions = ['tasks.view', 'sites.view']) {
  server.use(
    http.post(REFRESH, () => HttpResponse.json(sessionResponse({ permissions }))),
    http.get(`${API}/api/v1/sites`, () =>
      HttpResponse.json({
        items: [
          { id: ISSUE_SITE_ID, name: 'Harbour Cafe', timeZone: 'Europe/London', isActive: true },
          { id: SITE_2, name: 'Quay Bar', timeZone: 'America/New_York', isActive: true },
        ],
        page: 1,
        pageSize: 100,
        totalCount: 2,
      }),
    ),
  )
}

/** Serves the list, recording each request's query string. */
function serveTasks(
  respond: (params: URLSearchParams) => Response = () => HttpResponse.json(page([])),
) {
  const requests: URLSearchParams[] = []
  server.use(
    http.get(`${API}/api/v1/tasks`, ({ request }) => {
      const params = new URL(request.url).searchParams
      requests.push(params)
      return respond(params)
    }),
  )
  return { requests, last: () => requests.at(-1)! }
}

describe('task list', () => {
  it('asks for the last 7 days up to today across all sites by default', async () => {
    signIn()
    const api = serveTasks()
    await renderApp('/tasks')

    await screen.findByText('No tasks in this period')
    const p = api.requests[0]!
    expect(p.get('From')).toBe('2026-11-01')
    expect(p.get('To')).toBe('2026-11-07')
    expect(p.get('Page')).toBe('1')
    expect(p.get('PageSize')).toBe('20')
    for (const key of ['SiteId', 'Status', 'HasFailures', 'AwaitingApproval'])
      expect(p.has(key)).toBe(false)
  })

  it('shows each task with site, due time in the site zone, badges and who completed it', async () => {
    signIn()
    serveTasks(() =>
      HttpResponse.json(
        page([
          taskListItem({
            title: 'Closing checks',
            siteId: SITE_2,
            siteName: 'Quay Bar',
            dueAt: '2026-11-07T22:00:00Z',
            status: 'Completed',
            completedAt: '2026-11-07T23:30:00Z',
            completedBy: 'Pat Cook',
            completedLate: true,
            hasFailures: true,
            requiresApproval: true,
            isAwaitingApproval: true,
          }),
          taskListItem({
            id: '99999999-0000-0000-0000-000000000002',
            title: 'Fridge log',
            kind: 'RecordLog',
            isOverdue: true,
            dueDate: '2026-11-06',
            dueAt: '2026-11-06T09:00:00Z',
          }),
        ]),
      ),
    )
    await renderApp('/tasks')

    const rows = await screen.findAllByTestId('task-row')
    expect(rows).toHaveLength(2)
    const first = within(rows[0]!)
    expect(first.getByText('Closing checks')).toBeInTheDocument()
    expect(first.getByText('Quay Bar')).toBeInTheDocument()
    // 22:00Z is 17:00 (5:00 PM) in New York in November.
    expect(await first.findByText(/^Due (17:00|5:00)/)).toBeInTheDocument()
    expect(first.getByText(/by Pat Cook/)).toBeInTheDocument()
    expect(first.getByTestId('status-badge')).toHaveTextContent('Awaiting approval')
    expect(first.getByText('Completed late')).toBeInTheDocument()
    expect(first.getByText('Failed checks')).toBeInTheDocument()
    const second = within(rows[1]!)
    expect(second.getByTestId('status-badge')).toHaveTextContent('Overdue')
    expect(second.getByText('Record log')).toBeInTheDocument()
    expect(second.queryByText('Failed checks')).not.toBeInTheDocument()
  })

  it('groups rows under a heading per due date', async () => {
    signIn()
    serveTasks(() =>
      HttpResponse.json(
        page([
          taskListItem({ title: 'A' }),
          taskListItem({ id: 'b', title: 'B' }),
          taskListItem({ id: 'c', title: 'C', dueDate: '2026-11-06' }),
        ]),
      ),
    )
    await renderApp('/tasks')

    await screen.findByText('A')
    const groups = screen.getAllByRole('region')
    expect(groups).toHaveLength(2)
    expect(within(groups[0]!).getByRole('heading', { level: 2 })).toHaveTextContent('7 November')
    expect(within(groups[0]!).getAllByTestId('task-row')).toHaveLength(2)
    expect(within(groups[1]!).getByRole('heading', { level: 2 })).toHaveTextContent('6 November')
    expect(within(groups[1]!).getAllByTestId('task-row')).toHaveLength(1)
  })

  it('shows skeletons while loading', async () => {
    signIn()
    serveTasks(() => new Promise<Response>(() => {}) as unknown as Response)
    await renderApp('/tasks')
    expect(await screen.findByRole('status', { name: 'Loading tasks' })).toBeInTheDocument()
  })

  it('shows an error with a retry', async () => {
    const user = userEvent.setup()
    signIn()
    let fail = true
    serveTasks(() =>
      fail
        ? HttpResponse.json(problem(500, 'x', 'boom'), { status: 500 })
        : HttpResponse.json(page([taskListItem({ title: 'Back again' })])),
    )
    await renderApp('/tasks')

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong on our side')
    fail = false
    await user.click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByText('Back again')).toBeInTheDocument()
  })

  it('tells people without tasks.view they have no access', async () => {
    signIn(['sites.view'])
    const api = serveTasks()
    await renderApp('/tasks')
    expect(await screen.findByText(/do not have access to tasks/)).toBeInTheDocument()
    expect(api.requests).toHaveLength(0)
  })

  it('hides the site filter from people who cannot list sites', async () => {
    signIn(['tasks.view'])
    serveTasks()
    await renderApp('/tasks')
    await screen.findByText('No tasks in this period')
    expect(screen.queryByRole('combobox', { name: 'Site' })).not.toBeInTheDocument()
  })
})

describe('paging', () => {
  it('loads more and reports how many are shown', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serveTasks((params) =>
      params.get('Page') === '1'
        ? HttpResponse.json(
            page([taskListItem({ title: 'First page' })], {
              totalCount: 2,
              pageSize: 1,
              hasNextPage: true,
            }),
          )
        : HttpResponse.json(
            page([taskListItem({ id: 'p2', title: 'Second page' })], {
              page: 2,
              pageSize: 1,
              totalCount: 2,
            }),
          ),
    )
    await renderApp('/tasks')

    expect(await screen.findByText('Showing 1 of 2')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Load more' }))
    expect(await screen.findByText('Second page')).toBeInTheDocument()
    expect(screen.getByText('First page')).toBeInTheDocument()
    expect(screen.getByText('Showing 2 of 2')).toBeInTheDocument()
    expect(api.last().get('Page')).toBe('2')
    expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument()
  })
})

describe('filters', () => {
  it.each([
    ['Today', { range: 'today' }, '2026-11-07', '2026-11-07'],
    ['Yesterday', { range: 'yesterday' }, '2026-11-06', '2026-11-06'],
    ['Last 30 days', { range: '30d' }, '2026-10-09', '2026-11-07'],
  ])('preset %s sets the URL and the date range', async (label, search, from, to) => {
    const user = userEvent.setup()
    signIn()
    const api = serveTasks()
    const { router } = await renderApp('/tasks')

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Date range' }), label)
    await waitFor(() => expect(api.last().get('From')).toBe(from))
    expect(api.last().get('To')).toBe(to)
    expect(router.state.location.search).toEqual(search)
  })

  it('returns to the default range without leaving a param behind', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serveTasks()
    const { router } = await renderApp('/tasks?range=today')

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Date range' }), '7d')
    await waitFor(() => expect(api.last().get('From')).toBe('2026-11-01'))
    expect(router.state.location.search).toEqual({})
  })

  it('takes a custom range from the date inputs', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serveTasks()
    const { router } = await renderApp('/tasks')

    await user.selectOptions(
      await screen.findByRole('combobox', { name: 'Date range' }),
      'Custom range',
    )
    const from = await screen.findByLabelText('From')
    expect(from).toHaveValue('2026-11-01')
    fireEvent.change(from, { target: { value: '2026-10-20' } })
    await waitFor(() => expect(api.last().get('From')).toBe('2026-10-20'))
    expect(api.last().get('To')).toBe('2026-11-07')
    expect(router.state.location.search).toEqual({
      range: 'custom',
      from: '2026-10-20',
      to: '2026-11-07',
    })
  })

  it('explains a custom range the API would refuse instead of requesting it', async () => {
    signIn()
    const api = serveTasks()
    await renderApp('/tasks?range=custom&from=2026-11-05&to=2026-11-01')
    expect(await screen.findByRole('alert')).toHaveTextContent('end date must not be before')
    expect(api.requests).toHaveLength(0)
  })

  it('filters by site', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serveTasks()
    const { router } = await renderApp('/tasks')

    const select = await screen.findByRole('combobox', { name: 'Site' })
    await screen.findByRole('option', { name: 'Quay Bar' })
    await user.selectOptions(select, 'Quay Bar')
    await waitFor(() => expect(api.last().get('SiteId')).toBe(SITE_2))
    expect(router.state.location.search).toEqual({ siteId: SITE_2 })
  })

  it('filters by status', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serveTasks()
    const { router } = await renderApp('/tasks')

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Status' }), 'Missed')
    await waitFor(() => expect(api.last().get('Status')).toBe('Missed'))
    expect(router.state.location.search).toEqual({ status: 'Missed' })
  })

  it('toggles failures and awaiting approval on and off', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serveTasks()
    const { router } = await renderApp('/tasks')

    const failures = await screen.findByRole('button', { name: 'With failures' })
    await user.click(failures)
    await waitFor(() => expect(api.last().get('HasFailures')).toBe('true'))
    await user.click(screen.getByRole('button', { name: 'Awaiting approval' }))
    await waitFor(() => expect(api.last().get('AwaitingApproval')).toBe('true'))
    expect(router.state.location.search).toEqual({ failures: true, approval: true })

    await user.click(failures)
    await waitFor(() => expect(api.last().has('HasFailures')).toBe(false))
    expect(router.state.location.search).toEqual({ approval: true })
  })

  it('restores every filter from a shared link', async () => {
    signIn()
    const api = serveTasks()
    await renderApp(
      `/tasks?range=yesterday&siteId=${SITE_2}&status=Completed&failures=true&approval=true`,
    )

    await waitFor(() => expect(api.requests.length).toBeGreaterThan(0))
    const p = api.last()
    expect(p.get('From')).toBe('2026-11-06')
    expect(p.get('To')).toBe('2026-11-06')
    expect(p.get('SiteId')).toBe(SITE_2)
    expect(p.get('Status')).toBe('Completed')
    expect(p.get('HasFailures')).toBe('true')
    expect(p.get('AwaitingApproval')).toBe('true')
    expect(screen.getByRole('combobox', { name: 'Date range' })).toHaveValue('yesterday')
    expect(screen.getByRole('combobox', { name: 'Status' })).toHaveValue('Completed')
    expect(screen.getByRole('button', { name: 'With failures' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Site' })).toHaveValue(SITE_2))
  })

  it('falls back to defaults for bad URL values', async () => {
    signIn()
    const api = serveTasks()
    await renderApp('/tasks?range=forever&from=nope&status=Bogus&failures=maybe&approval=3')

    await screen.findByText('No tasks in this period')
    const p = api.requests[0]!
    expect(p.get('From')).toBe('2026-11-01')
    expect(p.get('To')).toBe('2026-11-07')
    for (const key of ['Status', 'HasFailures', 'AwaitingApproval']) expect(p.has(key)).toBe(false)
  })

  it('offers Clear filters when nothing matches, and the default view after', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serveTasks((p) =>
      HttpResponse.json(p.has('Status') ? page([]) : page([taskListItem({ title: 'Everything' })])),
    )
    const { router } = await renderApp('/tasks?status=Missed&failures=true&range=today')

    expect(await screen.findByText('No tasks match these filters')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Clear filters' }))
    expect(await screen.findByText('Everything')).toBeInTheDocument()
    expect(router.state.location.search).toEqual({})
    expect(api.last().get('From')).toBe('2026-11-01')
  })
})

describe('opening a task', () => {
  it('links each row to the task screen, whose back link returns to the filtered list', async () => {
    const user = userEvent.setup()
    signIn()
    const item = taskListItem({ title: 'Linked one' })
    const api = serveTasks(() => HttpResponse.json(page([item])))
    server.use(
      http.get(`${API}/api/v1/tasks/${item.id}`, () =>
        HttpResponse.json(taskDetail({ id: item.id, title: 'Linked one' })),
      ),
    )
    const { router } = await renderApp('/tasks?status=Open&failures=true')

    await user.click(await screen.findByText('Linked one'))
    await waitFor(() => expect(router.state.location.pathname).toBe(`/tasks/${item.id}`))
    await screen.findByRole('heading', { level: 1, name: 'Linked one' })

    await user.click(within(screen.getByRole('main')).getByRole('link', { name: 'Tasks' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/tasks'))
    expect(router.state.location.search).toEqual({ status: 'Open', failures: true })
    await waitFor(() => expect(api.last().get('Status')).toBe('Open'))
    expect(api.last().get('HasFailures')).toBe('true')
  })

  it('ignores a back target that is not Today or the Tasks list', async () => {
    signIn()
    const item = taskDetail()
    server.use(http.get(`${API}/api/v1/tasks/${item.id}`, () => HttpResponse.json(item)))
    await renderApp(`/tasks/${item.id}?from=${encodeURIComponent('https://evil.example/x')}`)

    await screen.findByRole('heading', { name: 'Opening checks' })
    const back = within(screen.getByRole('main')).getByRole('link', { name: 'Today' })
    expect(back).toHaveAttribute('href', '/today')
  })
})
