import 'fake-indexeddb/auto'

import { QueryClient } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import { createApiClient } from '@/api/client'
import { createSessionStore, toSession } from '@/auth/session-store'
import { taskKey } from '@/features/tasks/queries'
import { problem, sessionResponse, taskDetail } from '@/test/fixtures'
import { API, server } from '@/test/server'

import { backoffMs, BACKOFF_MAX_MS, createOutbox } from './outbox'
import { createIdbOutboxStorage } from './outbox-storage'

const TASK_A = '99999999-0000-0000-0000-00000000000a'
const TASK_B = '99999999-0000-0000-0000-00000000000b'
const SITE = '55555555-0000-0000-0000-000000000001'
const SERVER_PHOTO = '77777777-0000-0000-0000-000000000001'
const OTHER_USER = 'dddddddd-0000-0000-0000-000000000002'

interface Call {
  method: string
  path: string
  body: unknown
}

function setup(options: { token?: string; userId?: string; storageName?: string } = {}) {
  const store = createSessionStore()
  const base = sessionResponse()
  const signIn = (token: string, userId = base.user.id) =>
    store.setSession(toSession({ ...base, accessToken: token, user: { ...base.user, id: userId } }))
  signIn(options.token ?? 'tok-1', options.userId)
  const queryClient = new QueryClient()
  const storageName = options.storageName ?? `outbox-test-${crypto.randomUUID()}`
  const clock = { now: 1_000_000, online: true }
  const create = () =>
    createOutbox({
      store,
      client: createApiClient({ baseUrl: API }),
      queryClient,
      storage: createIdbOutboxStorage(storageName),
      now: () => clock.now,
      withLock: (task) => task(),
      isOnline: () => clock.online,
    })
  return { store, signIn, queryClient, clock, outbox: create(), create, storageName }
}

/** Records every write the outbox sends; `respond` decides the answer (default: the task, 200/201). */
function serve(respond?: (call: Call) => Response | undefined) {
  const calls: Call[] = []
  const answer =
    (method: string, path: string) =>
    async ({ request }: { request: Request }) => {
      const body: unknown = path.includes('attachments')
        ? (/name="requestId"\r\n\r\n([^\r]+)/.exec(await request.text())?.[1] ?? null)
        : await request.json()
      const call = { method, path, body }
      calls.push(call)
      return (
        respond?.(call) ??
        (path.includes('attachments')
          ? HttpResponse.json(
              {
                id: SERVER_PHOTO,
                kind: 'Photo',
                contentType: 'image/jpeg',
                size: 1,
                fileName: 'a.jpg',
                siteId: SITE,
              },
              { status: 201 },
            )
          : HttpResponse.json(taskDetail({ id: path.split('/')[4] })))
      )
    }
  server.use(
    http.post(`${API}/api/v1/tasks/:id/complete`, ({ request, params }) =>
      answer('POST', `/api/v1/tasks/${String(params.id)}/complete`)({ request }),
    ),
    http.post(`${API}/api/v1/tasks/:id/entries`, ({ request, params }) =>
      answer('POST', `/api/v1/tasks/${String(params.id)}/entries`)({ request }),
    ),
    http.put(`${API}/api/v1/tasks/:id/progress`, ({ request, params }) =>
      answer('PUT', `/api/v1/tasks/${String(params.id)}/progress`)({ request }),
    ),
    http.post(`${API}/api/v1/attachments`, ({ request }) =>
      answer('POST', '/api/v1/attachments')({ request }),
    ),
  )
  return calls
}

const reading = (photoId: string) => ({
  answers: { temp: { value: 4, photoIds: [photoId] } },
  note: null,
  photoIds: [photoId],
})

const photo = () => new Blob([new Uint8Array([1, 2, 3, 4])], { type: 'image/jpeg' })

const upload = (taskId: string) => ({
  taskId,
  blob: photo(),
  fileName: 'shot.jpg',
  kind: 'Photo' as const,
  siteId: SITE,
  requestId: crypto.randomUUID(),
})

const entry = (taskId: string, body = { answers: {}, note: null, photoIds: [] as string[] }) => ({
  taskId,
  body,
  requestId: crypto.randomUUID(),
  recordedAt: '2026-11-07T08:00:00.000Z',
})

