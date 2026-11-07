import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { problem, sessionResponse } from '@/test/fixtures'
import {
  activityItem,
  complianceReport,
  complianceRow,
  dashboardSite,
  dashboardTotals,
  REPORT_SITE_1,
  REPORT_SITE_2,
} from '@/test/reportFixtures'
import { renderApp } from '@/test/renderApp'
import { API, server } from '@/test/server'

const REFRESH = `${API}/api/v1/auth/refresh`
const ALL = [
  'reports.view',
  'reports.export',
  'audit.view',
  'sites.view',
  'tasks.view',
  'issues.view',
]

beforeEach(() => {
  // Only Date is faked, so timers and MSW keep working.
  vi.setSystemTime(new Date('2026-11-07T10:00:00'))
})
afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})

function signIn(permissions = ALL) {
  server.use(
    http.post(REFRESH, () => HttpResponse.json(sessionResponse({ permissions }))),
    http.get(`${API}/api/v1/sites`, () =>
      HttpResponse.json({
        items: [
          { id: REPORT_SITE_1, name: 'Harbour Cafe', timeZone: 'Europe/London', isActive: true },
          { id: REPORT_SITE_2, name: 'Quay Bar', timeZone: 'Europe/London', isActive: true },
        ],
        page: 1,
        pageSize: 100,
        totalCount: 2,
      }),
    ),
  )
}

/** Serves a GET path and records each request's query string. */
function serve(path: string, respond: (params: URLSearchParams) => Response) {
  const requests: URLSearchParams[] = []
  server.use(
    http.get(`${API}/api/v1/reports/${path}`, ({ request }) => {
      const params = new URL(request.url).searchParams
      requests.push(params)
      return respond(params)
    }),
  )
  return { requests, last: () => requests.at(-1)! }
}

const hrefParams = (el: HTMLElement) => {
  const url = new URL(el.getAttribute('href')!, 'http://x')
  return { path: url.pathname, params: url.searchParams }
}

const SITES = [
  dashboardSite({
    siteName: 'Harbour Cafe',
    score: 96,
    completed: 8,
    dueToday: 10,
    openIssues: 2,
  }),
  dashboardSite({
    siteId: REPORT_SITE_2,
    siteName: 'Quay Bar',
    score: 72.5,
    completed: 3,
    dueToday: 8,
    overdue: 4,
    missedYesterday: 2,
    openIssues: 5,
    overdueIssues: 1,
  }),
  dashboardSite({
    siteId: '55555555-0000-0000-0000-000000000003',
    siteName: 'Dormant Deli',
    score: null,
    completed: 0,
    dueToday: 0,
  }),
]

function serveDashboard() {
  return serve('dashboard', () =>
    HttpResponse.json({
      sites: SITES,
      totals: dashboardTotals({
        score: 84.4,
        completed: 11,
        dueToday: 18,
        remaining: 7,
        overdue: 4,
        missedYesterday: 2,
        openIssues: 7,
        overdueIssues: 1,
        openByPriority: { High: 3, Critical: 1, Low: 3 },
      }),
    }),
  )
}

describe('reports navigation and access', () => {
  it('shows a no-access message without reports.view', async () => {
    signIn(['tasks.view'])
    await renderApp('/reports')
    expect(await screen.findByText(/do not have access to reports/i)).toBeInTheDocument()
    expect(screen.queryByRole('navigation', { name: 'Reports sections' })).not.toBeInTheDocument()
  })

  it('switches between the tabs and keeps the site filter', async () => {
    const user = userEvent.setup()
    signIn()
    serveDashboard()
    serve('compliance', () => HttpResponse.json(complianceReport([complianceRow()])))
    serve('activity', () => HttpResponse.json({ items: [], nextBefore: null }))
    const { router } = await renderApp(`/reports?siteId=${REPORT_SITE_2}`)

    const tabs = await screen.findByRole('navigation', { name: 'Reports sections' })
    expect(within(tabs).getByRole('link', { name: 'Overview' })).toHaveAttribute(
      'aria-current',
      'page',
    )
    await user.click(within(tabs).getByRole('link', { name: 'Compliance' }))
    await screen.findByTestId('compliance-row')
    expect(router.state.location.pathname).toBe('/reports/compliance')
    expect(router.state.location.search).toMatchObject({ siteId: REPORT_SITE_2 })

    await user.click(within(tabs).getByRole('link', { name: 'Activity' }))
    await screen.findByText('No activity yet')
    expect(router.state.location.pathname).toBe('/reports/activity')
  })
})

