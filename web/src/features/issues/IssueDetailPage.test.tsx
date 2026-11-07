import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import type { components } from '@/api/schema'
import {
  ISSUE_SITE_ID,
  issueDetail,
  issueEvent,
  problem,
  SESSION_MEMBERSHIP_ID,
  sessionResponse,
} from '@/test/fixtures'
import { renderApp } from '@/test/renderApp'
import { API, server } from '@/test/server'

type IssueDetail = components['schemas']['IssueDetailResponse']

const REFRESH = `${API}/api/v1/auth/refresh`
const ISSUE = issueDetail()
const URL_OF = (id = ISSUE.id) => `${API}/api/v1/issues/${id}`
const PHOTO_ID = '77777777-0000-0000-0000-000000000001'
const OTHER_MEMBER = 'aaaaaaaa-0000-0000-0000-000000000009'

const MANAGER = [
  'issues.view',
  'issues.raise',
  'issues.manage',
  'members.view',
  'sites.view',
  'tasks.view',
]
const STAFF = ['issues.view', 'issues.raise', 'sites.view']

function signIn(permissions = MANAGER) {
  server.use(
    http.post(REFRESH, () => HttpResponse.json(sessionResponse({ permissions }))),
    http.get(`${API}/api/v1/issues/summary`, () =>
      HttpResponse.json({ byStatus: {}, overdue: 0, activeByPriority: {}, assignedToMe: 0 }),
    ),
    http.get(`${API}/api/v1/members`, () =>
      HttpResponse.json({
        items: [
          member(SESSION_MEMBERSHIP_ID, 'Sam', 'Chef'),
          member(OTHER_MEMBER, 'Pat', 'Manager'),
        ],
        page: 1,
        pageSize: 100,
        totalCount: 2,
      }),
    ),
  )
}

function member(id: string, firstName: string, lastName: string) {
  return {
    id,
    userId: crypto.randomUUID(),
    email: `${firstName}@example.com`,
    firstName,
    lastName,
    roleId: crypto.randomUUID(),
    roleName: 'Member',
    status: 'Active',
    allSites: true,
    joinedAt: null,
    lastLoginAt: null,
    createdAt: '2026-01-01T00:00:00Z',
  }
}

/** Serves the issue and records every write. `state.issue` can be changed to simulate server-side changes. */
function serveIssue(initial: IssueDetail) {
  const state = {
    issue: initial,
    gets: 0,
    calls: [] as { method: string; path: string; body: unknown }[],
  }
  server.use(
    http.get(URL_OF(initial.id), () => {
      state.gets++
      return HttpResponse.json(state.issue)
    }),
  )
  return state
}

function onWrite(
  state: ReturnType<typeof serveIssue>,
  method: 'post' | 'put',
  path: string,
  respond: (body: unknown) => Response,
) {
  server.use(
    http[method](`${URL_OF(state.issue.id)}/${path}`, async ({ request }) => {
      const text = await request.text()
      const body = text ? JSON.parse(text) : undefined
      state.calls.push({ method, path, body })
      return respond(body)
    }),
  )
}

/** Answers a write with the issue after `change`, which is also what later reads return. */
function respondWith(state: ReturnType<typeof serveIssue>, change: Partial<IssueDetail>) {
  return () => {
    state.issue = { ...state.issue, ...change }
    return HttpResponse.json(state.issue)
  }
}

function serveUploads() {
  server.use(
    http.post(`${API}/api/v1/attachments`, () =>
      HttpResponse.json(
        {
          id: PHOTO_ID,
          kind: 'Photo',
          contentType: 'image/jpeg',
          size: 1,
          fileName: 'shot.jpg',
          siteId: ISSUE_SITE_ID,
        },
        { status: 201 },
      ),
    ),
    http.get(
      `${API}/api/v1/attachments/${PHOTO_ID}/content`,
      () => new HttpResponse(new Blob(['x'], { type: 'image/jpeg' }), { status: 200 }),
    ),
  )
}

const ME = { membershipId: SESSION_MEMBERSHIP_ID, name: 'Sam Chef' }
const SOMEONE = { membershipId: OTHER_MEMBER, name: 'Pat Manager' }