const complete = (taskId: string, extra: object = {}) => ({
  taskId,
  body: { note: 'done', ...extra },
  requestId: crypto.randomUUID(),
  completedAt: '2026-11-07T08:30:00.000Z',
})

describe('outbox ordering and photo ids', () => {
  it('sends in order, uploads first, and rewrites local photo ids everywhere in the body', async () => {
    const t = setup()
    const calls = serve()
    t.clock.online = false
    await t.outbox.settled()

    const localId = await t.outbox.enqueueUpload(upload(TASK_A))
    expect(localId).toMatch(/^local-/)
    // Queued after the upload, but for another task, and before the upload's own task completes.
    await t.outbox.enqueueEntry(entry(TASK_B))
    await t.outbox.enqueueEntry(entry(TASK_A, reading(localId)))

    t.clock.online = true
    await t.outbox.syncNow()

    expect(
      calls.map((c) => `${c.method} ${c.path.replace(TASK_A, 'A').replace(TASK_B, 'B')}`),
    ).toEqual([
      'POST /api/v1/attachments',
      'POST /api/v1/tasks/B/entries',
      'POST /api/v1/tasks/A/entries',
    ])
    const sent = calls[2]!.body as ReturnType<typeof reading>
    expect(sent.photoIds).toEqual([SERVER_PHOTO])
    expect(sent.answers.temp.photoIds).toEqual([SERVER_PHOTO])
    expect(t.outbox.getSnapshot().items).toHaveLength(0)
    // The mapping stays available for forms that still hold the local id.
    expect(t.outbox.getUpload(localId)).toMatchObject({ serverId: SERVER_PHOTO, blob: null })
  })

  it('puts a task upload ahead of an earlier queued item of the same task', async () => {
    const t = setup()
    const calls = serve()
    t.clock.online = false
    await t.outbox.settled()
    await t.outbox.enqueueEntry(entry(TASK_A))
    const localId = await t.outbox.enqueueUpload(upload(TASK_A))
    t.clock.online = true
    await t.outbox.syncNow()
    expect(calls.map((c) => c.path)).toEqual([
      '/api/v1/attachments',
      `/api/v1/tasks/${TASK_A}/entries`,
    ])
    expect(localId).toMatch(/^local-/)
  })

  it('sends the upload requestId so a repeated upload is recognised', async () => {
    const t = setup()
    const calls = serve()
    const input = upload(TASK_A)
    await t.outbox.enqueueUpload(input)
    await t.outbox.syncNow()
    expect(calls[0]!.body).toBe(input.requestId)
  })

  it('fails an item whose photo is neither queued nor uploaded', async () => {
    const t = setup()
    const calls = serve()
    await t.outbox.enqueueEntry(entry(TASK_A, reading('local-gone')))
    await t.outbox.syncNow()
    expect(calls).toHaveLength(0)
    expect(t.outbox.getSnapshot().items[0]).toMatchObject({
      status: 'failed',
      error: { code: 'outbox.photo_missing' },
    })
  })

  it('keeps a dependent item blocked while its upload has failed', async () => {
    const t = setup()
    const calls = serve((call) =>
      call.path.includes('attachments')
        ? HttpResponse.json(problem(400, 'attachment.too_large', 'Too large.'), { status: 400 })
        : undefined,
    )
    t.clock.online = false
    await t.outbox.settled()
    const localId = await t.outbox.enqueueUpload(upload(TASK_A))
    await t.outbox.enqueueEntry(entry(TASK_A, reading(localId)))
    t.clock.online = true
    await t.outbox.syncNow()
    expect(calls.map((c) => c.path)).toEqual(['/api/v1/attachments'])
    expect(t.outbox.getSnapshot()).toMatchObject({ pendingCount: 1, failedCount: 1 })
  })
})