describe('dashboard', () => {
  it('shows the KPI tiles and one row per site', async () => {
    signIn()
    serveDashboard()
    await renderApp('/reports')

    const tiles = await screen.findAllByTestId('kpi-tile')
    const text = tiles.map((t) => t.textContent)
    expect(text.find((t) => t?.startsWith('On-time score'))).toContain('84.4%')
    expect(text.find((t) => t?.startsWith('Completed today'))).toContain('11 / 18')
    expect(text.find((t) => t?.startsWith('Completed today'))).toContain('7 remaining')
    expect(text.find((t) => t?.startsWith('Overdue tasks'))).toContain('4')
    expect(text.find((t) => t?.startsWith('Missed yesterday'))).toContain('2')
    const open = tiles.find((t) => t.textContent?.startsWith('Open issues'))!
    expect(open).toHaveTextContent('7')
    expect(within(open).getByText('Critical').parentElement).toHaveTextContent('Critical1')

    const rows = screen.getAllByTestId('site-row')
    expect(rows).toHaveLength(3)
    const quay = within(rows[2]!)
    expect(quay.getByText('Quay Bar')).toBeInTheDocument()
    expect(quay.getByTestId('score')).toHaveTextContent('72.5%')
    expect(quay.getByTestId('score')).toHaveAttribute('data-tone', 'bad')
    expect(within(rows[1]!).getByTestId('score')).toHaveAttribute('data-tone', 'good')
    expect(within(rows[0]!).getByTestId('score')).toHaveAttribute('data-tone', 'none')
  })

  it('colours scores: 95 and above good, 80 to 95 warning, below 80 bad', async () => {
    const { scoreTone } = await import('./scoreTone')
    expect([100, 95, 94.9, 80, 79.9, null].map(scoreTone)).toEqual([
      'good',
      'good',
      'warn',
      'warn',
      'bad',
      'none',
    ])
  })

  it('sorts the site table by a column and flips on a second click, empty scores last', async () => {
    const user = userEvent.setup()
    signIn()
    serveDashboard()
    await renderApp('/reports')
    await screen.findAllByTestId('site-row')
    const names = () =>
      screen.getAllByTestId('site-row').map((r) => within(r).getAllByRole('link')[0]!.textContent)

    // Starts alphabetical.
    expect(names()).toEqual(['Dormant Deli', 'Harbour Cafe', 'Quay Bar'])

    await user.click(screen.getByRole('button', { name: /On-time score/ }))
    expect(names()).toEqual(['Harbour Cafe', 'Quay Bar', 'Dormant Deli'])
    expect(screen.getByRole('columnheader', { name: /On-time score/ })).toHaveAttribute(
      'aria-sort',
      'descending',
    )

    await user.click(screen.getByRole('button', { name: /On-time score/ }))
    expect(names()).toEqual(['Quay Bar', 'Harbour Cafe', 'Dormant Deli'])

    await user.click(screen.getByRole('button', { name: /Overdue tasks/ }))
    expect(names()[0]).toBe('Quay Bar')
  })

  it('filters by site, putting it in the URL and the request', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serveDashboard()
    const { router } = await renderApp('/reports')
    await screen.findAllByTestId('site-row')
    expect(api.requests[0]?.get('SiteId')).toBeNull()

    await user.selectOptions(screen.getByLabelText('Site'), REPORT_SITE_2)

    await waitFor(() => expect(api.last().get('SiteId')).toBe(REPORT_SITE_2))
    expect(router.state.location.search).toMatchObject({ siteId: REPORT_SITE_2 })
  })

  it('links a site to its compliance report and its open issues', async () => {
    signIn()
    serveDashboard()
    await renderApp('/reports')
    const row = (await screen.findAllByTestId('site-row'))[2]!
    const links = within(row).getAllByRole('link')
    expect(hrefParams(links[0]!).path).toBe('/reports/compliance')
    expect(hrefParams(links[0]!).params.get('siteId')).toBe(REPORT_SITE_2)
    expect(hrefParams(links[1]!).path).toBe('/issues')
    expect(hrefParams(links[1]!).params.get('siteId')).toBe(REPORT_SITE_2)
  })

  it('shows skeletons, an error with retry and an empty state', async () => {
    const user = userEvent.setup()
    signIn()
    let mode: 'hang' | 'fail' | 'empty' = 'hang'
    serve('dashboard', () => {
      if (mode === 'hang') return new Promise<Response>(() => {}) as unknown as Response
      if (mode === 'fail') return HttpResponse.json(problem(500, 'x', 'boom'), { status: 500 })
      return HttpResponse.json({ sites: [], totals: dashboardTotals() })
    })
    const view = await renderApp('/reports')
    expect(await screen.findByRole('status', { name: 'Loading dashboard' })).toBeInTheDocument()

    mode = 'fail'
    await view.queryClient.resetQueries()
    expect(await screen.findByRole('alert')).toHaveTextContent(/went wrong/i)

    mode = 'empty'
    await user.click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByText('No active sites')).toBeInTheDocument()
  })
})

