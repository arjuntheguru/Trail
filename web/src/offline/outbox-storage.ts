import { createStore, del, delMany, entries, set, type UseStore } from 'idb-keyval'

import type { OutboxItem, OutboxScope, UploadItem } from './outbox-types'

/** Where the queue lives between page loads. */
export interface OutboxStorage {
  load(scope: OutboxScope): Promise<OutboxItem[]>
  save(item: OutboxItem): Promise<void>
  remove(item: Pick<OutboxItem, 'id' | 'userId' | 'organizationId'>): Promise<void>
  clear(scope: OutboxScope): Promise<void>
}

/** Blobs are stored as raw bytes: older mobile Safari versions mishandle Blob values in IndexedDB. */
type StoredItem =
  | Exclude<OutboxItem, UploadItem>
  | (Omit<UploadItem, 'blob'> & { data: ArrayBuffer | null; contentType: string })

const prefixOf = (scope: OutboxScope) => `${scope.userId}:${scope.organizationId}:`
const keyOf = (item: Pick<OutboxItem, 'id' | 'userId' | 'organizationId'>) =>
  `${prefixOf(item)}${item.id}`

async function toStored(item: OutboxItem): Promise<StoredItem> {
  if (item.type !== 'upload') return item
  const { blob, ...rest } = item
  return { ...rest, data: blob ? await blob.arrayBuffer() : null, contentType: blob?.type ?? '' }
}

function fromStored(stored: StoredItem): OutboxItem {
  if (stored.type !== 'upload') return stored
  const { data, contentType, ...rest } = stored
  return { ...rest, blob: data ? new Blob([data], { type: contentType }) : null }
}

/** The queue in its own IndexedDB database (separate from the query cache, which is disposable). */
export function createIdbOutboxStorage(dbName = 'trail-outbox'): OutboxStorage {
  let idb: UseStore | undefined
  // Created on first use: the database is not touched in environments without IndexedDB.
  const db = () => (idb ??= createStore(dbName, 'items'))

  return {
    async load(scope) {
      const prefix = prefixOf(scope)
      const all = await entries<string, StoredItem>(db())
      return all
        .filter(([key]) => key.startsWith(prefix))
        .map(([, value]) => fromStored(value))
        .sort((a, b) => a.seq - b.seq)
    },
    async save(item) {
      await set(keyOf(item), await toStored(item), db())
    },
    remove: (item) => del(keyOf(item), db()),
    async clear(scope) {
      const prefix = prefixOf(scope)
      const keys = (await entries<string, StoredItem>(db()))
        .map(([key]) => key)
        .filter((key) => key.startsWith(prefix))
      await delMany(keys, db())
    },
  }
}