describe('outbox coalescing', () => {
  it('keeps only the latest progress per task', async () => {
    const t = setup()
    const calls = serve()
    t.clock.online = false
    await t.outbox.settled()
    await t.outbox.enqueueProgress(TASK_A, { answers: {}, note: 'one', photoIds: [] })
    await t.outbox.enqueueProgress(TASK_A, { answers: {}, note: 'two', photoIds: [] })
    await t.outbox.enqueueProgress(TASK_B, { answers: {}, note: 'other', photoIds: [] })
    expect(t.outbox.getSnapshot().items).toHaveLength(2)

    t.clock.online = true
    await t.outbox.syncNow()
    expect(calls.map((c) => (c.body as { note: string }).note)).toEqual(['two', 'other'])
  })

  it('drops queued progress when the task is completed, and ignores later drafts', async () => {
    const t = setup()
    const calls = serve()
    t.clock.online = false
    await t.outbox.settled()
    await t.outbox.enqueueProgress(TASK_A, { answers: {}, note: 'draft', photoIds: [] })
    await t.outbox.enqueueComplete(complete(TASK_A))
    expect(t.outbox.getSnapshot().items.map((i) => i.type)).toEqual(['complete'])
    await t.outbox.enqueueProgress(TASK_A, { answers: {}, note: 'late draft', photoIds: [] })
    expect(t.outbox.getSnapshot().items.map((i) => i.type)).toEqual(['complete'])

    t.clock.online = true
    await t.outbox.syncNow()
    expect(calls.map((c) => c.method + ' ' + c.path.split('/').pop())).toEqual(['POST complete'])
  })

  it('does not queue a second completion for the same task', async () => {
    const t = setup()
    t.clock.online = false
    await t.outbox.settled()
    const first = await t.outbox.enqueueComplete(complete(TASK_A))
    const second = await t.outbox.enqueueComplete(complete(TASK_A))
    expect(second.id).toBe(first.id)
    expect(t.outbox.getSnapshot().items).toHaveLength(1)
  })
})

describe('outbox retries', () => {
  it.each([
    ['no connection', () => HttpResponse.error()],
    ['a server error', () => HttpResponse.json(problem(503, 'x', 'down'), { status: 503 })],
    ['rate limiting', () => HttpResponse.json(problem(429, 'x', 'slow'), { status: 429 })],
  ])('backs off exponentially after %s and keeps the same requestId', async (_name, failure) => {
    const t = setup()
    let failing = true
    const calls = serve(() => (failing ? failure() : undefined))
    const queued = await t.outbox.enqueueComplete(complete(TASK_A))
    await t.outbox.settled()
    await t.outbox.process()
    expect(calls).toHaveLength(1)
    expect(t.outbox.getSnapshot().items[0]).toMatchObject({
      status: 'pending',
      attempts: 1,
      nextAttemptAt: t.clock.now + 2_000,
    })

    // Not due yet: nothing is sent.
    await t.outbox.process()
    expect(calls).toHaveLength(1)

    t.clock.now += 2_000
    await t.outbox.process()
    expect(calls).toHaveLength(2)
    expect(t.outbox.getSnapshot().items[0]).toMatchObject({
      attempts: 2,
      nextAttemptAt: t.clock.now + 4_000,
    })

    failing = false
    t.clock.now += 4_000
    await t.outbox.process()
    expect(calls).toHaveLength(3)
    expect(t.outbox.getSnapshot().items).toHaveLength(0)
    const ids = calls.map((c) => (c.body as { requestId: string }).requestId)
    expect(new Set(ids)).toEqual(new Set([queued.requestId]))
    expect((calls[2]!.body as { completedAt: string }).completedAt).toBe('2026-11-07T08:30:00.000Z')
  })

  it('caps the delay at five minutes', () => {
    expect(backoffMs(1)).toBe(2_000)
    expect(backoffMs(3)).toBe(8_000)
    expect(backoffMs(30)).toBe(BACKOFF_MAX_MS)
  })

  it('syncNow ignores the backoff', async () => {
    const t = setup()
    let failing = true
    const calls = serve(() =>
      failing ? HttpResponse.json(problem(503, 'x', 'down'), { status: 503 }) : undefined,
    )
    await t.outbox.enqueueComplete(complete(TASK_A))
    await t.outbox.settled()
    await t.outbox.process()
    failing = false
    await t.outbox.syncNow()
    expect(calls).toHaveLength(2)
    expect(t.outbox.getSnapshot().items).toHaveLength(0)
  })

  it('treats a replay answered with 200 as done and removes the item', async () => {
    const t = setup()
    let lost = true
    const requestIds: unknown[] = []
    const detail = taskDetail({ id: TASK_A, status: 'Completed' })
    const calls = serve((call) => {
      requestIds.push((call.body as { requestId: string }).requestId)
      // The first response never arrives although the server handled the request.
      return lost ? HttpResponse.error() : HttpResponse.json(detail, { status: 200 })
    })
    await t.outbox.enqueueComplete(complete(TASK_A))
    await t.outbox.settled()
    await t.outbox.process()
    expect(t.outbox.getSnapshot().pendingCount).toBe(1)

    lost = false
    await t.outbox.syncNow()
    expect(calls).toHaveLength(2)
    expect(requestIds[0]).toBe(requestIds[1])
    expect(t.outbox.getSnapshot().items).toHaveLength(0)
    expect(t.queryClient.getQueryData(taskKey(sessionResponse().organization!.id, TASK_A))).toEqual(
      detail,
    )
  })
})

