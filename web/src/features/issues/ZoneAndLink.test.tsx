import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import {
  ISSUE_SITE_ID,
  issueDetail,
  issueEvent,
  issueListItem,
  problem,
  sessionResponse,
  taskDetail,
} from '@/test/fixtures'
import { renderApp } from '@/test/renderApp'
import { API, server } from '@/test/server'

const REFRESH = `${API}/api/v1/auth/refresh`
const SITE_2 = '55555555-0000-0000-0000-000000000002'
const ISSUE = issueDetail()
const TASK = taskDetail({ siteId: SITE_2, title: 'Closing checks' })

function signIn(permissions = ['issues.view', 'issues.raise', 'issues.manage', 'sites.view']) {
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
    http.get(`${API}/api/v1/issues/summary`, () =>
      HttpResponse.json({ byStatus: {}, overdue: 0, activeByPriority: {}, assignedToMe: 0 }),
    ),
    http.get(`${API}/api/v1/issues/assignee-candidates`, () => HttpResponse.json([])),
  )
}

function serveList(items: ReturnType<typeof issueListItem>[]) {
  server.use(
    http.get(`${API}/api/v1/issues`, () =>
      HttpResponse.json({
        items,
        page: 1,
        pageSize: 20,
        totalCount: items.length,
        hasNextPage: false,
      }),
    ),
  )
}

function serveIssue(issue = ISSUE) {
  const calls: unknown[] = []
  server.use(
    http.get(`${API}/api/v1/issues/${issue.id}`, () => HttpResponse.json(issue)),
    http.put(`${API}/api/v1/issues/${issue.id}/due`, async ({ request }) => {
      calls.push(await request.json())
      return HttpResponse.json(issue)
    }),
  )
  return calls
}

describe('issue times in the site time zone', () => {
  it('shows list due times on the wall clock of each issue site', async () => {
    signIn(['issues.view'])
    serveList([
      issueListItem({ timeZone: 'Asia/Tokyo', dueAt: '2099-11-08T00:30:00Z' }),
      issueListItem({ timeZone: 'America/Los_Angeles', dueAt: '2099-11-08T00:30:00Z' }),
    ])
    await renderApp('/issues')

    const [tokyo, la] = await screen.findAllByTestId('due-indicator')
    expect(tokyo).toHaveTextContent(/9:30/)
    expect(la).toHaveTextContent(/16:30|4:30/)
  })

  it('shows the due time, history and escalation time on the detail in the site zone', async () => {
    signIn(['issues.view'])
    serveIssue(
      issueDetail({
        timeZone: 'Asia/Tokyo',
        dueAt: '2099-11-08T00:30:00Z',
        escalatedAt: '2026-11-08T00:45:00Z',
        timeline: [
          issueEvent({ kind: 'Raised', to: 'Medium', occurredAt: '2026-11-08T00:15:00Z' }),
          issueEvent({
            kind: 'DueChanged',
            to: '2099-11-08T00:30:00Z',
            occurredAt: '2026-11-08T00:20:00Z',
          }),
        ],
      }),
    )
    await renderApp(`/issues/${ISSUE.id}`)

    expect(await screen.findByTestId('due-indicator')).toHaveTextContent(/9:30/)
    expect(screen.getByTestId('escalated-badge')).toHaveTextContent(/9:45/)
    const events = screen.getAllByTestId('timeline-event')
    expect(events[0]).toHaveTextContent(/9:15/)
    expect(events[1]).toHaveTextContent(/set the due time to .*9:30/)
  })

  it('reads the due input in the site zone when changing it', async () => {
    const user = userEvent.setup()
    signIn()
    const calls = serveIssue(
      issueDetail({ timeZone: 'America/New_York', dueAt: '2099-06-01T13:00:00Z' }),
    )
    await renderApp(`/issues/${ISSUE.id}`)

    const due = await screen.findByLabelText(/^Due/)
    // 13:00 UTC is 09:00 on the New York clock in June.
    expect(due).toHaveValue('2099-06-01T09:00')
    fireEvent.change(due, { target: { value: '2099-06-02T10:00' } })
    await user.click(screen.getByRole('button', { name: 'Save due time' }))
    await waitFor(() => expect(calls).toHaveLength(1))
    expect(calls[0]).toEqual({ dueAt: '2099-06-02T14:00:00.000Z' })
  })
})

describe('escalation', () => {
  it('flags escalated issues in the list only', async () => {
    signIn(['issues.view'])
    serveList([
      issueListItem({ title: 'Escalated one', isEscalated: true }),
      issueListItem({ title: 'Calm one' }),
    ])
    await renderApp('/issues')

    const rows = await screen.findAllByTestId('issue-row')
    expect(within(rows[0]!).getByTestId('escalated-badge')).toHaveTextContent('Escalated')
    expect(within(rows[1]!).queryByTestId('escalated-badge')).not.toBeInTheDocument()
  })

  it('shows no badge on the detail of an issue that was not escalated', async () => {
    signIn(['issues.view'])
    serveIssue()
    await renderApp(`/issues/${ISSUE.id}`)
    await screen.findByRole('heading', { name: ISSUE.title })
    expect(screen.queryByTestId('escalated-badge')).not.toBeInTheDocument()
  })
})

