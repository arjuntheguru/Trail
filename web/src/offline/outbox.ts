import type { QueryClient } from '@tanstack/react-query'

import type { ApiClient } from '@/api/client'
import { ApiError, errorMessage } from '@/api/problem'
import { unwrap } from '@/api/unwrap'
import type { SessionStore } from '@/auth/session-store'
import { uploadAttachment, type AttachmentKind } from '@/features/tasks/attachments'
import { applyTaskUpdate, taskKey } from '@/features/tasks/queries'

import { isOnline } from './online'
import { createIdbOutboxStorage, type OutboxStorage } from './outbox-storage'
import {
  isLocalId,
  isUploaded,
  LOCAL_ID_PREFIX,
  type CompleteBody,
  type CompleteItem,
  type EntryBody,
  type OutboxError,
  type OutboxItem,
  type OutboxScope,
  type ProgressBody,
  type UploadItem,
} from './outbox-types'

export const OUTBOX_INTERVAL_MS = 30_000
export const BACKOFF_BASE_MS = 2_000
export const BACKOFF_MAX_MS = 5 * 60_000
/** Uploaded photos stay around (for mapping local ids) this long after nothing references them. */
const UPLOAD_KEEP_MS = 24 * 60 * 60 * 1000
const LOCK_NAME = 'trail-outbox'

export const backoffMs = (attempts: number) =>
  Math.min(BACKOFF_MAX_MS, BACKOFF_BASE_MS * 2 ** Math.max(attempts - 1, 0))

const CLOSED_MESSAGE = 'This task was already completed or closed before your changes synced.'
const FRIENDLY: Record<string, string> = {
  'task.not_open': CLOSED_MESSAGE,
  'task.late_window_closed': CLOSED_MESSAGE,
}

/** Cross-tab mutual exclusion so two tabs never send the same queue at once. */
const defaultWithLock = <T>(task: () => Promise<T>): Promise<T> => {
  const locks = typeof navigator === 'undefined' ? undefined : navigator.locks
  return locks ? locks.request(LOCK_NAME, task) : task()
}

export interface OutboxSnapshot {
  /** False until the saved queue for the current user and organization has been read. */
  loaded: boolean
  /** Everything still to send or failed, oldest first. Uploaded photos kept only for id mapping are hidden. */
  items: readonly OutboxItem[]
  pendingCount: number
  failedCount: number
  syncing: boolean
}

export interface OutboxOptions {
  store: SessionStore
  client: ApiClient
  queryClient: QueryClient
  storage?: OutboxStorage
  now?: () => number
  withLock?: <T>(task: () => Promise<T>) => Promise<T>
  isOnline?: () => boolean
  intervalMs?: number
}

export interface UploadInput {
  taskId: string
  blob: Blob
  fileName: string
  kind: AttachmentKind
  siteId: string
  requestId: string
}

type PhotoBody = {
  note?: string | null
  photoIds?: string[] | null
  answers?: Record<string, { photoIds?: string[] | null }> | null
}

/** Thrown while a dependency (an upload) has not finished; the item stays as it is. */
class Waiting extends Error {}

type Step = 'next' | 'block' | 'stop'

const isSendable = (item: OutboxItem) =>
  item.status === 'pending' && !(item.type === 'upload' && item.serverId)

const photoMissing = (detail: string) =>
  new ApiError({ status: 422, code: 'outbox.photo_missing', detail })

/**
 * The write outbox: changes made without a connection are kept on this device (IndexedDB, per user and
 * organization) and sent in order once the network and a real session are back. Every sendable item carries
 * a `requestId` created when it was queued, so a retry after a lost response is recognised by the server.
 */