describe('outbox failures', () => {
  it('marks a refused change failed, holds that task back and carries on with others', async () => {
    const t = setup()
    const calls = serve((call) =>
      call.path.includes(TASK_A) && call.path.endsWith('/complete')
        ? HttpResponse.json(problem(409, 'task.not_open', 'Task is not open.'), { status: 409 })
        : undefined,
    )
    t.clock.online = false
    await t.outbox.settled()
    await t.outbox.enqueueComplete(complete(TASK_A))
    await t.outbox.enqueueEntry(entry(TASK_A))
    await t.outbox.enqueueEntry(entry(TASK_B))
    t.clock.online = true
    await t.outbox.syncNow()

    expect(calls.map((c) => c.path)).toEqual([
      `/api/v1/tasks/${TASK_A}/complete`,
      `/api/v1/tasks/${TASK_B}/entries`,
    ])
    const items = t.outbox.getSnapshot().items
    expect(items).toHaveLength(2)
    expect(items[0]).toMatchObject({
      type: 'complete',
      status: 'failed',
      error: {
        code: 'task.not_open',
        message: 'This task was already completed or closed before your changes synced.',
      },
    })
    expect(items[1]).toMatchObject({ type: 'entry', taskId: TASK_A, status: 'pending' })

    // A later sync still does not send the held-back item.
    await t.outbox.syncNow()
    expect(calls).toHaveLength(2)
  })

  it('retry sends a failed change again, discard removes it', async () => {
    const t = setup()
    let refuse = true
    const calls = serve(() =>
      refuse ? HttpResponse.json(problem(400, 'x', 'Nope.'), { status: 400 }) : undefined,
    )
    await t.outbox.enqueueComplete(complete(TASK_A))
    await t.outbox.enqueueEntry(entry(TASK_B))
    await t.outbox.settled()
    await t.outbox.syncNow()
    expect(t.outbox.getSnapshot()).toMatchObject({ failedCount: 2, pendingCount: 0 })

    const [first, second] = t.outbox.getSnapshot().items
    await t.outbox.discard(second!.id)
    refuse = false
    await t.outbox.retry(first!.id)
    await t.outbox.syncNow()
    expect(t.outbox.getSnapshot().items).toHaveLength(0)
    expect(calls).toHaveLength(3)
  })

  it('pauses on 401 until the session has a different token', async () => {
    const t = setup({ token: 'old' })
    let unauthorized = true
    const calls = serve(() =>
      unauthorized ? HttpResponse.json(problem(401, 'x', 'Expired.'), { status: 401 }) : undefined,
    )
    await t.outbox.enqueueComplete(complete(TASK_A))
    await t.outbox.settled()
    await t.outbox.process()
    expect(calls).toHaveLength(1)
    expect(t.outbox.getSnapshot().items[0]).toMatchObject({ status: 'pending', attempts: 0 })

    await t.outbox.process()
    expect(calls).toHaveLength(1)

    unauthorized = false
    t.signIn('new')
    await t.outbox.process()
    expect(calls).toHaveLength(2)
    expect(t.outbox.getSnapshot().items).toHaveLength(0)
  })

  it('sends nothing while the session has no token, then sends once it has one', async () => {
    const t = setup({ token: '' })
    const calls = serve()
    const stop = t.outbox.start()
    await t.outbox.enqueueComplete(complete(TASK_A))
    await t.outbox.process()
    expect(calls).toHaveLength(0)
    expect(t.outbox.getSnapshot().pendingCount).toBe(1)

    t.signIn('real')
    await expect.poll(() => calls.length).toBe(1)
    stop()
  })

  it('sends again when the network returns', async () => {
    const t = setup()
    const calls = serve()
    const stop = t.outbox.start()
    t.clock.online = false
    await t.outbox.enqueueComplete(complete(TASK_A))
    await t.outbox.settled()
    expect(calls).toHaveLength(0)

    t.clock.online = true
    window.dispatchEvent(new Event('online'))
    await expect.poll(() => calls.length).toBe(1)
    stop()
  })
})

