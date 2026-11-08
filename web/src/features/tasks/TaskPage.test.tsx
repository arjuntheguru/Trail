import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { components } from '@/api/schema'
import {
  entry,
  field,
  problem,
  SESSION_USER_ID,
  sessionResponse,
  taskDetail,
} from '@/test/fixtures'
import { resetNetwork, setOnline } from '@/test/network'
import { renderApp } from '@/test/renderApp'
import { API, server } from '@/test/server'

type TaskDetail = components['schemas']['TaskDetailResponse']

const REFRESH = `${API}/api/v1/auth/refresh`
const TASK = taskDetail()
const URL_OF = (id = TASK.id) => `${API}/api/v1/tasks/${id}`

const FIELDS = [
  field({ key: 'wash', type: 'Checkbox', label: 'Hands washed' }),
  field({ key: 'temp', type: 'Temperature', label: 'Fridge temp', min: 0, max: 5 }),
  field({ key: 'clean', type: 'YesNo', label: 'Surfaces clean', expectedAnswer: true }),
]

function signIn(permissions = ['tasks.view', 'tasks.complete']) {
  server.use(
    http.post(REFRESH, () => HttpResponse.json(sessionResponse({ permissions }))),
    // Today, where completing a task returns to.
    http.get(`${API}/api/v1/sites`, () =>
      HttpResponse.json({ items: [], page: 1, pageSize: 100, totalCount: 0 }),
    ),
  )
}

/** Serves the task and records every write. `state.task` can be changed to simulate server-side changes. */
function serveTask(initial: TaskDetail) {
  const state = {
    task: initial,
    gets: 0,
    calls: [] as { path: string; body: unknown }[],
    /** Draft saves (PUT progress), kept apart from deliberate actions. */
    saves: [] as unknown[],
  }
  server.use(
    http.put(`${URL_OF(initial.id)}/progress`, async ({ request }) => {
      state.saves.push(await request.json())
      return HttpResponse.json(state.task)
    }),
    http.get(URL_OF(initial.id), () => {
      state.gets++
      return HttpResponse.json(state.task)
    }),
  )
  return state
}

function onWrite(
  state: ReturnType<typeof serveTask>,
  method: 'post' | 'put',
  path: string,
  respond: (body: unknown) => Response,
) {
  server.use(
    http[method](`${URL_OF(state.task.id)}/${path}`, async ({ request }) => {
      const body = await request.json()
      state.calls.push({ path, body })
      return respond(body)
    }),
  )
}

function parseMultipart(body: string) {
  const parts: Record<string, { value: string; filename?: string }> = {}
  const pattern =
    /name="([^"]+)"(?:; filename="([^"]*)")?\r\n(?:[^\r\n]+\r\n)*?\r\n([\s\S]*?)\r\n--/g
  for (const [, name, filename, value] of body.matchAll(pattern)) {
    parts[name!] = { value: value!, filename }
  }
  return parts
}

describe('task screen header', () => {
  it('shows title, site, due time, status and description', async () => {
    signIn()
    serveTask(
      taskDetail({ description: 'Do this before opening.', isOverdue: true, fields: FIELDS }),
    )
    await renderApp(`/tasks/${TASK.id}`)

    expect(await screen.findByRole('heading', { name: 'Opening checks' })).toBeInTheDocument()
    expect(screen.getByText('Harbour Cafe')).toBeInTheDocument()
    expect(screen.getByText(/Due .*\d.*Saturday/)).toBeInTheDocument()
    expect(screen.getByTestId('status-badge')).toHaveTextContent('Overdue')
    expect(screen.getByText('Do this before opening.')).toBeInTheDocument()
  })

  it('links to the task from Today', async () => {
    signIn()
    server.use(
      http.get(`${API}/api/v1/sites`, () =>
        HttpResponse.json({
          items: [
            { id: TASK.siteId, name: 'Harbour Cafe', timeZone: 'Europe/London', isActive: true },
          ],
          page: 1,
          pageSize: 100,
          totalCount: 1,
        }),
      ),
      http.get(`${API}/api/v1/sites/${TASK.siteId}/tasks`, () =>
        HttpResponse.json({
          siteId: TASK.siteId,
          siteName: 'Harbour Cafe',
          date: '2026-11-07',
          timeZone: 'Europe/London',
          items: [{ ...TASK, entryCount: 0, completedBy: null, canCompleteLate: false }],
        }),
      ),
    )
    await renderApp('/today')
    const link = await screen.findByRole('link', { name: /Opening checks/ })
    expect(link).toHaveAttribute('href', `/tasks/${TASK.id}`)
  })

  it('explains tasks that are not open yet and does not offer the form', async () => {
    signIn()
    serveTask(taskDetail({ fields: FIELDS, availableAt: '2099-01-01T00:00:00Z' }))
    await renderApp(`/tasks/${TASK.id}`)
    expect(await screen.findByTestId('workability')).toHaveTextContent(/Not open yet/)
    expect(screen.queryByRole('button', { name: 'Complete task' })).not.toBeInTheDocument()
  })

  it('shows a clear message for a task that cannot be found', async () => {
    signIn()
    server.use(
      http.get(URL_OF(), () =>
        HttpResponse.json(problem(404, 'task.not_found', 'Task not found.'), { status: 404 }),
      ),
    )
    await renderApp(`/tasks/${TASK.id}`)
    expect(await screen.findByText('This task could not be found.')).toBeInTheDocument()
  })
})