describe('raising from a task', () => {
  function serveTask(respond: () => Response = () => HttpResponse.json(TASK)) {
    server.use(http.get(`${API}/api/v1/tasks/${TASK.id}`, respond))
  }

  function serveRaise(respond: () => Response = () => HttpResponse.json(ISSUE, { status: 201 })) {
    const bodies: Record<string, unknown>[] = []
    server.use(
      http.post(`${API}/api/v1/issues`, async ({ request }) => {
        bodies.push((await request.json()) as Record<string, unknown>)
        return respond()
      }),
      http.get(`${API}/api/v1/issues/${ISSUE.id}`, () => HttpResponse.json(ISSUE)),
      http.get(`${API}/api/v1/issues`, () =>
        HttpResponse.json({ items: [], page: 1, pageSize: 20, totalCount: 0 }),
      ),
    )
    return bodies
  }

  it('shows the linked task, locks its site and sends siteTaskId', async () => {
    const user = userEvent.setup()
    signIn()
    serveTask()
    const bodies = serveRaise()
    await renderApp(`/issues/new?siteId=${ISSUE_SITE_ID}&taskId=${TASK.id}`)

    expect(await screen.findByText('Closing checks')).toBeInTheDocument()
    // The task's site wins over the one in the link, and cannot be changed while linked.
    expect(screen.getByText(/Site: Quay Bar/)).toBeInTheDocument()
    expect(screen.queryByLabelText('Site')).not.toBeInTheDocument()

    await user.type(screen.getByLabelText('Title'), 'Fridge too warm')
    await user.click(screen.getByRole('button', { name: 'Raise issue' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toMatchObject({ siteId: SITE_2, siteTaskId: TASK.id })
  })

  it('lets the person remove the link and choose any site', async () => {
    const user = userEvent.setup()
    signIn()
    serveTask()
    const bodies = serveRaise()
    await renderApp(`/issues/new?taskId=${TASK.id}`)

    await user.click(await screen.findByRole('button', { name: 'Remove link' }))
    expect(screen.queryByText('Closing checks')).not.toBeInTheDocument()
    await user.selectOptions(screen.getByLabelText('Site'), 'Harbour Cafe')
    await user.type(screen.getByLabelText('Title'), 'Unlinked')
    await user.click(screen.getByRole('button', { name: 'Raise issue' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toMatchObject({ siteId: ISSUE_SITE_ID, siteTaskId: null })
  })

  it('explains an unknown task and a site mismatch', async () => {
    const user = userEvent.setup()
    signIn()
    serveTask()
    let code = 'issue.unknown_task'
    serveRaise(() =>
      code === 'issue.unknown_task'
        ? HttpResponse.json(problem(404, code, 'server text'), { status: 404 })
        : HttpResponse.json(problem(400, code, 'server text'), { status: 400 }),
    )
    await renderApp(`/issues/new?taskId=${TASK.id}`)

    await user.type(await screen.findByLabelText('Title'), 'Something')
    await user.click(screen.getByRole('button', { name: 'Raise issue' }))
    expect(await screen.findByText(/linked task could not be found/)).toBeInTheDocument()

    code = 'issue.task_site_mismatch'
    await user.click(screen.getByRole('button', { name: 'Raise issue' }))
    expect(await screen.findByText(/belongs to a different site/)).toBeInTheDocument()
  })

  it('carries on without the link when the task cannot be loaded', async () => {
    const user = userEvent.setup()
    signIn()
    serveTask(() => HttpResponse.json(problem(404, 'task.not_found', 'nope'), { status: 404 }))
    const bodies = serveRaise()
    await renderApp(`/issues/new?siteId=${ISSUE_SITE_ID}&taskId=${TASK.id}`)

    expect(await screen.findByText(/could not be loaded, so it was not attached/)).toBeVisible()
    await user.type(screen.getByLabelText('Title'), 'Still raise it')
    await user.click(screen.getByRole('button', { name: 'Raise issue' }))
    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]?.siteTaskId).toBeNull()
  })
})

describe('Raise issue on the task screen', () => {
  function serveTaskScreen(permissions: string[]) {
    signIn(permissions)
    server.use(http.get(`${API}/api/v1/tasks/${TASK.id}`, () => HttpResponse.json(TASK)))
  }

  it('links to the raise form prefilled with the site and task', async () => {
    serveTaskScreen(['tasks.view', 'issues.raise', 'sites.view'])
    await renderApp(`/tasks/${TASK.id}`)

    const link = await screen.findByRole('link', { name: 'Raise issue' })
    expect(link.getAttribute('href')).toContain('/issues/new')
    expect(link.getAttribute('href')).toContain(`siteId=${SITE_2}`)
    expect(link.getAttribute('href')).toContain(`taskId=${TASK.id}`)
  })

  it('is hidden from people who cannot raise issues', async () => {
    serveTaskScreen(['tasks.view', 'issues.view'])
    await renderApp(`/tasks/${TASK.id}`)

    await screen.findByRole('heading', { name: TASK.title })
    expect(screen.queryByRole('link', { name: 'Raise issue' })).not.toBeInTheDocument()
  })
})
