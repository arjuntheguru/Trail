import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useState,
  useSyncExternalStore,
  type ReactNode,
} from 'react'

import type { Outbox } from './outbox'
import type { OutboxItem } from './outbox-types'

const OutboxContext = createContext<Outbox | null>(null)

export function OutboxProvider({ outbox, children }: { outbox: Outbox; children: ReactNode }) {
  return <OutboxContext.Provider value={outbox}>{children}</OutboxContext.Provider>
}

/** The outbox itself, for code that queues changes. */
export function useOutboxApi(): Outbox {
  const outbox = useContext(OutboxContext)
  if (!outbox) throw new Error('useOutboxApi must be used inside <OutboxProvider>.')
  return outbox
}

/** The queue's state and the actions people can take on it. */
export function useOutbox() {
  const outbox = useOutboxApi()
  const snapshot = useSyncExternalStore(outbox.subscribe, outbox.getSnapshot, outbox.getSnapshot)
  return useMemo(
    () => ({
      ...snapshot,
      retry: outbox.retry,
      discard: outbox.discard,
      retryAll: outbox.retryAll,
    }),
    [snapshot, outbox],
  )
}

/** Queued changes for one task, oldest first. */
export function usePendingForTask(taskId: string): readonly OutboxItem[] {
  const { items } = useOutbox()
  return useMemo(() => items.filter((i) => i.taskId === taskId), [items, taskId])
}

/** The queued upload behind a `local-` photo id: its bytes while waiting, or the server id once sent. */
export function useQueuedUpload(localId: string) {
  const outbox = useOutboxApi()
  return useSyncExternalStore(
    outbox.subscribe,
    () => outbox.getUpload(localId),
    () => outbox.getUpload(localId),
  )
}

/** An object URL for the queued photo's bytes, revoked on unmount. */
export function useBlobUrl(blob: Blob | null | undefined) {
  const [url, setUrl] = useState<string>()
  useEffect(() => {
    if (!blob) return
    const created = URL.createObjectURL(blob)
    // eslint-disable-next-line react-hooks/set-state-in-effect -- the URL is an external resource owned by this effect
    setUrl(created)
    return () => {
      URL.revokeObjectURL(created)
      setUrl(undefined)
    }
  }, [blob])
  return blob ? url : undefined
}
