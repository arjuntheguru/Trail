import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { toSession } from '@/auth/session-store'
import { createIdbOutboxStorage } from '@/offline/outbox-storage'
import { createOutbox } from '@/offline/outbox'
import { problem, sessionResponse, taskDetail } from '@/test/fixtures'
import { resetNetwork, setOnline } from '@/test/network'
import { createTestAuth, renderApp } from '@/test/renderApp'
import { API, server } from '@/test/server'

const TASK = taskDetail()

function serveToday() {
  const calls = { logout: 0 }
  server.use(
    http.post(`${API}/api/v1/auth/refresh`, () =>
      HttpResponse.json(sessionResponse({ permissions: ['tasks.view', 'sites.view'] })),
    ),
    http.post(`${API}/api/v1/auth/logout`, () => {
      calls.logout++
      return new HttpResponse(null, { status: 204 })
    }),
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
  return calls
}

const complete = (taskId = TASK.id) => ({
  taskId,
  body: { note: 'done' },
  requestId: crypto.randomUUID(),
  completedAt: '2026-11-07T08:30:00.000Z',
})

afterEach(() => resetNetwork())

describe('sync indicator and Today', () => {
  it('appears only while changes are queued, with their count', async () => {
    serveToday()
    setOnline(false)
    const { outbox } = await renderApp('/today')
    await screen.findByRole('link', { name: /Opening checks/ })
    expect(screen.queryByTestId('sync-indicator')).not.toBeInTheDocument()

    await outbox.enqueueComplete(complete())
    await outbox.enqueueProgress('99999999-0000-0000-0000-0000000000ff', {
      answers: {},
      note: 'x',
      photoIds: [],
    })
    const indicator = await screen.findByRole('button', { name: '2 changes waiting to sync' })
    expect(indicator).toHaveTextContent('2')

    await outbox.discard(outbox.getSnapshot().items[0]!.id)
    expect(
      await screen.findByRole('button', { name: '1 change waiting to sync' }),
    ).toBeInTheDocument()
  })

  it('marks a task that has queued changes on its Today card', async () => {
    serveToday()
    setOnline(false)
    const { outbox } = await renderApp('/today')
    const card = await screen.findByTestId('task-card')
    expect(card).not.toHaveTextContent('Waiting to sync')

    await outbox.enqueueComplete(complete())
    await waitFor(() =>
      expect(screen.getByTestId('task-card')).toHaveTextContent('Waiting to sync'),
    )
  })

  it('shows a failed sync with its message and a distinct label', async () => {
    serveToday()
    server.use(
      http.post(`${API}/api/v1/tasks/${TASK.id}/complete`, () =>
        HttpResponse.json(problem(409, 'task.late_window_closed', 'Closed.'), { status: 409 }),
      ),
    )
    const user = userEvent.setup()
    const { outbox } = await renderApp('/today')
    await screen.findByTestId('task-card')
    await outbox.enqueueComplete(complete())

    const indicator = await screen.findByRole('button', { name: '1 change could not sync' })
    expect(screen.getByTestId('task-card')).toHaveTextContent('Sync failed')
    await user.click(indicator)
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'This task was already completed or closed before your changes synced.',
    )
    expect(screen.getByRole('button', { name: 'Sync now' })).toBeInTheDocument()
  })
})

describe('signing out with changes on this device', () => {
  async function signOutWithQueue() {
    const calls = serveToday()
    const storage = createIdbOutboxStorage(`signout-${crypto.randomUUID()}`)
    setOnline(false)
    const app = await renderApp('/today', undefined, { storage })
    await screen.findByTestId('task-card')
    await app.outbox.enqueueComplete(complete())
    await app.outbox.enqueueEntry({
      taskId: TASK.id,
      body: { answers: {}, note: null, photoIds: [] },
      requestId: crypto.randomUUID(),
      recordedAt: '2026-11-07T08:00:00.000Z',
    })
    await app.outbox.settled()
    const user = userEvent.setup()
    const openSignOut = async () => {
      await user.click(screen.getAllByRole('button', { name: /Account menu for Sam Chef/ })[0]!)
      await user.click(await screen.findByRole('menuitem', { name: /^Sign out$/ }))
    }
    // What a later session of the same user would find on disk.
    const reopened = async () => {
      const again = createOutbox({
        store: app.auth.store,
        client: app.auth.client,
        queryClient: app.queryClient,
        storage,
      })
      app.auth.store.setSession(toSession(sessionResponse()))
      await again.settled()
      return again.getSnapshot().items
    }
    return { app, calls, openSignOut, reopened }
  }

  it('asks first and keeps everything when the person says no', async () => {
    const { calls, openSignOut, app, reopened } = await signOutWithQueue()
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    await openSignOut()
    expect(confirm).toHaveBeenCalledWith(
      '2 changes on this device have not synced yet and will be lost if you sign out.',
    )
    expect(calls.logout).toBe(0)
    expect(app.auth.store.getState().status).toBe('authenticated')
    expect(await reopened()).toHaveLength(2)
  })

  it('deletes the queue of that user only after a confirmed sign-out', async () => {
    const { calls, openSignOut, app, reopened } = await signOutWithQueue()
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    await openSignOut()
    await waitFor(() => expect(calls.logout).toBe(1))
    await waitFor(() => expect(app.auth.store.getState().status).toBe('anonymous'))
    expect(await reopened()).toHaveLength(0)
  })

  it('does not ask when nothing is queued', async () => {
    const calls = serveToday()
    const confirm = vi.spyOn(window, 'confirm')
    const user = userEvent.setup()
    await renderApp('/today', createTestAuth())
    await screen.findByTestId('task-card')
    await user.click(screen.getAllByRole('button', { name: /Account menu for Sam Chef/ })[0]!)
    await user.click(await screen.findByRole('menuitem', { name: /^Sign out$/ }))
    await waitFor(() => expect(calls.logout).toBe(1))
    expect(confirm).not.toHaveBeenCalled()
  })
})