describe('field renderer', () => {
  it('renders an input for every field type', async () => {
    signIn()
    serveTask(
      taskDetail({
        fields: [
          field({ key: 'i', type: 'Instruction', label: 'Wear gloves', helpText: 'Blue ones.' }),
          field({ key: 'cb', type: 'Checkbox', label: 'Checked' }),
          field({ key: 'yn', type: 'YesNo', label: 'Is clean', expectedAnswer: false }),
          field({ key: 'n', type: 'Number', label: 'Count', unit: 'pcs' }),
          field({ key: 't', type: 'Temperature', label: 'Probe', min: 63 }),
          field({ key: 'tx', type: 'Text', label: 'Name' }),
          field({ key: 'lt', type: 'LongText', label: 'Details' }),
          field({
            key: 'ss',
            type: 'SingleSelect',
            label: 'Condition',
            options: [
              { value: 'a', label: 'Good', isFailure: false },
              { value: 'b', label: 'Poor', isFailure: true },
            ],
          }),
          field({
            key: 'ms',
            type: 'MultiSelect',
            label: 'Areas',
            options: [
              { value: 'k', label: 'Kitchen', isFailure: false },
              { value: 'b', label: 'Bar', isFailure: false },
            ],
          }),
          field({ key: 'd', type: 'Date', label: 'Use by' }),
          field({ key: 'tm', type: 'Time', label: 'Time taken' }),
          field({ key: 'p', type: 'Photo', label: 'Picture' }),
          field({ key: 'sg', type: 'Signature', label: 'Sign off' }),
        ],
      }),
    )
    await renderApp(`/tasks/${TASK.id}`)
    await screen.findByText('Wear gloves')

    expect(screen.getByText('Blue ones.')).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'Checked' })).toBeInTheDocument()
    const yesNo = screen.getByRole('radiogroup', { name: 'Is clean' })
    expect(within(yesNo).getByRole('radio', { name: 'Yes' })).toBeInTheDocument()
    expect(within(yesNo).getByRole('radio', { name: 'No' })).toBeInTheDocument()
    expect(screen.getByText('Expected answer: No')).toBeInTheDocument()
    expect(screen.getByLabelText('Count')).toHaveAttribute('inputmode', 'decimal')
    expect(screen.getByText('pcs')).toBeInTheDocument()
    expect(screen.getByLabelText('Probe')).toHaveAttribute('inputmode', 'decimal')
    expect(screen.getByText('°C')).toBeInTheDocument()
    expect(screen.getByText('Expected at least 63 °C')).toBeInTheDocument()
    expect(screen.getByLabelText('Name').tagName).toBe('INPUT')
    expect(screen.getByLabelText('Details').tagName).toBe('TEXTAREA')
    expect(
      within(screen.getByRole('radiogroup', { name: 'Condition' })).getAllByRole('radio'),
    ).toHaveLength(2)
    expect(
      within(screen.getByRole('group', { name: 'Areas' })).getAllByRole('checkbox'),
    ).toHaveLength(2)
    expect(screen.getByLabelText('Use by')).toHaveAttribute('type', 'date')
    expect(screen.getByLabelText('Time taken')).toHaveAttribute('type', 'time')
    expect(screen.getByTestId('photo-input-camera')).toHaveAttribute('capture', 'environment')
    expect(screen.getByLabelText('Sign off signature pad')).toBeInTheDocument()
  })

  it('toggles buttons and shows the failure as soon as an answer is out of range', async () => {
    const user = userEvent.setup()
    signIn()
    serveTask(taskDetail({ fields: FIELDS }))
    await renderApp(`/tasks/${TASK.id}`)

    const wash = await screen.findByRole('checkbox', { name: 'Hands washed' })
    expect(wash).toHaveAttribute('aria-checked', 'false')
    await user.click(wash)
    expect(wash).toHaveAttribute('aria-checked', 'true')

    expect(screen.getByText('Expected 0–5 °C')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Fridge temp'), '9')
    expect(await screen.findByText('Out of range: 9 °C, expected 0–5 °C')).toBeInTheDocument()
    // A failing answer asks for the corrective action.
    expect(screen.getByLabelText(/What did you do about it/)).toBeInTheDocument()

    await user.clear(screen.getByLabelText('Fridge temp'))
    await user.type(screen.getByLabelText('Fridge temp'), '4')
    expect(screen.queryByText(/Out of range/)).not.toBeInTheDocument()

    await user.click(screen.getByRole('radio', { name: 'No' }))
    expect(screen.getByText('Answered no, expected yes')).toBeInTheDocument()
  })

  it('flips the sign with the minus button for keypads that have none', async () => {
    const user = userEvent.setup()
    signIn()
    serveTask(taskDetail({ fields: [field({ key: 'f', type: 'Temperature', label: 'Freezer' })] }))
    await renderApp(`/tasks/${TASK.id}`)
    await user.type(await screen.findByLabelText('Freezer'), '18')
    await user.click(screen.getByRole('button', { name: 'Toggle minus sign' }))
    expect(screen.getByLabelText('Freezer')).toHaveValue('-18')
  })
})