describe('outbox storage and scoping', () => {
  it('survives a reload, including photo bytes', async () => {
    const t = setup()
    t.clock.online = false
    await t.outbox.settled()
    const localId = await t.outbox.enqueueUpload(upload(TASK_A))
    await t.outbox.enqueueComplete(complete(TASK_A))
    await t.outbox.settled()

    const reloaded = t.create()
    await reloaded.settled()
    expect(reloaded.getSnapshot()).toMatchObject({ loaded: true })
    expect(reloaded.getSnapshot().items.map((i) => i.type)).toEqual(['upload', 'complete'])
    const blob = reloaded.getUpload(localId)?.blob
    expect(blob?.size).toBe(4)
    expect(blob?.type).toBe('image/jpeg')
  })

  it('keeps one queue per user and organization and sends only the signed-in one', async () => {
    const t = setup()
    const calls = serve()
    t.clock.online = false
    await t.outbox.settled()
    await t.outbox.enqueueComplete(complete(TASK_A))
    await t.outbox.settled()

    t.signIn('tok-2', OTHER_USER)
    await t.outbox.settled()
    expect(t.outbox.getSnapshot().items).toHaveLength(0)
    await t.outbox.enqueueEntry(entry(TASK_B))
    t.clock.online = true
    await t.outbox.syncNow()
    // Only the second user's item went out.
    expect(calls.map((c) => c.path)).toEqual([`/api/v1/tasks/${TASK_B}/entries`])

    t.clock.online = false
    t.signIn('tok-3')
    await t.outbox.settled()
    expect(t.outbox.getSnapshot().items.map((i) => i.type)).toEqual(['complete'])
    t.clock.online = true
    await t.outbox.syncNow()
    expect(calls.map((c) => c.path)).toEqual([
      `/api/v1/tasks/${TASK_B}/entries`,
      `/api/v1/tasks/${TASK_A}/complete`,
    ])
  })

  it('keeps the queue when the session expires and deletes it only when cleared', async () => {
    const t = setup()
    t.clock.online = false
    await t.outbox.settled()
    await t.outbox.enqueueComplete(complete(TASK_A))
    const scope = {
      userId: sessionResponse().user.id,
      organizationId: sessionResponse().organization!.id,
    }

    t.store.clear('expired')
    await t.outbox.settled()
    expect(t.outbox.getSnapshot().items).toHaveLength(0)

    t.signIn('again')
    const resumed = t.create()
    await resumed.settled()
    expect(resumed.getSnapshot().items).toHaveLength(1)

    await resumed.clearScope(scope)
    const after = t.create()
    await after.settled()
    expect(after.getSnapshot().items).toHaveLength(0)
  })
})

describe('outbox.plan', () => {
  it('queues when earlier changes to the task are waiting, or a photo is not uploaded yet', async () => {
    const t = setup()
    t.clock.online = false
    await t.outbox.settled()
    expect((await t.outbox.plan(TASK_A, complete(TASK_A).body)).queue).toBe(false)

    const localId = await t.outbox.enqueueUpload(upload(TASK_A))
    expect((await t.outbox.plan(TASK_A, reading(localId))).queue).toBe(true)
    expect((await t.outbox.plan(TASK_B, complete(TASK_B).body)).queue).toBe(false)

    await t.outbox.enqueueEntry(entry(TASK_B))
    expect((await t.outbox.plan(TASK_B, complete(TASK_B).body)).queue).toBe(true)
  })

  it('swaps in the server id of a photo that has been uploaded', async () => {
    const t = setup()
    serve()
    const localId = await t.outbox.enqueueUpload(upload(TASK_A))
    await t.outbox.syncNow()
    const planned = await t.outbox.plan(TASK_A, reading(localId))
    expect(planned.queue).toBe(false)
    expect(planned.body.photoIds).toEqual([SERVER_PHOTO])
  })
})