describe('issue detail', () => {
  it('shows the header, description, photos, task link and resolution', async () => {
    signIn()
    serveUploads()
    serveIssue(
      issueDetail({
        description: 'The seal is torn.',
        priority: 'High',
        status: 'Resolved',
        source: 'TaskFailure',
        assignee: SOMEONE,
        dueAt: '2026-11-05T09:00:00Z',
        isOverdue: true,
        departmentName: 'Kitchen',
        siteTaskId: '99999999-0000-0000-0000-000000000001',
        taskTitle: 'Opening checks',
        taskDueDate: '2026-11-07',
        fieldKey: 'fridge_temp',
        photoIds: [PHOTO_ID],
        resolution: 'Replaced the seal.',
        resolvedBy: 'Pat Manager',
      }),
    )
    await renderApp(`/issues/${ISSUE.id}`)

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Fridge door will not seal' }),
    ).toBeInTheDocument()
    expect(screen.getByText('#12')).toBeInTheDocument()
    expect(screen.getByTestId('status-badge')).toHaveTextContent('Resolved')
    expect(screen.getByTestId('priority-badge')).toHaveTextContent('High')
    expect(screen.getByText(/Harbour Cafe · Kitchen/)).toBeInTheDocument()
    expect(screen.getByText('Pat Manager')).toBeInTheDocument()
    expect(screen.getByText('The seal is torn.')).toBeInTheDocument()
    expect(await screen.findByRole('img', { name: 'Issue photo 1' })).toBeInTheDocument()
    expect(screen.getByText('Raised from a task', { selector: '[data-slot=badge]' })).toBeVisible()

    const link = screen.getByRole('link', { name: 'Opening checks' })
    expect(link).toHaveAttribute('href', '/tasks/99999999-0000-0000-0000-000000000001')
    expect(screen.getByText(/field fridge_temp/)).toBeInTheDocument()

    const resolution = screen.getByRole('region', { name: 'Resolution' })
    expect(within(resolution).getByText('Replaced the seal.')).toBeInTheDocument()
  })

  it('flags an overdue active issue', async () => {
    signIn(STAFF)
    serveIssue(issueDetail({ dueAt: '2026-11-05T09:00:00Z', isOverdue: true }))
    await renderApp(`/issues/${ISSUE.id}`)

    expect(await screen.findByTestId('due-indicator')).toHaveTextContent(/^Overdue/)
    expect(screen.getByText('Overdue', { selector: '[data-slot=badge]' })).toBeInTheDocument()
  })

  it('lists the timeline in the order the API gives, describing each event', async () => {
    signIn(STAFF)
    serveUploads()
    serveIssue(
      issueDetail({
        timeline: [
          issueEvent({ kind: 'Raised', to: 'Medium', actorName: 'Sam Chef' }),
          issueEvent({ kind: 'Assigned', to: 'm', toName: 'Pat Manager', actorName: 'Sam Chef' }),
          issueEvent({
            kind: 'PriorityChanged',
            from: 'Medium',
            to: 'High',
            actorName: 'Pat Manager',
          }),
          issueEvent({
            kind: 'StatusChanged',
            from: 'Open',
            to: 'InProgress',
            actorName: 'Pat Manager',
          }),
          issueEvent({
            kind: 'Comment',
            note: 'Engineer booked for Friday.',
            photoIds: [PHOTO_ID],
            actorName: 'Pat Manager',
          }),
          issueEvent({
            kind: 'StatusChanged',
            from: 'InProgress',
            to: 'Resolved',
            note: 'Fitted a new seal.',
            actorName: 'Pat Manager',
          }),
          issueEvent({
            kind: 'StatusChanged',
            from: 'Resolved',
            to: 'Open',
            note: 'Still leaking.',
            actorName: 'Sam Chef',
          }),
        ],
      }),
    )
    await renderApp(`/issues/${ISSUE.id}`)

    const events = await screen.findAllByTestId('timeline-event')
    const text = events.map((e) => e.textContent)
    expect(text[0]).toContain('Sam Chef raised this issue (medium priority)')
    expect(text[1]).toContain('Sam Chef assigned this to Pat Manager')
    expect(text[2]).toContain('Pat Manager changed the priority from Medium to High')
    expect(text[3]).toContain('Pat Manager started work')
    expect(text[4]).toContain('Pat Manager commented')
    expect(text[4]).toContain('Engineer booked for Friday.')
    expect(text[5]).toContain('Pat Manager resolved this issue')
    expect(text[5]).toContain('Corrective action: Fitted a new seal.')
    expect(text[6]).toContain('Sam Chef reopened this issue')
    expect(text[6]).toContain('Reason: Still leaking.')
    expect(await within(events[4]!).findByRole('img', { name: 'Event photo 1' })).toBeVisible()
  })

  it('says so when the issue is not visible', async () => {
    signIn()
    server.use(
      http.get(URL_OF(), () =>
        HttpResponse.json(problem(404, 'issue.not_found', 'Issue not found.'), { status: 404 }),
      ),
    )
    await renderApp(`/issues/${ISSUE.id}`)
    expect(await screen.findByText('This issue does not exist or you cannot see it.')).toBeVisible()
  })

  it('shows an error with a retry for other failures', async () => {
    const user = userEvent.setup()
    signIn()
    let fail = true
    server.use(
      http.get(URL_OF(), () =>
        fail
          ? HttpResponse.json(problem(500, 'x', 'boom'), { status: 500 })
          : HttpResponse.json(ISSUE),
      ),
    )
    await renderApp(`/issues/${ISSUE.id}`)

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong')
    fail = false
    await user.click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByRole('heading', { name: ISSUE.title })).toBeInTheDocument()
  })
})

