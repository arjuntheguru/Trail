import { fireEvent, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import {
  ISSUE_SITE_ID,
  issueDetail,
  issueListItem,
  problem,
  sessionResponse,
} from '@/test/fixtures'
import { renderApp } from '@/test/renderApp'
import { API, server } from '@/test/server'

const REFRESH = `${API}/api/v1/auth/refresh`
const SITE_2 = '55555555-0000-0000-0000-000000000002'
const PHOTO_ID = '77777777-0000-0000-0000-000000000001'
const CREATED = issueDetail({ id: '88888888-0000-0000-0000-0000000000aa', number: 31 })

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
    // The detail page the form lands on, and the list/summary behind it.
    http.get(`${API}/api/v1/issues/${CREATED.id}`, () => HttpResponse.json(CREATED)),
    http.get(`${API}/api/v1/issues/summary`, () =>
      HttpResponse.json({ byStatus: {}, overdue: 0, activeByPriority: {}, assignedToMe: 0 }),
    ),
    http.get(`${API}/api/v1/issues`, () =>
      HttpResponse.json({
        items: [issueListItem()],
        page: 1,
        pageSize: 20,
        totalCount: 1,
        hasNextPage: false,
      }),
    ),
  )
}

function serveRaise(
  respond: (body: unknown) => Response = () => HttpResponse.json(CREATED, { status: 201 }),
) {
  const bodies: Record<string, unknown>[] = []
  server.use(
    http.post(`${API}/api/v1/issues`, async ({ request }) => {
      const body = (await request.json()) as Record<string, unknown>
      bodies.push(body)
      return respond(body)
    }),
  )
  return bodies
}

function serveUploads() {
  const sites: string[] = []
  server.use(
    http.post(`${API}/api/v1/attachments`, async ({ request }) => {
      const text = await request.text()
      sites.push(/name="siteId"\r\n\r\n([^\r]+)/.exec(text)?.[1] ?? '')
      return HttpResponse.json(
        {
          id: PHOTO_ID,
          kind: 'Photo',
          contentType: 'image/jpeg',
          size: 1,
          fileName: 'shot.jpg',
          siteId: ISSUE_SITE_ID,
        },
        { status: 201 },
      )
    }),
    http.get(
      `${API}/api/v1/attachments/${PHOTO_ID}/content`,
      () => new HttpResponse(new Blob(['x'], { type: 'image/jpeg' }), { status: 200 }),
    ),
  )
  return sites
}