describe('checklist completion', () => {
  it('completes with answers, note and returns to Today', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(taskDetail({ fields: FIELDS }))
    onWrite(state, 'post', 'complete', () =>
      HttpResponse.json({ ...state.task, status: 'Completed', hasFailures: false }),
    )
    const { router } = await renderApp(`/tasks/${TASK.id}`)

    await user.click(await screen.findByRole('checkbox', { name: 'Hands washed' }))
    await user.type(screen.getByLabelText('Fridge temp'), '4,5')
    await user.click(screen.getByRole('radio', { name: 'Yes' }))
    await user.type(screen.getByLabelText('Note (optional)'), '  all good ')
    await user.click(screen.getByRole('button', { name: 'Complete task' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/today'))
    expect(state.calls).toEqual([
      {
        path: 'complete',
        body: {
          answers: {
            wash: { value: true, photoIds: [] },
            temp: { value: 4.5, photoIds: [] },
            clean: { value: true, photoIds: [] },
          },
          note: 'all good',
          photoIds: [],
          requestId: expect.any(String),
        },
      },
    ])
  })

  it('blocks submit with required fields missing and focuses the first one', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(taskDetail({ fields: FIELDS }))
    onWrite(state, 'post', 'complete', () => HttpResponse.json(state.task))
    await renderApp(`/tasks/${TASK.id}`)

    await user.click(await screen.findByRole('button', { name: 'Complete task' }))

    expect(await screen.findByText("'Hands washed' is required.")).toBeInTheDocument()
    expect(screen.getByText("'Fridge temp' is required.")).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'Hands washed' })).toHaveFocus()
    expect(state.calls).toEqual([])

    // The message goes away once the person fixes that field.
    await user.click(screen.getByRole('checkbox', { name: 'Hands washed' }))
    expect(screen.queryByText("'Hands washed' is required.")).not.toBeInTheDocument()
  })

  it('requires a comment for a failed answer before completing', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(
      taskDetail({ fields: [field({ key: 'temp', type: 'Temperature', label: 'Probe', max: 5 })] }),
    )
    onWrite(state, 'post', 'complete', () =>
      HttpResponse.json({ ...state.task, status: 'Completed' }),
    )
    const { router } = await renderApp(`/tasks/${TASK.id}`)

    await user.type(await screen.findByLabelText('Probe'), '8')
    await user.click(screen.getByRole('button', { name: 'Complete task' }))
    expect(
      await screen.findByText("'Probe' failed: describe the corrective action taken."),
    ).toBeInTheDocument()
    expect(state.calls).toEqual([])

    await user.type(screen.getByLabelText(/What did you do about it/), 'Moved to another fridge')
    await user.click(screen.getByRole('button', { name: 'Complete task' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/today'))
    expect(state.calls[0]?.body).toMatchObject({
      answers: { temp: { value: 8, comment: 'Moved to another fridge' } },
    })
  })

  it('maps a server validation error onto the field it names', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(taskDetail({ fields: FIELDS }))
    onWrite(state, 'post', 'complete', () =>
      HttpResponse.json(
        problem(400, 'task.invalid_answers', 'Some answers are not valid.', {
          errors: { 'answers.temp': ['The probe was not calibrated.'] },
        }),
        { status: 400 },
      ),
    )
    await renderApp(`/tasks/${TASK.id}`)

    await user.click(await screen.findByRole('checkbox', { name: 'Hands washed' }))
    await user.type(screen.getByLabelText('Fridge temp'), '3')
    await user.click(screen.getByRole('radio', { name: 'Yes' }))
    await user.click(screen.getByRole('button', { name: 'Complete task' }))

    const message = await screen.findByText('The probe was not calibrated.')
    expect(message.closest('[data-field-key]')).toHaveAttribute('data-field-key', 'temp')
    await waitFor(() => expect(screen.getByLabelText('Fridge temp')).toHaveFocus())
    // Still on the task, and the button works again.
    expect(screen.getByRole('button', { name: 'Complete task' })).toBeEnabled()
  })

  it('shows a conflict clearly and reloads the task', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(taskDetail({ fields: FIELDS }))
    onWrite(state, 'post', 'complete', () => {
      state.task = {
        ...state.task,
        status: 'Completed',
        completedAt: '2026-11-07T09:00:00Z',
        completedBy: 'Alex Cook',
        entries: [entry({ submittedAt: '2026-11-07T09:00:00Z', submittedBy: 'Alex Cook' })],
      }
      return HttpResponse.json(problem(409, 'task.not_open', 'The task is not open.'), {
        status: 409,
      })
    })
    await renderApp(`/tasks/${TASK.id}`)

    await user.click(await screen.findByRole('checkbox', { name: 'Hands washed' }))
    await user.type(screen.getByLabelText('Fridge temp'), '3')
    await user.click(screen.getByRole('radio', { name: 'Yes' }))
    await user.click(screen.getByRole('button', { name: 'Complete task' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The task is not open.')
    // The refetch shows it completed by someone else, read-only.
    expect(await screen.findByTestId('summary')).toHaveTextContent('by Alex Cook')
    expect(screen.queryByRole('button', { name: 'Complete task' })).not.toBeInTheDocument()
    expect(state.gets).toBeGreaterThan(1)
  })
})

describe('autosave', () => {
  it('saves the draft about 1.5 s after typing, with the right payload', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(taskDetail({ fields: FIELDS }))
    await renderApp(`/tasks/${TASK.id}`)

    await user.type(await screen.findByLabelText('Fridge temp'), '4')
    expect(state.saves).toEqual([]) // debounced, not on every keystroke
    expect(screen.getByText('Unsaved changes')).toBeInTheDocument()

    await waitFor(() => expect(state.saves).toHaveLength(1), { timeout: 4000 })
    expect(state.saves[0]).toEqual({
      answers: { temp: { value: 4, photoIds: [] } },
      note: '',
      photoIds: [],
    })
    expect(await screen.findByText('Saved')).toBeInTheDocument()
  })

  it('saves straight away when a field loses focus', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(taskDetail({ fields: FIELDS }))
    await renderApp(`/tasks/${TASK.id}`)

    await user.type(await screen.findByLabelText('Fridge temp'), '4')
    await user.click(screen.getByRole('radio', { name: 'No' }))
    // Well before the 1.5 s debounce. (The changed answer may follow with a second save.)
    await waitFor(() => expect(state.saves.length).toBeGreaterThan(0), { timeout: 1000 })
    expect(state.saves[0]).toMatchObject({ answers: { temp: { value: 4 } } })
  })

  it('does not send half-typed numbers in the draft', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(taskDetail({ fields: FIELDS }))
    await renderApp(`/tasks/${TASK.id}`)

    await user.type(await screen.findByLabelText('Fridge temp'), '-')
    await user.tab()
    await user.click(screen.getByRole('checkbox', { name: 'Hands washed' }))
    await user.tab()
    await waitFor(() => expect(state.saves).toHaveLength(1), { timeout: 1000 })
    expect(state.saves[0]).toEqual({
      answers: { wash: { value: true, photoIds: [] } },
      note: '',
      photoIds: [],
    })
  })

  it('resumes from the saved draft', async () => {
    signIn()
    serveTask(
      taskDetail({
        fields: FIELDS,
        entries: [
          entry({
            answers: { temp: { value: 3.5, photoIds: [] }, wash: { value: true, photoIds: [] } },
            note: 'half done',
          }),
        ],
      }),
    )
    await renderApp(`/tasks/${TASK.id}`)
    expect(await screen.findByLabelText('Fridge temp')).toHaveValue('3.5')
    expect(screen.getByRole('checkbox', { name: 'Hands washed' })).toHaveAttribute(
      'aria-checked',
      'true',
    )
    expect(screen.getByLabelText('Note (optional)')).toHaveValue('half done')
  })

  it('does not autosave a task that cannot be worked on', async () => {
    signIn()
    const state = serveTask(taskDetail({ fields: FIELDS, status: 'Completed' }))
    await renderApp(`/tasks/${TASK.id}`)
    await screen.findByTestId('summary')
    await new Promise((resolve) => setTimeout(resolve, 1800))
    expect(state.saves).toEqual([])
    expect(screen.queryByText(/Saving|Saved|Unsaved/)).not.toBeInTheDocument()
  })
})