describe('permission gating', () => {
  it('gives view-only people no actions and no comment box', async () => {
    signIn(['issues.view'])
    serveIssue(issueDetail({ assignee: ME }))
    await renderApp(`/issues/${ISSUE.id}`)

    await screen.findByRole('heading', { name: ISSUE.title })
    expect(screen.queryByRole('region', { name: 'Actions' })).not.toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Manage' })).not.toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'New comment' })).not.toBeInTheDocument()
  })

  it('lets staff comment but not work an issue that is not theirs', async () => {
    signIn(STAFF)
    serveIssue(issueDetail({ assignee: SOMEONE }))
    await renderApp(`/issues/${ISSUE.id}`)

    expect(await screen.findByRole('region', { name: 'New comment' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Start work' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Resolve' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Close issue/ })).not.toBeInTheDocument()
  })

  it('lets the assignee start and resolve but not close, reopen or manage', async () => {
    signIn(STAFF)
    serveIssue(issueDetail({ assignee: ME }))
    await renderApp(`/issues/${ISSUE.id}`)

    expect(await screen.findByRole('button', { name: 'Start work' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Resolve' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Close issue/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reopen' })).not.toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Manage' })).not.toBeInTheDocument()
  })

  it('shows managers the actions that fit each status', async () => {
    signIn()
    const state = serveIssue(issueDetail({ status: 'InProgress', assignee: SOMEONE }))
    const { queryClient } = await renderApp(`/issues/${ISSUE.id}`)

    // Active: resolve and close, no start (not Open) and no reopen.
    expect(await screen.findByRole('button', { name: 'Resolve' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Close issue' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Start work' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reopen' })).not.toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Manage' })).toBeInTheDocument()

    // Resolved: verify and reopen, nothing to change any more.
    state.issue = { ...state.issue, status: 'Resolved' }
    await queryClient.invalidateQueries()
    expect(await screen.findByRole('button', { name: 'Verify and close' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Reopen' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Resolve' })).not.toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Manage' })).not.toBeInTheDocument()

    // Closed: only reopen.
    state.issue = { ...state.issue, status: 'Closed' }
    await queryClient.invalidateQueries()
    await waitFor(() =>
      expect(screen.queryByRole('button', { name: 'Verify and close' })).not.toBeInTheDocument(),
    )
    expect(screen.getByRole('button', { name: 'Reopen' })).toBeInTheDocument()
  })
})