describe('raise issue form', () => {
  it('checks the required fields before sending anything', async () => {
    const user = userEvent.setup()
    signIn()
    const bodies = serveRaise()
    await renderApp('/issues/new')

    await user.selectOptions(await screen.findByLabelText('Site'), 'Choose a site…')
    await user.click(screen.getByRole('button', { name: 'Raise issue' }))
    expect(await screen.findByText('Choose a site.')).toBeInTheDocument()
    expect(screen.getByText('Enter a title.')).toBeInTheDocument()
    expect(bodies).toEqual([])
  })

  it('rejects a due time in the past', async () => {
    const user = userEvent.setup()
    signIn()
    const bodies = serveRaise()
    await renderApp('/issues/new')

    await user.selectOptions(await screen.findByLabelText('Site'), 'Harbour Cafe')
    await user.type(screen.getByLabelText('Title'), 'Leak')
    fireEvent.change(screen.getByLabelText(/^Due/), {
      target: { value: '2020-01-01T09:00' },
    })
    await user.click(screen.getByRole('button', { name: 'Raise issue' }))

    expect(await screen.findByText('Pick a time in the future.')).toBeInTheDocument()
    expect(bodies).toEqual([])
  })

  it('sends the issue with its photo and opens the new issue', async () => {
    const user = userEvent.setup()
    signIn()
    const bodies = serveRaise()
    const uploadSites = serveUploads()
    const { router } = await renderApp('/issues/new')

    await user.selectOptions(await screen.findByLabelText('Site'), 'Quay Bar')
    await user.type(screen.getByLabelText('Title'), '  Leaking tap  ')
    await user.type(screen.getByLabelText('Description (optional)'), 'Dripping since lunch.')
    await user.selectOptions(screen.getByLabelText('Priority'), 'High')
    fireEvent.change(screen.getByLabelText(/^Due/), {
      target: { value: '2099-03-04T09:30' },
    })
    await user.upload(
      screen.getByLabelText('Issue photo library'),
      new File(['pixels'], 'shot.jpg', { type: 'image/jpeg' }),
    )
    expect(await screen.findByRole('img', { name: 'Issue photo 1' })).toBeInTheDocument()
    expect(uploadSites).toEqual([SITE_2])

    await user.click(screen.getByRole('button', { name: 'Raise issue' }))

    await waitFor(() => expect(router.state.location.pathname).toBe(`/issues/${CREATED.id}`))
    expect(bodies).toEqual([
      {
        title: 'Leaking tap',
        description: 'Dripping since lunch.',
        siteId: SITE_2,
        departmentId: null,
        priority: 'High',
        // March is GMT in London, so the site's wall clock equals UTC.
        dueAt: '2099-03-04T09:30:00.000Z',
        siteTaskId: null,
        assigneeId: null,
        photoIds: [PHOTO_ID],
      },
    ])
    expect(await screen.findByRole('heading', { name: CREATED.title })).toBeInTheDocument()
  })

  it('sends null for the blank optional fields and defaults to medium priority', async () => {
    const user = userEvent.setup()
    signIn()
    const bodies = serveRaise()
    await renderApp(`/issues/new?siteId=${ISSUE_SITE_ID}`)

    expect(await screen.findByLabelText('Site')).toHaveValue(ISSUE_SITE_ID)
    await user.type(screen.getByLabelText('Title'), 'Broken light')
    await user.click(screen.getByRole('button', { name: 'Raise issue' }))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toMatchObject({
      description: null,
      priority: 'Medium',
      dueAt: null,
      photoIds: [],
    })
  })

  it('drops photos when the site changes, since they belong to one site', async () => {
    const user = userEvent.setup()
    signIn()
    serveUploads()
    await renderApp(`/issues/new?siteId=${ISSUE_SITE_ID}`)

    await user.upload(
      await screen.findByLabelText('Issue photo library'),
      new File(['pixels'], 'shot.jpg', { type: 'image/jpeg' }),
    )
    expect(await screen.findByRole('img', { name: 'Issue photo 1' })).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Site'), 'Quay Bar')
    expect(screen.queryByRole('img', { name: 'Issue photo 1' })).not.toBeInTheDocument()
  })

  it('shows server validation errors on the matching fields', async () => {
    const user = userEvent.setup()
    signIn()
    serveRaise(() =>
      HttpResponse.json(
        problem(400, 'validation.failed', 'Invalid', { errors: { Title: ['Title is too odd.'] } }),
        { status: 400 },
      ),
    )
    const { router } = await renderApp(`/issues/new?siteId=${ISSUE_SITE_ID}`)

    await user.type(await screen.findByLabelText('Title'), 'Odd')
    await user.click(screen.getByRole('button', { name: 'Raise issue' }))

    expect(await screen.findByText('Title is too odd.')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/issues/new')
  })

  it('maps a due-in-past rejection to the due field and other failures to a banner', async () => {
    const user = userEvent.setup()
    signIn()
    let code = 'issue.due_in_past'
    serveRaise(() => HttpResponse.json(problem(400, code, `Rejected: ${code}`), { status: 400 }))
    await renderApp(`/issues/new?siteId=${ISSUE_SITE_ID}`)

    await user.type(await screen.findByLabelText('Title'), 'Something')
    await user.click(screen.getByRole('button', { name: 'Raise issue' }))
    expect(await screen.findByText('Rejected: issue.due_in_past')).toBeInTheDocument()

    code = 'issue.unknown_attachments'
    await user.click(screen.getByRole('button', { name: 'Raise issue' }))
    expect(await screen.findByText('Rejected: issue.unknown_attachments')).toBeInTheDocument()
  })

  it('is closed to people who cannot raise issues', async () => {
    signIn(['issues.view'])
    await renderApp('/issues/new')
    expect(
      await screen.findByText('You do not have permission to raise issues.'),
    ).toBeInTheDocument()
    expect(screen.queryByLabelText('Title')).not.toBeInTheDocument()
  })
})