describe('compliance report', () => {
  const rows = [
    complianceRow({ key: REPORT_SITE_1, name: 'Harbour Cafe', due: 20, missed: 3, score: 85 }),
    complianceRow({ key: REPORT_SITE_2, name: 'Quay Bar', due: 10, missed: 0, score: 70 }),
  ]

  it('asks for the last 30 days grouped by site and shows rows with a total', async () => {
    signIn()
    const api = serve('compliance', () => HttpResponse.json(complianceReport(rows)))
    await renderApp('/reports/compliance')

    expect(await screen.findAllByTestId('compliance-row')).toHaveLength(2)
    expect(api.requests[0]?.get('From')).toBe('2026-10-09')
    expect(api.requests[0]?.get('To')).toBe('2026-11-07')
    expect(api.requests[0]?.get('GroupBy')).toBe('Site')
    expect(screen.getByTestId('compliance-total')).toHaveTextContent('Total')
    expect(
      within(screen.getAllByTestId('compliance-row')[1]!).getByTestId('score'),
    ).toHaveAttribute('data-tone', 'bad')
  })

  it('sends the chosen range, site and grouping, and keeps them in the URL', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serve('compliance', () => HttpResponse.json(complianceReport(rows)))
    const { router } = await renderApp('/reports/compliance')
    await screen.findAllByTestId('compliance-row')

    await user.selectOptions(screen.getByLabelText('Date range'), '7d')
    await waitFor(() => expect(api.last().get('From')).toBe('2026-11-01'))
    expect(router.state.location.search).toMatchObject({ range: '7d' })

    await user.selectOptions(screen.getByLabelText('Site'), REPORT_SITE_2)
    await waitFor(() => expect(api.last().get('SiteId')).toBe(REPORT_SITE_2))

    await user.selectOptions(screen.getByLabelText('Group by'), 'Day')
    await waitFor(() => expect(api.last().get('GroupBy')).toBe('Day'))
    expect(router.state.location.search).toMatchObject({
      range: '7d',
      siteId: REPORT_SITE_2,
      groupBy: 'Day',
    })
  })

  it('accepts a custom range from the URL and rejects one over 93 days without calling the API', async () => {
    signIn()
    const api = serve('compliance', () => HttpResponse.json(complianceReport(rows)))
    await renderApp('/reports/compliance?range=custom&from=2026-01-01&to=2026-06-01')
    expect(await screen.findByRole('alert')).toHaveTextContent(/at most 93 days/)
    expect(api.requests).toHaveLength(0)
  })

  it("shows the API's message when it refuses the range", async () => {
    signIn()
    serve('compliance', () =>
      HttpResponse.json(
        problem(400, 'report.invalid_range', 'The date range is limited to 93 days.'),
        {
          status: 400,
        },
      ),
    )
    await renderApp('/reports/compliance')
    expect(await screen.findByRole('alert')).toHaveTextContent('limited to 93 days')
  })

  it('drills down from a site row to its tasks and issues', async () => {
    signIn()
    serve('compliance', () => HttpResponse.json(complianceReport(rows)))
    await renderApp('/reports/compliance')
    const row = (await screen.findAllByTestId('compliance-row'))[0]!

    const due = hrefParams(within(row).getByRole('link', { name: '20' }))
    expect(due.path).toBe('/tasks')
    expect(Object.fromEntries(due.params)).toEqual({
      range: 'custom',
      from: '2026-10-09',
      to: '2026-11-07',
      siteId: REPORT_SITE_1,
    })

    const missed = hrefParams(within(row).getByRole('link', { name: '3' }))
    expect(missed.params.get('status')).toBe('Missed')
    expect(missed.params.get('siteId')).toBe(REPORT_SITE_1)

    const issues = hrefParams(within(row).getByRole('link', { name: 'Issues at Harbour Cafe' }))
    expect(issues.path).toBe('/issues')
    expect(issues.params.get('siteId')).toBe(REPORT_SITE_1)
  })

  it('drills down from a day row to that single day', async () => {
    signIn()
    serve('compliance', () =>
      HttpResponse.json(
        complianceReport([complianceRow({ key: '2026-11-05', name: '2026-11-05', due: 4 })], {
          groupBy: 'Day',
        }),
      ),
    )
    await renderApp(`/reports/compliance?groupBy=Day&siteId=${REPORT_SITE_2}`)
    const row = await screen.findByTestId('compliance-row')
    const { params } = hrefParams(within(row).getByRole('link', { name: '4' }))
    expect(params.get('from')).toBe('2026-11-05')
    expect(params.get('to')).toBe('2026-11-05')
    expect(params.get('siteId')).toBe(REPORT_SITE_2)
    expect(within(row).queryByRole('link', { name: /Issues at/ })).not.toBeInTheDocument()
  })

  it('leaves out drill-down links the user cannot follow', async () => {
    signIn(['reports.view'])
    serve('compliance', () => HttpResponse.json(complianceReport(rows)))
    await renderApp('/reports/compliance')
    const row = (await screen.findAllByTestId('compliance-row'))[0]!
    expect(within(row).queryByRole('link')).not.toBeInTheDocument()
  })

  it('shows an empty state when nothing was due', async () => {
    signIn()
    serve('compliance', () => HttpResponse.json(complianceReport([])))
    await renderApp('/reports/compliance')
    expect(await screen.findByText('Nothing was due in this period')).toBeInTheDocument()
  })
})