describe('workflow actions', () => {
  it('starts work', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveIssue(issueDetail({ assignee: ME }))
    onWrite(state, 'post', 'start', respondWith(state, { status: 'InProgress' }))
    await renderApp(`/issues/${ISSUE.id}`)

    await user.click(await screen.findByRole('button', { name: 'Start work' }))

    await waitFor(() => expect(screen.getByTestId('status-badge')).toHaveTextContent('In progress'))
    expect(state.calls).toEqual([{ method: 'post', path: 'start', body: undefined }])
    expect(screen.queryByRole('button', { name: 'Start work' })).not.toBeInTheDocument()
  })

  it('requires the corrective action to resolve, then sends it with the photos', async () => {
    const user = userEvent.setup()
    signIn()
    serveUploads()
    const state = serveIssue(issueDetail({ assignee: ME }))
    onWrite(
      state,
      'post',
      'resolve',
      respondWith(state, { status: 'Resolved', resolution: 'Fitted a new seal.' }),
    )
    await renderApp(`/issues/${ISSUE.id}`)

    await user.click(await screen.findByRole('button', { name: 'Resolve' }))
    await user.click(screen.getByRole('button', { name: 'Mark as resolved' }))
    expect(await screen.findByText('Describe the corrective action taken.')).toBeInTheDocument()
    expect(state.calls).toEqual([])

    await user.type(screen.getByLabelText('Corrective action taken'), '  Fitted a new seal. ')
    await user.upload(
      screen.getByLabelText('Resolution photo library'),
      new File(['pixels'], 'shot.jpg', { type: 'image/jpeg' }),
    )
    expect(await screen.findByRole('img', { name: 'Resolution photo 1' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Mark as resolved' }))

    await waitFor(() => expect(screen.getByTestId('status-badge')).toHaveTextContent('Resolved'))
    expect(state.calls).toEqual([
      {
        method: 'post',
        path: 'resolve',
        body: { resolution: 'Fitted a new seal.', photoIds: [PHOTO_ID] },
      },
    ])
    expect(screen.getByRole('region', { name: 'Resolution' })).toHaveTextContent(
      'Fitted a new seal.',
    )
    expect(screen.queryByLabelText('Corrective action taken')).not.toBeInTheDocument()
  })

  it('shows the server message on the resolution field when it is rejected', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveIssue(issueDetail({ assignee: ME }))
    onWrite(state, 'post', 'resolve', () =>
      HttpResponse.json(
        problem(400, 'validation.failed', 'Invalid', {
          errors: { Resolution: ['Say more about the fix.'] },
        }),
        { status: 400 },
      ),
    )
    await renderApp(`/issues/${ISSUE.id}`)

    await user.click(await screen.findByRole('button', { name: 'Resolve' }))
    await user.type(screen.getByLabelText('Corrective action taken'), 'Fixed')
    await user.click(screen.getByRole('button', { name: 'Mark as resolved' }))

    expect(await screen.findByText('Say more about the fix.')).toBeInTheDocument()
    expect(screen.getByLabelText('Corrective action taken')).toHaveAttribute('aria-invalid', 'true')
  })

  it('reloads the issue and explains when someone else got there first (409)', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveIssue(issueDetail({ assignee: ME }))
    onWrite(state, 'post', 'resolve', () => {
      // Meanwhile the issue was resolved elsewhere.
      state.issue = { ...state.issue, status: 'Closed' }
      return HttpResponse.json(
        problem(409, 'issue.not_active', 'The issue is already resolved or closed.'),
        { status: 409 },
      )
    })
    await renderApp(`/issues/${ISSUE.id}`)

    await user.click(await screen.findByRole('button', { name: 'Resolve' }))
    await user.type(screen.getByLabelText('Corrective action taken'), 'Fixed')
    const gets = state.gets
    await user.click(screen.getByRole('button', { name: 'Mark as resolved' }))

    await waitFor(() => expect(state.gets).toBeGreaterThan(gets))
    await waitFor(() => expect(screen.getByTestId('status-badge')).toHaveTextContent('Closed'))
    // The resolve form goes away with the fresh state: a closed issue can only be reopened.
    expect(screen.queryByLabelText('Corrective action taken')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Reopen' })).toBeInTheDocument()
  })

  it('tells a non-assignee the server refused (403) without changing the screen', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveIssue(issueDetail({ assignee: ME }))
    onWrite(state, 'post', 'start', () =>
      HttpResponse.json(
        problem(403, 'issue.not_assignee', 'Only the assignee or an issue manager can do this.'),
        { status: 403 },
      ),
    )
    await renderApp(`/issues/${ISSUE.id}`)

    await user.click(await screen.findByRole('button', { name: 'Start work' }))
    expect(
      await screen.findByText('Only the assignee or an issue manager can do this.'),
    ).toBeInTheDocument()
  })

  it('closes with an optional note', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveIssue(issueDetail({ status: 'Resolved', resolution: 'Done.' }))
    onWrite(state, 'post', 'close', respondWith(state, { status: 'Closed' }))
    await renderApp(`/issues/${ISSUE.id}`)

    await user.click(await screen.findByRole('button', { name: 'Verify and close' }))
    await user.type(screen.getByLabelText('Note (optional)'), 'Checked, all good.')
    await user.click(screen.getByRole('button', { name: 'Close issue' }))

    await waitFor(() => expect(screen.getByTestId('status-badge')).toHaveTextContent('Closed'))
    expect(state.calls).toEqual([
      { method: 'post', path: 'close', body: { note: 'Checked, all good.' } },
    ])
  })

  it('closes without a note', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveIssue(issueDetail())
    onWrite(state, 'post', 'close', respondWith(state, { status: 'Closed' }))
    await renderApp(`/issues/${ISSUE.id}`)

    await user.click(await screen.findByRole('button', { name: 'Close issue' }))
    // The toggle and the panel's submit button share a name; the panel's comes last.
    await user.click(screen.getAllByRole('button', { name: 'Close issue' }).at(-1)!)

    await waitFor(() => expect(state.calls).toHaveLength(1))
    expect(state.calls[0]?.body).toEqual({ note: null })
  })

  it('reopens only with a reason', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveIssue(issueDetail({ status: 'Closed' }))
    onWrite(state, 'post', 'reopen', respondWith(state, { status: 'Open' }))
    await renderApp(`/issues/${ISSUE.id}`)

    await user.click(await screen.findByRole('button', { name: 'Reopen' }))
    await user.click(screen.getByRole('button', { name: 'Reopen issue' }))
    expect(
      await screen.findByText('Give a reason so the team knows what is still wrong.'),
    ).toBeInTheDocument()
    expect(state.calls).toEqual([])

    await user.type(screen.getByLabelText('Why is it being reopened?'), 'Leaking again.')
    await user.click(screen.getByRole('button', { name: 'Reopen issue' }))

    await waitFor(() => expect(screen.getByTestId('status-badge')).toHaveTextContent('Open'))
    expect(state.calls).toEqual([
      { method: 'post', path: 'reopen', body: { reason: 'Leaking again.' } },
    ])
  })

  it('reports a conflict when reopening an issue that is already open', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveIssue(issueDetail({ status: 'Closed' }))
    onWrite(state, 'post', 'reopen', () =>
      HttpResponse.json(
        problem(
          409,
          'issue.not_resolved_or_closed',
          'Only a resolved or closed issue can be reopened.',
        ),
        { status: 409 },
      ),
    )
    await renderApp(`/issues/${ISSUE.id}`)

    await user.click(await screen.findByRole('button', { name: 'Reopen' }))
    await user.type(screen.getByLabelText('Why is it being reopened?'), 'Again')
    await user.click(screen.getByRole('button', { name: 'Reopen issue' }))

    expect(
      await screen.findByText('Only a resolved or closed issue can be reopened.'),
    ).toBeInTheDocument()
  })
})

