import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import type { components } from '@/api/schema'
import { ISSUE_SITE_ID, issueListItem, problem, sessionResponse } from '@/test/fixtures'
import { renderApp } from '@/test/renderApp'
import { API, server } from '@/test/server'

type Summary = components['schemas']['IssueSummaryResponse']

const REFRESH = `${API}/api/v1/auth/refresh`
const SITE_2 = '55555555-0000-0000-0000-000000000002'

const SUMMARY: Summary = {
  byStatus: { Open: 5, InProgress: 2, Resolved: 1, Closed: 9 },
  overdue: 3,
  activeByPriority: { Low: 1, Medium: 3, High: 2, Critical: 1 },
  assignedToMe: 4,
}

function page(items: ReturnType<typeof issueListItem>[], extra: object = {}) {
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

function signIn(permissions = ['issues.view', 'issues.raise', 'sites.view']) {
  server.use(
    http.post(REFRESH, () => HttpResponse.json(sessionResponse({ permissions }))),
    http.get(`${API}/api/v1/sites`, () =>
      HttpResponse.json({
        items: [
          { id: ISSUE_SITE_ID, name: 'Harbour Cafe', timeZone: 'Europe/London', isActive: true },
          { id: SITE_2, name: 'Quay Bar', timeZone: 'Europe/London', isActive: true },
        ],
        page: 1,
        pageSize: 100,
        totalCount: 2,
      }),
    ),
  )
}

/** Serves the list and summary, recording each list request's query string. */
function serveIssues(
  respond: (params: URLSearchParams) => Response = () => HttpResponse.json(page([])),
  summary: Summary = SUMMARY,
) {
  const requests: URLSearchParams[] = []
  const summaryRequests: URLSearchParams[] = []
  server.use(
    http.get(`${API}/api/v1/issues`, ({ request }) => {
      const params = new URL(request.url).searchParams
      requests.push(params)
      return respond(params)
    }),
    http.get(`${API}/api/v1/issues/summary`, ({ request }) => {
      summaryRequests.push(new URL(request.url).searchParams)
      return HttpResponse.json(summary)
    }),
  )
  return { requests, summaryRequests, last: () => requests.at(-1)! }
}

describe('issues list', () => {
  it('shows each issue with its number, site, badges, assignee and due state', async () => {
    signIn()
    serveIssues(() =>
      HttpResponse.json(
        page([
          issueListItem({
            number: 7,
            title: 'Walk-in freezer alarm',
            priority: 'Critical',
            status: 'InProgress',
            source: 'TaskFailure',
            assignee: { membershipId: 'm1', name: 'Pat Manager' },
            dueAt: '2026-11-05T09:00:00Z',
            isOverdue: true,
            photoCount: 2,
          }),
          issueListItem({ number: 8, title: 'Broken tap', siteName: 'Quay Bar' }),
        ]),
      ),
    )
    await renderApp('/issues')

    const rows = await screen.findAllByTestId('issue-row')
    expect(rows).toHaveLength(2)
    const first = within(rows[0]!)
    expect(first.getByText('#7')).toBeInTheDocument()
    expect(first.getByText('Walk-in freezer alarm')).toBeInTheDocument()
    expect(first.getByText('Harbour Cafe')).toBeInTheDocument()
    expect(first.getByTestId('priority-badge')).toHaveTextContent('Critical')
    expect(first.getByTestId('status-badge')).toHaveTextContent('In progress')
    expect(first.getByText('Pat Manager')).toBeInTheDocument()
    expect(first.getByTestId('due-indicator')).toHaveTextContent(/^Overdue/)
    expect(first.getByRole('img', { name: 'Raised from a task' })).toBeInTheDocument()
    expect(first.getByText('2')).toBeInTheDocument()

    const second = within(rows[1]!)
    expect(second.getByText('Unassigned')).toBeInTheDocument()
    expect(second.getByText('Quay Bar')).toBeInTheDocument()
    expect(second.getByRole('img', { name: 'Raised manually' })).toBeInTheDocument()
    expect(second.queryByTestId('due-indicator')).not.toBeInTheDocument()
  })

  it('asks for open and in-progress issues by default, first page of 20', async () => {
    signIn()
    const api = serveIssues()
    await renderApp('/issues')

    await screen.findByText('No open issues')
    expect(api.requests[0]?.getAll('Status')).toEqual(['Open', 'InProgress'])
    expect(api.requests[0]?.get('Page')).toBe('1')
    expect(api.requests[0]?.get('PageSize')).toBe('20')
  })

  it('shows skeletons while loading', async () => {
    signIn()
    serveIssues(() => new Promise<Response>(() => {}) as unknown as Response)
    await renderApp('/issues')
    expect(await screen.findByRole('status', { name: 'Loading issues' })).toBeInTheDocument()
  })

  it('shows an error with a retry', async () => {
    const user = userEvent.setup()
    signIn()
    let fail = true
    serveIssues(() =>
      fail
        ? HttpResponse.json(problem(500, 'x', 'boom'), { status: 500 })
        : HttpResponse.json(page([issueListItem({ title: 'Back again' })])),
    )
    await renderApp('/issues')

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong on our side')
    fail = false
    await user.click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByText('Back again')).toBeInTheDocument()
  })

  it('hides Raise issue from people who cannot raise', async () => {
    signIn(['issues.view'])
    serveIssues()
    await renderApp('/issues')
    await screen.findByText('No open issues')
    expect(screen.queryByRole('link', { name: /Raise issue/ })).not.toBeInTheDocument()
  })

  it('links to the raise form and to each issue', async () => {
    const user = userEvent.setup()
    signIn()
    const item = issueListItem({ title: 'Linked one' })
    serveIssues(() => HttpResponse.json(page([item])))
    server.use(http.get(`${API}/api/v1/issues/${item.id}`, () => new Promise(() => {})))
    const { router } = await renderApp('/issues')

    expect(await screen.findByRole('link', { name: /Raise issue/ })).toHaveAttribute(
      'href',
      '/issues/new',
    )
    await user.click(await screen.findByText('Linked one'))
    await waitFor(() => expect(router.state.location.pathname).toBe(`/issues/${item.id}`))
  })
})