export function createOutbox(options: OutboxOptions) {
  const { store, client, queryClient } = options
  const storage = options.storage ?? createIdbOutboxStorage()
  const now = options.now ?? Date.now
  const withLock = options.withLock ?? defaultWithLock
  const online = options.isOnline ?? isOnline
  const intervalMs = options.intervalMs ?? OUTBOX_INTERVAL_MS

  let scope: OutboxScope | null = null
  let items: OutboxItem[] = []
  let loaded = true
  let ready: Promise<void> = Promise.resolve()
  /** Bumped by every in-memory change so a reload from disk can tell it raced with one. */
  let version = 0
  let writes: Promise<void> = Promise.resolve()
  let syncing = false
  let running: Promise<void> | null = null
  const rerun = { wanted: false, force: false }
  /** The access token a 401 was received with; nothing is sent again until the session has another. */
  let pausedToken: string | null = null
  const listeners = new Set<() => void>()

  function compute(): OutboxSnapshot {
    const visible = items.filter((i) => !isUploaded(i))
    return {
      loaded,
      items: visible,
      pendingCount: visible.filter((i) => i.status === 'pending').length,
      failedCount: visible.filter((i) => i.status === 'failed').length,
      syncing,
    }
  }

  let snapshot: OutboxSnapshot = compute()

  function emit() {
    snapshot = compute()
    listeners.forEach((listener) => listener())
  }

  function persist(op: () => Promise<void>) {
    const next = writes.then(op)
    writes = next.catch(() => {})
    return next
  }

  function putItem(item: OutboxItem) {
    const index = items.findIndex((i) => i.id === item.id)
    items = index < 0 ? [...items, item] : items.map((i) => (i.id === item.id ? item : i))
    version++
    emit()
    return persist(() => storage.save(item))
  }

  function removeItems(ids: string[]) {
    const gone = items.filter((i) => ids.includes(i.id))
    if (gone.length === 0) return Promise.resolve()
    items = items.filter((i) => !ids.includes(i.id))
    version++
    emit()
    return persist(async () => {
      for (const item of gone) await storage.remove(item)
    })
  }

  // ---- session scope -------------------------------------------------------------------------------------

  function currentScope(): OutboxScope | null {
    const state = store.getState()
    if (state.status !== 'authenticated' || !state.session.organization) return null
    return { userId: state.session.user.id, organizationId: state.session.organization.id }
  }

  const tokenOf = () => {
    const state = store.getState()
    return state.status === 'authenticated' ? state.session.accessToken : ''
  }

  /** Switches to the queue of the signed-in user and organization. Other queues stay on disk untouched. */
  function syncScope(): boolean {
    const next = currentScope()
    if (scope?.userId === next?.userId && scope?.organizationId === next?.organizationId) {
      return false
    }
    scope = next
    items = []
    version++
    pausedToken = null
    if (!next) {
      loaded = true
      ready = Promise.resolve()
      emit()
      return true
    }
    loaded = false
    emit()
    const target = scope
    ready = storage
      .load(next)
      .then((saved) => {
        if (scope === target) items = saved
      })
      .catch(() => {
        // Unreadable storage: carry on with an empty queue rather than blocking the screens.
      })
      .then(() => {
        if (scope !== target) return
        loaded = true
        version++
        emit()
      })
    return true
  }

  async function begin(): Promise<OutboxScope> {
    syncScope()
    await ready
    if (!scope) throw new Error('Sign in to save changes on this device.')
    return scope
  }

  function newBase(s: OutboxScope, taskId: string, id: string = crypto.randomUUID()) {
    const seq = Math.max(now(), items.reduce((max, i) => Math.max(max, i.seq), 0) + 1)
    return {
      userId: s.userId,
      organizationId: s.organizationId,
      id,
      seq,
      taskId,
      createdAt: now(),
      attempts: 0,
      status: 'pending' as const,
      nextAttemptAt: 0,
    }
  }

  // ---- enqueueing ----------------------------------------------------------------------------------------

  async function enqueueUpload(input: UploadInput): Promise<string> {
    const s = await begin()
    const localId = `${LOCAL_ID_PREFIX}${crypto.randomUUID()}`
    await putItem({
      ...newBase(s, input.taskId, localId),
      type: 'upload',
      localId,
      requestId: input.requestId,
      blob: input.blob,
      kind: input.kind,
      siteId: input.siteId,
      fileName: input.fileName,
    })
    trigger()
    return localId
  }

  /** Keeps only the latest draft per task. A queued completion supersedes drafts, so none is kept then. */
  async function enqueueProgress(taskId: string, body: ProgressBody) {
    const s = await begin()
    await dropProgress(taskId)
    if (items.some((i) => i.taskId === taskId && i.type === 'complete')) return
    await putItem({ ...newBase(s, taskId), type: 'progress', body })
    trigger()
  }

  async function enqueueComplete(input: {
    taskId: string
    body: CompleteBody
    requestId: string
    completedAt: string
  }) {
    const s = await begin()
    const existing = items.find(
      (i): i is CompleteItem => i.taskId === input.taskId && i.type === 'complete',
    )
    if (existing) return existing
    await dropProgress(input.taskId)
    const item = {
      ...newBase(s, input.taskId),
      type: 'complete' as const,
      requestId: input.requestId,
      completedAt: input.completedAt,
      body: input.body,
    }
    await putItem(item)
    trigger()
    return item
  }

  async function enqueueEntry(input: {
    taskId: string
    body: EntryBody
    requestId: string
    recordedAt: string
  }) {
    const s = await begin()
    const item = {
      ...newBase(s, input.taskId),
      type: 'entry' as const,
      requestId: input.requestId,
      recordedAt: input.recordedAt,
      body: input.body,
    }
    await putItem(item)
    trigger()
    return item
  }

  /** Forgets queued drafts of a task, e.g. after the task was completed directly. */
  async function dropProgress(taskId: string) {
    await begin()
    await removeItems(
      items.filter((i) => i.taskId === taskId && i.type === 'progress').map((i) => i.id),
    )
  }

  // ---- photo ids -----------------------------------------------------------------------------------------

  const uploadFor = (localId: string) =>
    items.find((i): i is UploadItem => i.type === 'upload' && i.localId === localId)

  /** Replaces local photo ids (top level and inside answers) with the ids the server gave their uploads. */
  function mapBody<T extends PhotoBody>(body: T) {
    const waiting: string[] = []
    const missing: string[] = []
    const mapIds = (ids: string[]) =>
      ids.map((id) => {
        if (!isLocalId(id)) return id
        const upload = uploadFor(id)
        if (!upload) missing.push(id)
        else if (!upload.serverId) waiting.push(id)
        return upload?.serverId ?? id
      })
    const run = <U extends { photoIds?: string[] | null }>(holder: U): U =>
      holder.photoIds ? { ...holder, photoIds: mapIds(holder.photoIds) } : holder
    const next: T = { ...run(body) }
    if (body.answers) {
      next.answers = Object.fromEntries(
        Object.entries(body.answers).map(([key, answer]) => [key, run(answer)]),
      )
    }
    return { body: next, waiting, missing }
  }

  /**
   * Decides how a new completion or reading should travel. It must be queued when it uses a photo that has
   * not been uploaded yet, or when earlier changes to the same task are still queued (order matters).
   * `body` has the ids of already uploaded photos replaced.
   */
  async function plan<T extends PhotoBody>(taskId: string, body: T) {
    await begin()
    const mapped = mapBody(body)
    const queue =
      mapped.waiting.length > 0 ||
      mapped.missing.length > 0 ||
      items.some((i) => i.taskId === taskId && i.type !== 'progress' && !isUploaded(i))
    return { queue, body: mapped.body }
  }

  // ---- user actions --------------------------------------------------------------------------------------

  const reset = (item: OutboxItem): OutboxItem => ({
    ...item,
    status: 'pending',
    error: undefined,
    attempts: 0,
    nextAttemptAt: 0,
  })

  async function retry(id: string) {
    const item = items.find((i) => i.id === id)
    if (!item || item.status !== 'failed') return
    await putItem(reset(item))
    trigger(true)
  }

  async function retryAll() {
    for (const item of items.filter((i) => i.status === 'failed')) await putItem(reset(item))
    trigger(true)
  }

  /** Throws a change away, together with queued photos that nothing else would use. */
  async function discard(id: string) {
    const item = items.find((i) => i.id === id)
    if (!item) return
    const rest = items.filter((i) => i.id !== id && i.taskId === item.taskId)
    const orphans =
      item.type !== 'upload' && rest.every((i) => i.type === 'upload')
        ? rest.filter((i) => !isUploaded(i)).map((i) => i.id)
        : []
    await removeItems([id, ...orphans])
    trigger()
  }

  /** Deletes the queue of a user and organization from this device (confirmed sign-out). */
  async function clearScope(target: OutboxScope | null = scope) {
    if (!target) return
    if (scope?.userId === target.userId && scope.organizationId === target.organizationId) {
      items = []
      version++
      emit()
    }
    await persist(() => storage.clear(target))
  }

  // ---- processing ----------------------------------------------------------------------------------------

  /** FIFO, except that a task's uploads go before its other items. */
  function ordered(): OutboxItem[] {
    const sorted = [...items].sort((a, b) => a.seq - b.seq)
    const result: OutboxItem[] = []
    const emitted = new Set<string>()
    for (const item of sorted) {
      if (emitted.has(item.id)) continue
      if (item.type !== 'upload') {
        for (const upload of sorted) {
          if (
            upload.type === 'upload' &&
            upload.taskId === item.taskId &&
            !emitted.has(upload.id)
          ) {
            emitted.add(upload.id)
            result.push(upload)
          }
        }
      }
      emitted.add(item.id)
      result.push(item)
    }
    return result
  }

  /** Picks up what other tabs queued or already sent. */
  async function reload(s: OutboxScope) {
    await writes
    const seen = version
    const saved = await storage.load(s).catch(() => null)
    if (!saved || seen !== version || scope !== s) return
    const key = (list: OutboxItem[]) => JSON.stringify(list.map((i) => ({ ...i, blob: undefined })))
    if (key(saved) === key(items)) return
    items = saved
    version++
    emit()
  }

  async function dispatch(item: OutboxItem, s: OutboxScope) {
    if (item.type === 'upload') {
      if (!item.blob) throw photoMissing('The photo saved on this device could not be read.')
      const attachment = await uploadAttachment(client, {
        file: item.blob,
        fileName: item.fileName,
        kind: item.kind,
        siteId: item.siteId,
        requestId: item.requestId,
      })
      const live = items.find((i) => i.id === item.id)
      if (live?.type === 'upload') {
        await putItem({
          ...live,
          blob: null,
          serverId: attachment.id,
          uploadedAt: now(),
          error: undefined,
        })
      }
      return
    }

    const mapped = mapBody(item.body)
    if (mapped.missing.length > 0) {
      throw photoMissing('A photo saved on this device is no longer available.')
    }
    if (mapped.waiting.length > 0) throw new Waiting()

    const path = { params: { path: { id: item.taskId } } }
    if (item.type === 'progress') {
      const updated = await unwrap(
        client.PUT('/api/v1/tasks/{id}/progress', { ...path, body: mapped.body as ProgressBody }),
      )
      queryClient.setQueryData(taskKey(s.organizationId, item.taskId), updated)
    } else if (item.type === 'complete') {
      const updated = await unwrap(
        client.POST('/api/v1/tasks/{id}/complete', {
          ...path,
          body: {
            ...(mapped.body as CompleteBody),
            requestId: item.requestId,
            completedAt: item.completedAt,
          },
        }),
      )
      applyTaskUpdate(queryClient, s.organizationId, updated)
    } else {
      const updated = await unwrap(
        client.POST('/api/v1/tasks/{id}/entries', {
          ...path,
          body: {
            ...(mapped.body as EntryBody),
            requestId: item.requestId,
            recordedAt: item.recordedAt,
          },
        }),
      )
      applyTaskUpdate(queryClient, s.organizationId, updated)
    }
    await removeItems([item.id])
  }

  async function markFailed(id: string, error: OutboxError) {
    const live = items.find((i) => i.id === id)
    if (live) await putItem({ ...live, status: 'failed', error })
  }

  async function retryLater(id: string) {
    const live = items.find((i) => i.id === id)
    if (!live) return
    const attempts = live.attempts + 1
    await putItem({ ...live, attempts, nextAttemptAt: now() + backoffMs(attempts) })
  }

  async function attempt(item: OutboxItem, s: OutboxScope): Promise<Step> {
    const token = tokenOf()
    try {
      await dispatch(item, s)
      return 'next'
    } catch (error) {
      if (error instanceof Waiting) return 'block'
      const failure = error instanceof ApiError ? error : new ApiError({ status: 0 })
      const status = failure.status
      if (status === 401) {
        // The auth layer already tried to refresh; wait for the session to get a different token.
        pausedToken = token
        return 'stop'
      }
      if (status === 0 || status === 408 || status === 429 || status >= 500) {
        await retryLater(item.id)
        // No connection: the rest would fail the same way. Otherwise only this task waits.
        return status === 0 ? 'stop' : 'block'
      }
      await markFailed(item.id, {
        code: failure.code,
        message: (failure.code && FRIENDLY[failure.code]) || errorMessage(failure),
      })
      return 'block'
    }
  }

  function collectGarbage() {
    const stale = items
      .filter(isUploaded)
      .filter((u) => now() - (u.uploadedAt ?? 0) > UPLOAD_KEEP_MS)
      .filter(
        (u) =>
          !items.some((i) => i.type !== 'upload' && JSON.stringify(i.body).includes(u.localId)),
      )
    return removeItems(stale.map((u) => u.id))
  }

  async function pass(force: boolean) {
    syncScope()
    const s = scope
    if (!s) return
    await ready
    if (scope !== s || !online()) return
    const token = tokenOf()
    if (!token) return // an offline-restored session has no token yet
    if (force) pausedToken = null
    else if (pausedToken !== null && pausedToken === token) return

    await reload(s)
    if (scope !== s) return
    if (force) {
      for (const item of items.filter((i) => isSendable(i) && i.nextAttemptAt > 0)) {
        await putItem({ ...item, nextAttemptAt: 0 })
      }
    }
    if (!items.some(isSendable)) {
      await collectGarbage()
      return
    }

    syncing = true
    emit()
    try {
      const blocked = new Set<string>()
      for (const queued of ordered()) {
        if (scope !== s || !online()) break
        const item = items.find((i) => i.id === queued.id)
        if (!item || isUploaded(item)) continue
        if (item.status === 'failed') {
          blocked.add(item.taskId) // later changes to this task must not overtake it
          continue
        }
        if (blocked.has(item.taskId)) continue
        if (item.nextAttemptAt > now()) {
          blocked.add(item.taskId)
          continue
        }
        const step = await attempt(item, s)
        if (step === 'stop') break
        if (step === 'block') blocked.add(item.taskId)
      }
      await collectGarbage()
    } finally {
      syncing = false
      emit()
    }
  }

  function runPass(force = false): Promise<void> {
    if (running) {
      rerun.wanted = true
      rerun.force ||= force
      return running
    }
    running = (async () => {
      try {
        let again = true
        let forced = force
        while (again) {
          rerun.wanted = false
          rerun.force = false
          await withLock(() => pass(forced))
          again = rerun.wanted
          forced = rerun.force
        }
      } finally {
        running = null
      }
    })()
    return running
  }

  function trigger(force = false) {
    runPass(force).catch(() => {})
  }

  /** Starts listening for the moments to send: app start, session changes, regained network, a timer. */
  function start() {
    syncScope()
    let lastToken = tokenOf()
    const unsubscribe = store.subscribe(() => {
      const changed = syncScope()
      const token = tokenOf()
      if (changed || token !== lastToken) {
        lastToken = token
        if (token) trigger()
      }
    })
    const onOnline = () => trigger(true)
    window.addEventListener('online', onOnline)
    const timer = setInterval(() => {
      if (items.some(isSendable)) trigger()
    }, intervalMs)
    if (lastToken) trigger()
    return () => {
      unsubscribe()
      window.removeEventListener('online', onOnline)
      clearInterval(timer)
    }
  }

  return {
    subscribe(listener: () => void) {
      listeners.add(listener)
      return () => {
        listeners.delete(listener)
      }
    },
    getSnapshot: () => snapshot,
    /** The queued photo (with its blob, or its server id once uploaded) behind a `local-` id. */
    getUpload: (localId: string) => uploadFor(localId),
    /** True while any change for the task (other than a hidden uploaded photo) is queued. */
    hasQueued: (taskId: string) => items.some((i) => i.taskId === taskId && !isUploaded(i)),
    start,
    /** Runs a pass that respects backoff; resolves when it has finished. */
    process: () => runPass(false),
    /** Runs a pass now, ignoring backoff; resolves when it has finished. */
    syncNow: () => runPass(true),
    enqueueUpload,
    enqueueProgress,
    enqueueComplete,
    enqueueEntry,
    dropProgress,
    plan,
    retry,
    retryAll,
    discard,
    clearScope,
    /** Resolves once the saved queue has been read and pending writes have settled (tests, sign-out). */
    async settled() {
      syncScope()
      await ready
      await writes
    },
  }
}

export type Outbox = ReturnType<typeof createOutbox>