describe('changing an issue', () => {
  it('assigns from the list of members', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveIssue(issueDetail())
    onWrite(state, 'put', 'assignee', respondWith(state, { assignee: SOMEONE }))
    await renderApp(`/issues/${ISSUE.id}`)

    const select = await screen.findByLabelText('Assignee')
    await waitFor(() =>
      expect(within(select).getByRole('option', { name: 'Pat Manager' })).toBeVisible(),
    )
    await user.selectOptions(select, 'Pat Manager')

    await waitFor(() => expect(select).toHaveValue(OTHER_MEMBER))
    expect(state.calls).toEqual([
      { method: 'put', path: 'assignee', body: { assigneeId: OTHER_MEMBER } },
    ])
  })

  it('unassigns', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveIssue(issueDetail({ assignee: SOMEONE }))
    onWrite(state, 'put', 'assignee', respondWith(state, { assignee: null }))
    await renderApp(`/issues/${ISSUE.id}`)

    const select = await screen.findByLabelText('Assignee')
    await waitFor(() =>
      expect(within(select).getByRole('option', { name: 'Sam Chef' })).toBeVisible(),
    )
    await user.selectOptions(select, 'Unassigned')

    await waitFor(() => expect(state.calls).toHaveLength(1))
    expect(state.calls[0]?.body).toEqual({ assigneeId: null })
  })

  it('falls back to "assign to me" when the caller cannot list members', async () => {
    const user = userEvent.setup()
    signIn(['issues.view', 'issues.raise', 'issues.manage', 'sites.view'])
    const state = serveIssue(issueDetail({ assignee: SOMEONE }))
    onWrite(state, 'put', 'assignee', respondWith(state, { assignee: ME }))
    await renderApp(`/issues/${ISSUE.id}`)

    expect(await screen.findByRole('button', { name: 'Assign to me' })).toBeInTheDocument()
    expect(screen.queryByLabelText('Assignee')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Assign to me' }))

    await waitFor(() => expect(state.calls).toHaveLength(1))
    expect(state.calls[0]?.body).toEqual({ assigneeId: SESSION_MEMBERSHIP_ID })
    await waitFor(() =>
      expect(screen.queryByRole('button', { name: 'Assign to me' })).not.toBeInTheDocument(),
    )
  })

  it('shows the server reason when the assignee is refused', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveIssue(issueDetail())
    onWrite(state, 'put', 'assignee', () =>
      HttpResponse.json(
        problem(
          400,
          'issue.invalid_assignee',
          'The assignee must be an active member who can view issues at this site.',
        ),
        { status: 400 },
      ),
    )
    await renderApp(`/issues/${ISSUE.id}`)

    const select = await screen.findByLabelText('Assignee')
    await waitFor(() =>
      expect(within(select).getByRole('option', { name: 'Pat Manager' })).toBeVisible(),
    )
    await user.selectOptions(select, 'Pat Manager')

    expect(await screen.findByText(/must be an active member/)).toBeInTheDocument()
    expect(select).toHaveValue('')
  })

  it('changes the priority', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveIssue(issueDetail())
    onWrite(state, 'put', 'priority', respondWith(state, { priority: 'Critical' }))
    await renderApp(`/issues/${ISSUE.id}`)

    await user.selectOptions(await screen.findByLabelText('Priority'), 'Critical')

    await waitFor(() => expect(screen.getByTestId('priority-badge')).toHaveTextContent('Critical'))
    expect(state.calls).toEqual([
      { method: 'put', path: 'priority', body: { priority: 'Critical' } },
    ])
  })

  it('changes and clears the due time, refusing a time in the past', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveIssue(issueDetail({ dueAt: '2099-01-01T10:00:00Z' }))
    onWrite(state, 'put', 'due', (body) => {
      const dueAt = (body as { dueAt: string | null }).dueAt
      state.issue = { ...state.issue, dueAt }
      return HttpResponse.json(state.issue)
    })
    await renderApp(`/issues/${ISSUE.id}`)

    const due = await screen.findByLabelText('Due')
    fireEvent.change(due, { target: { value: '2020-01-01T09:00' } })
    await user.click(screen.getByRole('button', { name: 'Save due time' }))
    expect(await screen.findByText('Pick a time in the future.')).toBeInTheDocument()
    expect(state.calls).toEqual([])

    fireEvent.change(due, { target: { value: '2099-06-01T09:00' } })
    await user.click(screen.getByRole('button', { name: 'Save due time' }))
    await waitFor(() => expect(state.calls).toHaveLength(1))
    expect(state.calls[0]?.body).toEqual({ dueAt: new Date('2099-06-01T09:00').toISOString() })

    await user.click(await screen.findByRole('button', { name: 'Clear' }))
    await waitFor(() => expect(state.calls).toHaveLength(2))
    expect(state.calls[1]?.body).toEqual({ dueAt: null })
    await waitFor(() =>
      expect(screen.queryByRole('button', { name: 'Clear' })).not.toBeInTheDocument(),
    )
  })

  it('reloads when the issue became inactive before the change landed (409)', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveIssue(issueDetail())
    onWrite(state, 'put', 'priority', () => {
      state.issue = { ...state.issue, status: 'Resolved' }
      return HttpResponse.json(
        problem(409, 'issue.not_active', 'The issue is already resolved or closed.'),
        { status: 409 },
      )
    })
    await renderApp(`/issues/${ISSUE.id}`)

    await user.selectOptions(await screen.findByLabelText('Priority'), 'High')

    // The manage panel disappears with the fresh state; the status shows what the server holds.
    await waitFor(() => expect(screen.getByTestId('status-badge')).toHaveTextContent('Resolved'))
    expect(screen.queryByRole('region', { name: 'Manage' })).not.toBeInTheDocument()
  })
})