describe('photos', () => {
  const PHOTO_ID = '77777777-0000-0000-0000-000000000001'

  function serveUploads() {
    const uploads: { kind: unknown; siteId: unknown; hasFile: boolean }[] = []
    server.use(
      http.post(`${API}/api/v1/attachments`, async ({ request }) => {
        // jsdom's File cannot be parsed back by Node's formData(), so read the raw multipart text.
        const parts = parseMultipart(await request.text())
        uploads.push({
          kind: parts.kind?.value,
          siteId: parts.siteId?.value,
          hasFile: !!parts.file?.filename,
        })
        return HttpResponse.json(
          {
            id: PHOTO_ID,
            kind: 'Photo',
            contentType: 'image/jpeg',
            size: 1,
            fileName: 'shot.jpg',
            siteId: TASK.siteId,
          },
          { status: 201 },
        )
      }),
      http.get(
        `${API}/api/v1/attachments/${PHOTO_ID}/content`,
        () => new HttpResponse(new Blob(['x'], { type: 'image/jpeg' }), { status: 200 }),
      ),
    )
    return uploads
  }

  it('requires a task photo, uploads it, and sends its id on completion', async () => {
    const user = userEvent.setup()
    signIn()
    const uploads = serveUploads()
    const state = serveTask(
      taskDetail({
        requiresPhoto: true,
        fields: [field({ key: 'wash', type: 'Checkbox', label: 'Hands washed' })],
      }),
    )
    onWrite(state, 'post', 'complete', () =>
      HttpResponse.json({ ...state.task, status: 'Completed' }),
    )
    const { router } = await renderApp(`/tasks/${TASK.id}`)

    await user.click(await screen.findByRole('checkbox', { name: 'Hands washed' }))
    await user.click(screen.getByRole('button', { name: 'Complete task' }))
    expect(await screen.findByText('Attach at least one photo.')).toBeInTheDocument()
    expect(state.calls).toEqual([])

    const input = screen.getByTestId('photo-input-camera')
    await user.upload(input, new File(['pixels'], 'shot.jpg', { type: 'image/jpeg' }))

    expect(await screen.findByRole('button', { name: 'Remove Task photo 1' })).toBeInTheDocument()
    expect(uploads).toEqual([{ kind: 'Photo', siteId: TASK.siteId, hasFile: true }])
    expect(await screen.findByRole('img', { name: 'Task photo 1' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Complete task' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/today'))
    expect(state.calls[0]?.body).toMatchObject({ photoIds: [PHOTO_ID] })
  })

  it('lets the person remove a photo before submitting', async () => {
    const user = userEvent.setup()
    signIn()
    serveUploads()
    serveTask(taskDetail({ requiresPhoto: true, fields: [] }))
    await renderApp(`/tasks/${TASK.id}`)

    await user.upload(
      await screen.findByTestId('photo-input-camera'),
      new File(['pixels'], 'shot.jpg', { type: 'image/jpeg' }),
    )
    await user.click(await screen.findByRole('button', { name: 'Remove Task photo 1' }))
    expect(screen.queryByRole('button', { name: 'Remove Task photo 1' })).not.toBeInTheDocument()
  })

  it('shows a failed upload with a retry', async () => {
    const user = userEvent.setup()
    signIn()
    server.use(
      http.post(`${API}/api/v1/attachments`, () =>
        HttpResponse.json(problem(400, 'attachment.unsupported_type', 'Upload a JPEG.'), {
          status: 400,
        }),
      ),
    )
    serveTask(taskDetail({ requiresPhoto: true, fields: [] }))
    await renderApp(`/tasks/${TASK.id}`)

    await user.upload(
      await screen.findByTestId('photo-input-camera'),
      new File(['pixels'], 'doc.txt', { type: 'image/jpeg' }),
    )
    expect(await screen.findByText('Upload a JPEG.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Retry upload of doc.txt' })).toBeInTheDocument()
  })

  it('attaches a photo to a failed field that needs evidence', async () => {
    const user = userEvent.setup()
    signIn()
    serveUploads()
    const state = serveTask(
      taskDetail({
        fields: [
          field({
            key: 'temp',
            type: 'Temperature',
            label: 'Probe',
            max: 5,
            photoRequiredOnFailure: true,
            commentRequiredOnFailure: false,
          }),
        ],
      }),
    )
    onWrite(state, 'post', 'complete', () =>
      HttpResponse.json({ ...state.task, status: 'Completed' }),
    )
    await renderApp(`/tasks/${TASK.id}`)

    await user.type(await screen.findByLabelText('Probe'), '9')
    await user.click(screen.getByRole('button', { name: 'Complete task' }))
    expect(await screen.findByText("'Probe' failed: add a photo as evidence.")).toBeInTheDocument()

    await user.upload(
      screen.getByTestId('photo-input-camera'),
      new File(['pixels'], 'shot.jpg', { type: 'image/jpeg' }),
    )
    await screen.findByRole('button', { name: 'Remove Probe photo 1' })
    await user.click(screen.getByRole('button', { name: 'Complete task' }))
    await waitFor(() => expect(state.calls).toHaveLength(1))
    expect(state.calls[0]?.body).toMatchObject({
      answers: { temp: { value: 9, photoIds: [PHOTO_ID] } },
    })
  })
})