describe('summary strip', () => {
  it('shows counts by status, overdue and assigned to me', async () => {
    signIn()
    serveIssues()
    await renderApp('/issues')

    const strip = await screen.findByRole('list', { name: 'Issue summary' })
    const count = (label: string) => within(strip).getByRole('button', { name: new RegExp(label) })
    expect(count('Open')).toHaveTextContent('5')
    expect(count('In progress')).toHaveTextContent('2')
    expect(count('Resolved')).toHaveTextContent('1')
    expect(count('Overdue')).toHaveTextContent('3')
    expect(count('Mine')).toHaveTextContent('4')
  })

  it('filters the list when a count is pressed, and clears when pressed again', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serveIssues()
    const { router } = await renderApp('/issues')

    const overdue = await screen.findByRole('button', { name: /Overdue/ })
    await user.click(overdue)

    await waitFor(() => expect(api.last().get('Overdue')).toBe('true'))
    expect(router.state.location.search).toEqual({ overdue: true })
    expect(overdue).toHaveAttribute('aria-pressed', 'true')

    await user.click(overdue)
    await waitFor(() => expect(api.last().get('Overdue')).toBeNull())
    expect(router.state.location.search).toEqual({})
  })

  it('scopes the counts to the chosen site', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serveIssues()
    await renderApp('/issues')

    await screen.findByRole('list', { name: 'Issue summary' })
    await user.selectOptions(await screen.findByRole('combobox', { name: 'Site' }), 'Quay Bar')
    await waitFor(() => expect(api.summaryRequests.at(-1)?.get('SiteId')).toBe(SITE_2))
  })
})