describe('comments', () => {
  it('posts a comment with a photo and clears the box', async () => {
    const user = userEvent.setup()
    signIn(STAFF)
    serveUploads()
    const state = serveIssue(issueDetail())
    onWrite(state, 'post', 'comments', (body) => {
      const { text, photoIds } = body as { text: string; photoIds: string[] }
      state.issue = {
        ...state.issue,
        timeline: [
          ...state.issue.timeline,
          issueEvent({ kind: 'Comment', note: text, photoIds, actorName: 'Sam Chef' }),
        ],
      }
      return HttpResponse.json(state.issue)
    })
    await renderApp(`/issues/${ISSUE.id}`)

    await user.type(await screen.findByLabelText('Add a comment'), '  Parts ordered.  ')
    await user.upload(
      screen.getByLabelText('Comment photo library'),
      new File(['pixels'], 'shot.jpg', { type: 'image/jpeg' }),
    )
    expect(await screen.findByRole('img', { name: 'Comment photo 1' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Post comment' }))

    expect(await screen.findByText('Parts ordered.')).toBeInTheDocument()
    expect(state.calls).toEqual([
      { method: 'post', path: 'comments', body: { text: 'Parts ordered.', photoIds: [PHOTO_ID] } },
    ])
    expect(screen.getByLabelText('Add a comment')).toHaveValue('')
    expect(screen.getAllByTestId('timeline-event')).toHaveLength(2)
  })

  it('does not send an empty comment', async () => {
    const user = userEvent.setup()
    signIn(STAFF)
    const state = serveIssue(issueDetail())
    onWrite(state, 'post', 'comments', () => HttpResponse.json(state.issue))
    await renderApp(`/issues/${ISSUE.id}`)

    await user.click(await screen.findByRole('button', { name: 'Post comment' }))
    expect(await screen.findByText('Write a comment or add a photo.')).toBeInTheDocument()
    expect(state.calls).toEqual([])
  })

  it('allows comments on a closed issue', async () => {
    const user = userEvent.setup()
    signIn(STAFF)
    const state = serveIssue(issueDetail({ status: 'Closed' }))
    onWrite(state, 'post', 'comments', () => HttpResponse.json(state.issue))
    await renderApp(`/issues/${ISSUE.id}`)

    await user.type(await screen.findByLabelText('Add a comment'), 'Late note')
    await user.click(screen.getByRole('button', { name: 'Post comment' }))
    await waitFor(() => expect(state.calls).toHaveLength(1))
  })
})