describe('record log', () => {
  const LOG_FIELDS = [field({ key: 'temp', type: 'Temperature', label: 'Reading', min: 0, max: 5 })]
  const reading = (n: number, value: number, by = 'Sam Chef') =>
    entry({
      answers: { temp: { value, photoIds: [] } },
      submittedAt: `2026-11-07T0${n}:00:00Z`,
      submittedBy: by,
      hasFailures: value > 5,
      failures:
        value > 5
          ? [
              {
                fieldKey: 'temp',
                label: 'Reading',
                reason: `${value}°C is above the maximum of 5°C.`,
              },
            ]
          : [],
    })

  it('lists readings with who and failures, adds a reading and finishes', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(
      taskDetail({
        kind: 'RecordLog',
        fields: LOG_FIELDS,
        entries: [reading(8, 3), reading(9, 9, 'Alex Cook')],
      }),
    )
    onWrite(state, 'post', 'entries', (body) => {
      const answers = (body as { answers: Record<string, { value: number }> }).answers
      state.task = {
        ...state.task,
        entries: [...state.task.entries, reading(5, answers.temp!.value)],
      }
      return HttpResponse.json(state.task, { status: 201 })
    })
    onWrite(state, 'post', 'complete', () =>
      HttpResponse.json({ ...state.task, status: 'Completed' }),
    )
    const { router } = await renderApp(`/tasks/${TASK.id}`)

    const readings = await screen.findAllByTestId('reading')
    expect(readings).toHaveLength(2)
    // Newest first, with the person and the server's failure reason.
    expect(readings[0]).toHaveTextContent('Alex Cook')
    expect(readings[0]).toHaveTextContent('9°C is above the maximum of 5°C.')
    expect(readings[1]).toHaveTextContent('3 °C')

    await user.type(screen.getByLabelText('Reading'), '4')
    await user.click(screen.getByRole('button', { name: 'Add reading' }))

    await waitFor(() => expect(screen.getAllByTestId('reading')).toHaveLength(3))
    expect(state.calls[0]).toEqual({
      path: 'entries',
      body: {
        answers: { temp: { value: 4, photoIds: [] } },
        note: null,
        photoIds: [],
        requestId: expect.any(String),
      },
    })
    // The form is cleared for the next reading.
    expect(screen.getByLabelText('Reading')).toHaveValue('')

    await user.type(screen.getByLabelText('Closing note (optional)'), 'Done for the day')
    await user.click(screen.getByRole('button', { name: 'Finish' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/today'))
    expect(state.calls[1]).toEqual({
      path: 'complete',
      body: { note: 'Done for the day', requestId: expect.any(String) },
    })
  })

  it('does not let you finish without a reading, nor add an empty one', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(taskDetail({ kind: 'RecordLog', fields: LOG_FIELDS }))
    onWrite(state, 'post', 'entries', () => HttpResponse.json(state.task))
    await renderApp(`/tasks/${TASK.id}`)

    expect(await screen.findByRole('button', { name: 'Finish' })).toBeDisabled()
    expect(screen.getByText('Add at least one reading before finishing.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Add reading' }))
    expect(await screen.findByText("'Reading' is required.")).toBeInTheDocument()
    expect(state.calls).toEqual([])
  })

  it('shows a finished log read-only', async () => {
    signIn()
    serveTask(
      taskDetail({
        kind: 'RecordLog',
        status: 'Completed',
        fields: LOG_FIELDS,
        completedAt: '2026-11-07T10:00:00Z',
        completedBy: 'Sam Chef',
        entries: [reading(8, 3)],
      }),
    )
    await renderApp(`/tasks/${TASK.id}`)
    expect(await screen.findAllByTestId('reading')).toHaveLength(1)
    expect(screen.queryByRole('button', { name: 'Add reading' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Finish' })).not.toBeInTheDocument()
  })
})

describe('read-only completed tasks', () => {
  const completed = (overrides: Partial<TaskDetail> = {}) =>
    taskDetail({
      status: 'Completed',
      fields: FIELDS,
      completedAt: '2026-11-07T09:00:00Z',
      completedById: '12121212-0000-0000-0000-000000000001',
      completedBy: 'Alex Cook',
      completedLate: true,
      hasFailures: true,
      entries: [
        entry({
          answers: {
            wash: { value: true, photoIds: [] },
            temp: { value: 9, photoIds: [], comment: 'Moved stock' },
            clean: { value: true, photoIds: [] },
          },
          failures: [
            { fieldKey: 'temp', label: 'Fridge temp', reason: '9°C is above the maximum of 5°C.' },
          ],
          hasFailures: true,
          note: 'Busy morning',
          submittedAt: '2026-11-07T09:00:00Z',
        }),
      ],
      ...overrides,
    })

  it('shows answers, failures, who and when, approval, with nothing to edit', async () => {
    signIn(['tasks.view', 'tasks.complete'])
    serveTask(
      completed({
        requiresApproval: true,
        approvedAt: '2026-11-07T11:00:00Z',
        approvedBy: 'Manager Mo',
        approvalNote: 'Looks fine',
      }),
    )
    await renderApp(`/tasks/${TASK.id}`)

    const summary = await screen.findByTestId('summary')
    expect(summary).toHaveTextContent('by Alex Cook (late)')
    expect(summary).toHaveTextContent('by Manager Mo')
    expect(summary).toHaveTextContent('Looks fine')
    expect(screen.getByText('9 °C')).toBeInTheDocument()
    expect(screen.getByText('9°C is above the maximum of 5°C.')).toBeInTheDocument()
    expect(screen.getByText('“Moved stock”')).toBeInTheDocument()
    expect(screen.getByText('Busy morning')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Complete task' })).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Fridge temp')).not.toBeInTheDocument()
    expect(screen.getByTestId('status-badge')).toHaveTextContent('Approved')
  })

  describe('approve and reopen', () => {
    const awaiting = (overrides: Partial<TaskDetail> = {}) =>
      completed({ requiresApproval: true, isAwaitingApproval: true, ...overrides })

    it('shows both to people with the approve permission', async () => {
      signIn(['tasks.view', 'tasks.complete', 'tasks.approve'])
      serveTask(awaiting())
      await renderApp(`/tasks/${TASK.id}`)
      expect(await screen.findByRole('button', { name: 'Approve task' })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Reopen task' })).toBeInTheDocument()
    })

    it('hides both without the permission', async () => {
      signIn(['tasks.view', 'tasks.complete'])
      serveTask(awaiting())
      await renderApp(`/tasks/${TASK.id}`)
      await screen.findByTestId('summary')
      expect(screen.queryByRole('button', { name: 'Approve task' })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Reopen task' })).not.toBeInTheDocument()
    })

    it('does not offer approval of your own work, but still reopen', async () => {
      signIn(['tasks.view', 'tasks.complete', 'tasks.approve'])
      serveTask(awaiting({ completedById: SESSION_USER_ID }))
      await renderApp(`/tasks/${TASK.id}`)
      await screen.findByTestId('summary')
      expect(screen.queryByRole('button', { name: 'Approve task' })).not.toBeInTheDocument()
      expect(screen.getByText(/someone else needs to approve/)).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Reopen task' })).toBeInTheDocument()
    })

    it('hides both once the task is approved, and approve when nothing awaits approval', async () => {
      signIn(['tasks.view', 'tasks.complete', 'tasks.approve'])
      serveTask(completed({ approvedAt: '2026-11-07T11:00:00Z', approvedBy: 'Mo' }))
      await renderApp(`/tasks/${TASK.id}`)
      await screen.findByTestId('summary')
      expect(screen.queryByRole('button', { name: 'Approve task' })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Reopen task' })).not.toBeInTheDocument()
    })

    it('approves with an optional note', async () => {
      const user = userEvent.setup()
      signIn(['tasks.view', 'tasks.complete', 'tasks.approve'])
      const state = serveTask(awaiting())
      onWrite(state, 'post', 'approve', () =>
        HttpResponse.json({
          ...state.task,
          isAwaitingApproval: false,
          approvedAt: '2026-11-07T11:00:00Z',
          approvedBy: 'Me',
        }),
      )
      await renderApp(`/tasks/${TASK.id}`)
      await user.type(await screen.findByLabelText('Note (optional)'), 'Fine')
      await user.click(screen.getByRole('button', { name: 'Approve task' }))
      await waitFor(() =>
        expect(state.calls).toEqual([{ path: 'approve', body: { note: 'Fine' } }]),
      )
      expect(await screen.findByTestId('status-badge')).toHaveTextContent('Approved')
    })

    it('needs a reason to reopen, then returns the task to the form', async () => {
      const user = userEvent.setup()
      signIn(['tasks.view', 'tasks.complete', 'tasks.approve'])
      const state = serveTask(awaiting())
      onWrite(state, 'post', 'reopen', () => {
        state.task = {
          ...state.task,
          status: 'Open',
          isAwaitingApproval: false,
          completedAt: null,
          approvalNote: 'Redo the temperatures',
          entries: state.task.entries.map((e) => ({ ...e, submittedAt: null })),
        }
        return HttpResponse.json(state.task)
      })
      await renderApp(`/tasks/${TASK.id}`)

      await user.click(await screen.findByRole('button', { name: 'Reopen task' }))
      expect(await screen.findByText(/Give a reason/)).toBeInTheDocument()
      expect(state.calls).toEqual([])

      await user.type(screen.getByLabelText('Why does it need redoing?'), 'Redo the temperatures')
      await user.click(screen.getByRole('button', { name: 'Reopen task' }))
      await waitFor(() =>
        expect(state.calls).toEqual([
          { path: 'reopen', body: { reason: 'Redo the temperatures' } },
        ]),
      )
      // Editable again, with the draft loaded and the reason shown.
      expect(await screen.findByRole('button', { name: 'Complete task' })).toBeInTheDocument()
      expect(screen.getByText(/Sent back for changes/).parentElement).toHaveTextContent(
        'Redo the temperatures',
      )
      expect(screen.getByLabelText('Fridge temp')).toHaveValue('9')
    })
  })
})

describe('snooze', () => {
  it('offers day choices for an open task and sends the chosen date', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(taskDetail({ fields: FIELDS }))
    onWrite(state, 'post', 'snooze', () =>
      HttpResponse.json({ ...state.task, dueDate: '2026-11-08' }),
    )
    const { router } = await renderApp(`/tasks/${TASK.id}`)

    await user.click(await screen.findByRole('button', { name: 'Snooze' }))
    await user.click(screen.getByRole('button', { name: 'Next week' }))
    await waitFor(() =>
      expect(state.calls).toEqual([{ path: 'snooze', body: { date: '2026-11-14' } }]),
    )
    await waitFor(() => expect(router.state.location.pathname).toBe('/today'))
  })

  it('shows the server message when the date is refused', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(taskDetail({ fields: FIELDS }))
    onWrite(state, 'post', 'snooze', () =>
      HttpResponse.json(
        problem(400, 'task.invalid_snooze_date', 'A task can be snoozed at most 14 days ahead.'),
        { status: 400 },
      ),
    )
    await renderApp(`/tasks/${TASK.id}`)
    await user.click(await screen.findByRole('button', { name: 'Snooze' }))
    await user.click(screen.getByRole('button', { name: 'Tomorrow' }))
    expect(
      await screen.findByText('A task can be snoozed at most 14 days ahead.'),
    ).toBeInTheDocument()
  })

  it('is not offered without the complete permission or for missed tasks', async () => {
    signIn(['tasks.view'])
    serveTask(taskDetail({ fields: FIELDS }))
    await renderApp(`/tasks/${TASK.id}`)
    await screen.findByRole('heading', { name: 'Opening checks' })
    expect(screen.queryByRole('button', { name: 'Snooze' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Complete task' })).not.toBeInTheDocument()
  })
})

describe('working offline', () => {
  const PHOTO_ID = '77777777-0000-0000-0000-000000000009'
  const LOG_FIELDS = [field({ key: 'temp', type: 'Temperature', label: 'Reading', min: 0, max: 5 })]

  afterEach(() => resetNetwork())

  function serveUploads() {
    const uploads: { requestId?: string }[] = []
    server.use(
      http.post(`${API}/api/v1/attachments`, async ({ request }) => {
        const parts = parseMultipart(await request.text())
        uploads.push({ requestId: parts.requestId?.value })
        return HttpResponse.json(
          {
            id: PHOTO_ID,
            kind: 'Photo',
            contentType: 'image/jpeg',
            size: 1,
            fileName: 'shot.jpg',
            siteId: TASK.siteId,
          },
          { status: 201 },
        )
      }),
      http.get(
        `${API}/api/v1/attachments/${PHOTO_ID}/content`,
        () => new HttpResponse(new Blob(['x'], { type: 'image/jpeg' }), { status: 200 }),
      ),
    )
    return uploads
  }

  async function fillChecklist(user: ReturnType<typeof userEvent.setup>) {
    await user.click(await screen.findByRole('checkbox', { name: 'Hands washed' }))
    await user.type(screen.getByLabelText('Fridge temp'), '4')
    await user.click(screen.getByRole('radio', { name: 'Yes' }))
  }

  it('queues a completion, shows it as waiting and sends it with requestId and completedAt when back online', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(taskDetail({ fields: FIELDS }))
    onWrite(state, 'post', 'complete', () =>
      HttpResponse.json({ ...state.task, status: 'Completed' }),
    )
    const { router, outbox } = await renderApp(`/tasks/${TASK.id}`)

    await fillChecklist(user)
    setOnline(false)
    const pressed = Date.now()
    await user.click(screen.getByRole('button', { name: 'Complete task' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/today'))
    expect(state.calls).toEqual([])
    expect(outbox.getSnapshot().pendingCount).toBe(1)
    expect(await screen.findByRole('button', { name: '1 change waiting to sync' })).toBeVisible()

    // Opening the task shows what was entered, read-only, instead of the form.
    router.history.push(`/tasks/${TASK.id}`)
    expect(await screen.findByTestId('queued-completion')).toHaveTextContent(
      /Completed on this device at .*\. Waiting to sync\./,
    )
    expect(screen.queryByRole('button', { name: 'Complete task' })).not.toBeInTheDocument()
    expect(screen.getByText('Hands washed')).toBeInTheDocument()

    setOnline(true)
    await waitFor(() => expect(state.calls).toHaveLength(1))
    const body = state.calls[0]!.body as { requestId: string; completedAt: string }
    expect(body.requestId).toMatch(/^[0-9a-f-]{36}$/)
    expect(Date.parse(body.completedAt)).toBeGreaterThanOrEqual(pressed - 1000)
    expect(Date.parse(body.completedAt)).toBeLessThanOrEqual(Date.now())
    await waitFor(() => expect(outbox.getSnapshot().items).toHaveLength(0))
  })

  it('queues the completion when the request cannot reach the server, keeping the same requestId', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(taskDetail({ fields: FIELDS }))
    const requestIds: string[] = []
    let lost = true
    onWrite(state, 'post', 'complete', (body) => {
      requestIds.push((body as { requestId: string }).requestId)
      return lost
        ? HttpResponse.error()
        : HttpResponse.json({ ...state.task, status: 'Completed' }, { status: 200 })
    })
    const { router, outbox } = await renderApp(`/tasks/${TASK.id}`)

    await fillChecklist(user)
    await user.click(screen.getByRole('button', { name: 'Complete task' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/today'))
    await waitFor(() => expect(outbox.getSnapshot().items).toHaveLength(1))

    lost = false
    await outbox.syncNow()
    expect(requestIds.length).toBeGreaterThanOrEqual(2)
    expect(new Set(requestIds).size).toBe(1)
    expect(outbox.getSnapshot().items).toHaveLength(0)
  })

  it('sends a requestId, and no offline time, when completing online', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(taskDetail({ fields: FIELDS }))
    onWrite(state, 'post', 'complete', () =>
      HttpResponse.json({ ...state.task, status: 'Completed' }),
    )
    const { router, outbox } = await renderApp(`/tasks/${TASK.id}`)

    await fillChecklist(user)
    await user.click(screen.getByRole('button', { name: 'Complete task' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/today'))
    expect(state.calls).toHaveLength(1)
    expect(state.calls[0]!.body).toMatchObject({
      requestId: expect.stringMatching(/^[0-9a-f-]{36}$/),
    })
    expect(state.calls[0]!.body).not.toHaveProperty('completedAt')
    expect(outbox.getSnapshot().items).toHaveLength(0)
  })

  it('queues an offline photo and rewrites its id when the completion syncs', async () => {
    const user = userEvent.setup()
    signIn()
    const uploads = serveUploads()
    const state = serveTask(
      taskDetail({
        requiresPhoto: true,
        fields: [field({ key: 'wash', type: 'Checkbox', label: 'Hands washed' })],
      }),
    )
    onWrite(state, 'post', 'complete', () =>
      HttpResponse.json({ ...state.task, status: 'Completed' }),
    )
    const { router, outbox } = await renderApp(`/tasks/${TASK.id}`)

    await user.click(await screen.findByRole('checkbox', { name: 'Hands washed' }))
    setOnline(false)
    await user.upload(
      screen.getByTestId('photo-input-camera'),
      new File(['pixels'], 'shot.jpg', { type: 'image/jpeg' }),
    )
    // The thumbnail comes from the queued bytes and does not block completing.
    expect(await screen.findByRole('img', { name: 'Task photo 1' })).toBeInTheDocument()
    expect(outbox.getSnapshot().items.map((i) => i.type)).toEqual(['upload'])
    await user.click(screen.getByRole('button', { name: 'Complete task' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/today'))
    expect(uploads).toHaveLength(0)
    expect(state.calls).toHaveLength(0)

    setOnline(true)
    await waitFor(() => expect(state.calls).toHaveLength(1))
    expect(uploads).toHaveLength(1)
    expect(uploads[0]!.requestId).toMatch(/^[0-9a-f-]{36}$/)
    expect(state.calls[0]!.body).toMatchObject({ photoIds: [PHOTO_ID] })
    expect(JSON.stringify(state.calls[0]!.body)).not.toContain('local-')
  })

  it('keeps an offline draft in the queue and sends it when the connection returns', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(taskDetail({ fields: FIELDS }))
    const { outbox } = await renderApp(`/tasks/${TASK.id}`)

    const temp = await screen.findByLabelText('Fridge temp')
    setOnline(false)
    await user.type(temp, '4')
    expect(
      await screen.findByText('Saved on this device', {}, { timeout: 4000 }),
    ).toBeInTheDocument()
    expect(state.saves).toEqual([])
    expect(outbox.getSnapshot().items.map((i) => i.type)).toEqual(['progress'])

    setOnline(true)
    await waitFor(() => expect(state.saves).toHaveLength(1))
    expect(state.saves[0]).toMatchObject({ answers: { temp: { value: 4 } } })
    await waitFor(() => expect(outbox.getSnapshot().items).toHaveLength(0))
  })

  it('adds a reading offline, lists it as waiting and syncs it later', async () => {
    const user = userEvent.setup()
    signIn()
    const state = serveTask(taskDetail({ kind: 'RecordLog', fields: LOG_FIELDS }))
    onWrite(state, 'post', 'entries', (body) => {
      const answers = (body as { answers: Record<string, { value: number }> }).answers
      state.task = {
        ...state.task,
        entries: [
          entry({
            answers: { temp: { value: answers.temp!.value, photoIds: [] } },
            submittedAt: '2026-11-07T08:00:00Z',
            submittedBy: 'Sam Chef',
          }),
        ],
      }
      return HttpResponse.json(state.task, { status: 201 })
    })
    const { outbox } = await renderApp(`/tasks/${TASK.id}`)

    await user.type(await screen.findByLabelText('Reading'), '3')
    setOnline(false)
    await user.click(screen.getByRole('button', { name: 'Add reading' }))

    const waiting = await screen.findByTestId('reading')
    expect(waiting).toHaveTextContent('Waiting to sync')
    expect(waiting).toHaveTextContent('3 °C')
    expect(screen.getByRole('heading', { name: 'Readings (1)' })).toBeInTheDocument()
    expect(screen.getByLabelText('Reading')).toHaveValue('')
    expect(state.calls).toEqual([])
    // The log can be finished while the reading is still queued.
    expect(screen.getByRole('button', { name: 'Finish' })).toBeEnabled()

    setOnline(true)
    await waitFor(() => expect(state.calls).toHaveLength(1))
    expect(state.calls[0]!.body).toMatchObject({
      answers: { temp: { value: 3, photoIds: [] } },
      requestId: expect.any(String),
      recordedAt: expect.any(String),
    })
    await waitFor(() => expect(screen.queryByText('Waiting to sync')).not.toBeInTheDocument())
    expect(outbox.getSnapshot().items).toHaveLength(0)
    expect(await screen.findByTestId('reading')).toHaveTextContent('Sam Chef')
  })

  describe('when a queued change is refused', () => {
    async function queueRefusedCompletion() {
      const user = userEvent.setup()
      signIn()
      const state = serveTask(taskDetail({ fields: FIELDS }))
      let refuse = true
      onWrite(state, 'post', 'complete', () =>
        refuse
          ? HttpResponse.json(problem(409, 'task.not_open', 'The task is not open.'), {
              status: 409,
            })
          : HttpResponse.json({ ...state.task, status: 'Completed' }),
      )
      const app = await renderApp(`/tasks/${TASK.id}`)
      await fillChecklist(user)
      setOnline(false)
      await user.click(screen.getByRole('button', { name: 'Complete task' }))
      await waitFor(() => expect(app.router.state.location.pathname).toBe('/today'))
      setOnline(true)
      const failed = await screen.findByRole('button', { name: '1 change could not sync' })
      return { user, state, app, failed, allow: () => (refuse = false) }
    }

    it('shows the failure in the indicator and retries', async () => {
      const { user, failed, state, app, allow } = await queueRefusedCompletion()
      await user.click(failed)
      expect(await screen.findByText('Opening checks')).toBeInTheDocument()
      expect(screen.getByRole('alert')).toHaveTextContent(
        'This task was already completed or closed before your changes synced.',
      )
      expect(state.calls).toHaveLength(1)

      allow()
      await user.click(screen.getByRole('button', { name: 'Retry' }))
      await waitFor(() => expect(state.calls).toHaveLength(2))
      await waitFor(() => expect(app.outbox.getSnapshot().items).toHaveLength(0))
      expect(screen.queryByTestId('sync-indicator')).not.toBeInTheDocument()
    })

    it('discards only after confirmation', async () => {
      const { user, failed, app } = await queueRefusedCompletion()
      const confirm = vi.spyOn(window, 'confirm').mockReturnValueOnce(false)
      await user.click(failed)
      await user.click(await screen.findByRole('button', { name: 'Discard' }))
      expect(confirm).toHaveBeenCalledTimes(1)
      expect(app.outbox.getSnapshot().items).toHaveLength(1)

      confirm.mockReturnValueOnce(true)
      await user.click(screen.getByRole('button', { name: 'Discard' }))
      await waitFor(() => expect(app.outbox.getSnapshot().items).toHaveLength(0))
    })

    it('says so on the task page', async () => {
      const { app } = await queueRefusedCompletion()
      app.router.history.push(`/tasks/${TASK.id}`)
      expect(await screen.findByTestId('queued-completion')).toHaveTextContent(
        /could not be synced/,
      )
    })
  })
})