describe('activity feed', () => {
  it('lists items linking to the task or issue and pages with the cursor', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serve('activity', (params) =>
      params.get('Before')
        ? HttpResponse.json({
            items: [
              activityItem({
                title: 'Closing checks',
                subjectId: 'aaaa0000-0000-0000-0000-000000000002',
              }),
            ],
            nextBefore: null,
          })
        : HttpResponse.json({
            items: [
              activityItem(),
              activityItem({
                kind: 'issue_raised',
                title: 'Broken tap',
                issueNumber: 7,
                subjectId: 'bbbb0000-0000-0000-0000-000000000001',
                actorName: null,
              }),
            ],
            nextBefore: '2026-11-07T08:00:00Z',
          }),
    )
    await renderApp('/reports/activity')

    const items = await screen.findAllByTestId('activity-item')
    expect(items).toHaveLength(2)
    expect(api.requests[0]?.get('Limit')).toBe('50')
    expect(api.requests[0]?.get('Before')).toBeNull()
    expect(items[0]).toHaveTextContent('Sam Chef completed Opening checks')
    expect(items[0]).toHaveTextContent('Harbour Cafe')
    expect(within(items[0]!).getByRole('link', { name: 'Opening checks' })).toHaveAttribute(
      'href',
      '/tasks/99999999-0000-0000-0000-000000000001',
    )
    expect(items[1]).toHaveTextContent('Someone raised #7 Broken tap')
    expect(within(items[1]!).getByRole('link', { name: '#7 Broken tap' })).toHaveAttribute(
      'href',
      '/issues/bbbb0000-0000-0000-0000-000000000001',
    )

    await user.click(screen.getByRole('button', { name: 'Load more' }))
    await waitFor(() => expect(screen.getAllByTestId('activity-item')).toHaveLength(3))
    expect(api.last().get('Before')).toBe('2026-11-07T08:00:00Z')
    expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument()
  })

  it('filters by site', async () => {
    const user = userEvent.setup()
    signIn()
    const api = serve('activity', () => HttpResponse.json({ items: [], nextBefore: null }))
    const { router } = await renderApp('/reports/activity')
    await screen.findByText('No activity yet')

    await user.selectOptions(screen.getByLabelText('Site'), REPORT_SITE_1)
    await waitFor(() => expect(api.last().get('SiteId')).toBe(REPORT_SITE_1))
    expect(router.state.location.search).toMatchObject({ siteId: REPORT_SITE_1 })
  })

  it('shows an error with a retry', async () => {
    signIn()
    serve('activity', () => HttpResponse.json(problem(500, 'x', 'boom'), { status: 500 }))
    await renderApp('/reports/activity')
    expect(await screen.findByRole('alert')).toHaveTextContent(/went wrong/i)
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument()
  })
})