describe('filters in the URL', () => {
  it('reflects each filter in the URL and the request', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serveIssues()
    const { router } = await renderApp('/issues')
    await screen.findByText('No open issues')

    await user.selectOptions(screen.getByRole('combobox', { name: 'Status' }), 'Resolved')
    await waitFor(() => expect(api.last().getAll('Status')).toEqual(['Resolved']))
    expect(router.state.location.search).toEqual({ status: 'resolved' })

    await user.selectOptions(screen.getByRole('combobox', { name: 'Priority' }), 'High')
    await waitFor(() => expect(api.last().get('Priority')).toBe('High'))

    await user.selectOptions(screen.getByRole('combobox', { name: 'Site' }), 'Quay Bar')
    await waitFor(() => expect(api.last().get('SiteId')).toBe(SITE_2))

    await user.click(screen.getByRole('button', { name: 'Assigned to me' }))
    await waitFor(() => expect(api.last().get('Mine')).toBe('true'))

    await user.click(screen.getByRole('button', { name: 'Overdue', pressed: false }))
    await waitFor(() => expect(api.last().get('Overdue')).toBe('true'))

    expect(router.state.location.search).toEqual({
      status: 'resolved',
      priority: 'High',
      siteId: SITE_2,
      mine: true,
      overdue: true,
    })
  })

  it('searches by text and keeps the term in the URL', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serveIssues()
    const { router } = await renderApp('/issues')
    await screen.findByText('No open issues')

    await user.type(screen.getByRole('searchbox', { name: 'Search issues' }), '#42')
    await waitFor(() => expect(api.last().get('Search')).toBe('#42'))
    expect(router.state.location.search).toEqual({ q: '#42' })
  })

  it('starts from the filters in a shared link', async () => {
    signIn()
    const api = serveIssues()
    await renderApp(`/issues?status=closed&priority=Low&siteId=${SITE_2}&mine=true&q=leak`)

    await waitFor(() => expect(api.requests.length).toBeGreaterThan(0))
    const params = api.requests[0]!
    expect(params.getAll('Status')).toEqual(['Closed'])
    expect(params.get('Priority')).toBe('Low')
    expect(params.get('SiteId')).toBe(SITE_2)
    expect(params.get('Mine')).toBe('true')
    expect(params.get('Search')).toBe('leak')
    expect(screen.getByRole('combobox', { name: 'Status' })).toHaveValue('closed')
    expect(screen.getByRole('searchbox', { name: 'Search issues' })).toHaveValue('leak')
  })

  it('ignores unusable values in the URL', async () => {
    signIn()
    const api = serveIssues()
    await renderApp('/issues?status=bogus&priority=Urgent&mine=maybe')

    await screen.findByText('No open issues')
    expect(api.requests[0]?.getAll('Status')).toEqual(['Open', 'InProgress'])
    expect(api.requests[0]?.get('Priority')).toBeNull()
    expect(api.requests[0]?.get('Mine')).toBeNull()
  })

  it('offers to clear filters when nothing matches', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serveIssues()
    const { router } = await renderApp('/issues?priority=High&overdue=true')

    expect(await screen.findByText('No issues match these filters')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Clear filters' }))
    await waitFor(() => expect(router.state.location.search).toEqual({}))
    expect(await screen.findByText('No open issues')).toBeInTheDocument()
    expect(api.last().get('Priority')).toBeNull()
  })
})

describe('paging', () => {
  it('loads more issues and appends them', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serveIssues((params) =>
      params.get('Page') === '1'
        ? HttpResponse.json(
            page([issueListItem({ title: 'First page issue' })], {
              totalCount: 2,
              hasNextPage: true,
              totalPages: 2,
            }),
          )
        : HttpResponse.json(
            page([issueListItem({ title: 'Second page issue' })], {
              page: 2,
              totalCount: 2,
            }),
          ),
    )
    await renderApp('/issues')

    expect(await screen.findByText('First page issue')).toBeInTheDocument()
    expect(screen.getByText('Showing 1 of 2')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Load more' }))

    expect(await screen.findByText('Second page issue')).toBeInTheDocument()
    expect(screen.getByText('First page issue')).toBeInTheDocument()
    expect(api.last().get('Page')).toBe('2')
    expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument()
  })
})

describe('navigation badge', () => {
  it('shows the open count, red when something is overdue', async () => {
    signIn()
    serveIssues()
    await renderApp('/issues')

    const badges = await screen.findAllByTestId('issues-nav-badge')
    expect(badges[0]).toHaveTextContent('7')
    expect(badges[0]).toHaveAccessibleName('7 open issues, 3 overdue')
    expect(badges[0]).toHaveClass('bg-destructive')
  })

  it('shows no badge when nothing is open', async () => {
    signIn()
    serveIssues(undefined, { ...SUMMARY, byStatus: { Open: 0, InProgress: 0 }, overdue: 0 })
    await renderApp('/issues')
    await screen.findByText('No open issues')
    expect(screen.queryByTestId('issues-nav-badge')).not.toBeInTheDocument()
  })
})