describe('csv exports', () => {
  function csv(
    disposition: string | null = 'attachment; filename=tasks-2026-10-09-to-2026-11-07.csv',
  ) {
    const headers: Record<string, string> = { 'Content-Type': 'text/csv; charset=utf-8' }
    if (disposition) headers['Content-Disposition'] = disposition
    return new HttpResponse('Site,Title\nHarbour Cafe,Opening\n', { headers })
  }

  function spyDownload() {
    const created: Blob[] = []
    const revoked: string[] = []
    const clicks: { href: string; download: string }[] = []
    vi.spyOn(URL, 'createObjectURL').mockImplementation((blob) => {
      created.push(blob as Blob)
      return `blob:test/${created.length}`
    })
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation((url) => void revoked.push(url))
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      clicks.push({ href: this.href, download: this.download })
    })
    return { created, revoked, clicks }
  }

  async function open(permissions = ALL) {
    signIn(permissions)
    serve('compliance', () => HttpResponse.json(complianceReport([complianceRow()])))
    await renderApp(`/reports/compliance?range=7d&siteId=${REPORT_SITE_1}`)
    await screen.findByTestId('compliance-row')
  }

  it('downloads through the authenticated client with the filters and the served file name', async () => {
    const user = userEvent.setup()
    const spy = spyDownload()
    const api = serve('tasks.csv', () => csv())
    await open()

    await user.click(screen.getByRole('button', { name: 'Tasks CSV' }))

    await waitFor(() => expect(spy.clicks).toHaveLength(1))
    expect(api.last().get('From')).toBe('2026-11-01')
    expect(api.last().get('To')).toBe('2026-11-07')
    expect(api.last().get('SiteId')).toBe(REPORT_SITE_1)
    expect(spy.clicks[0]).toEqual({
      href: 'blob:test/1',
      download: 'tasks-2026-10-09-to-2026-11-07.csv',
    })
    expect(spy.created).toHaveLength(1)
    expect(spy.created[0]).toBeInstanceOf(Blob)
    await waitFor(() => expect(spy.revoked).toEqual(['blob:test/1']))
    expect(await screen.findByText('Saved tasks-2026-10-09-to-2026-11-07.csv.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tasks CSV' })).toBeEnabled()
  })

  it('names the file after the range when the response has no usable header', async () => {
    const user = userEvent.setup()
    const spy = spyDownload()
    serve('issues.csv', () => csv(null))
    await open()

    await user.click(screen.getByRole('button', { name: 'Issues CSV' }))

    await waitFor(() => expect(spy.clicks).toHaveLength(1))
    expect(spy.clicks[0]?.download).toBe('issues-2026-11-01-to-2026-11-07.csv')
  })

  it('reads a quoted or encoded file name', async () => {
    const { filenameFromDisposition } = await import('./download')
    expect(filenameFromDisposition('attachment; filename="a b.csv"')).toBe('a b.csv')
    expect(filenameFromDisposition("attachment; filename=x.csv; filename*=UTF-8''y%20z.csv")).toBe(
      'y z.csv',
    )
    expect(filenameFromDisposition(null)).toBeUndefined()
    expect(filenameFromDisposition('attachment')).toBeUndefined()
  })

  it('disables the buttons while preparing and shows the API message on failure', async () => {
    const user = userEvent.setup()
    const spy = spyDownload()
    let release: (() => void) | undefined
    serve('audit.csv', () => {
      return new Promise<Response>((resolve) => {
        release = () =>
          resolve(
            HttpResponse.json(
              problem(400, 'report.too_many_rows', 'The export would exceed 50,000 rows.'),
              { status: 400 },
            ),
          )
      }) as unknown as Response
    })
    await open()

    await user.click(screen.getByRole('button', { name: 'Audit trail CSV' }))
    expect(await screen.findByRole('button', { name: 'Preparing…' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Tasks CSV' })).toBeDisabled()

    await waitFor(() => expect(release).toBeDefined())
    release!()
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The export would exceed 50,000 rows.',
    )
    expect(screen.getByRole('button', { name: 'Audit trail CSV' })).toBeEnabled()
    expect(spy.created).toHaveLength(0)
    expect(spy.clicks).toHaveLength(0)
  })

  it('hides every export without reports.export', async () => {
    await open(['reports.view', 'audit.view', 'sites.view'])
    expect(screen.queryByRole('button', { name: /CSV/ })).not.toBeInTheDocument()
    expect(screen.queryByText('Export')).not.toBeInTheDocument()
  })

  it('hides the audit export without audit.view', async () => {
    await open(['reports.view', 'reports.export', 'sites.view'])
    expect(screen.getByRole('button', { name: 'Tasks CSV' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Issues CSV' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Audit trail CSV' })).not.toBeInTheDocument()
  })

  it('disables exports while the range is invalid', async () => {
    signIn()
    await renderApp('/reports/compliance?range=custom&from=2026-06-01&to=2026-01-01')
    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tasks CSV' })).toBeDisabled()
  })
})
