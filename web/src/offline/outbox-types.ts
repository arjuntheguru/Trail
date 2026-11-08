import type { components } from '@/api/schema'
import type { AttachmentKind } from '@/features/tasks/attachments'

type Schemas = components['schemas']

/** A photo that only exists on this device has an id with this prefix until its upload succeeds. */
export const LOCAL_ID_PREFIX = 'local-'
export const isLocalId = (id: string) => id.startsWith(LOCAL_ID_PREFIX)

export type ProgressBody = Schemas['SaveTaskProgressCommand']
/** What the person entered; the outbox adds `requestId` and `completedAt` when it sends. */
export type CompleteBody = Omit<Schemas['CompleteTaskCommand'], 'requestId' | 'completedAt'>
/** What the person entered; the outbox adds `requestId` and `recordedAt` when it sends. */
export type EntryBody = Omit<Schemas['AddTaskEntryCommand'], 'requestId' | 'recordedAt'>

/** Identifies whose queue this is: one queue per user and organization, so sessions never mix. */
export interface OutboxScope {
  userId: string
  organizationId: string
}

export interface OutboxError {
  code?: string
  /** Already worded for the person. */
  message: string
}

interface ItemBase extends OutboxScope {
  id: string
  /** Monotonic ordering key (FIFO). */
  seq: number
  taskId: string
  createdAt: number
  attempts: number
  status: 'pending' | 'failed'
  error?: OutboxError
  /** Epoch ms before which the item is not sent again (backoff). */
  nextAttemptAt: number
}

export interface UploadItem extends ItemBase {
  type: 'upload'
  /** Equals `id`; the photo id forms use until the upload succeeds. */
  localId: string
  /** Generated once so a retry after a lost response returns the same attachment. */
  requestId: string
  /** The downscaled image; dropped once uploaded. */
  blob: Blob | null
  kind: AttachmentKind
  siteId: string
  fileName: string
  serverId?: string
  uploadedAt?: number
}

export interface ProgressItem extends ItemBase {
  type: 'progress'
  body: ProgressBody
}

export interface CompleteItem extends ItemBase {
  type: 'complete'
  requestId: string
  /** When the person pressed Complete (ISO instant). */
  completedAt: string
  body: CompleteBody
}

export interface EntryItem extends ItemBase {
  type: 'entry'
  requestId: string
  /** When the person added the reading (ISO instant). */
  recordedAt: string
  body: EntryBody
}

export type OutboxItem = UploadItem | ProgressItem | CompleteItem | EntryItem

/** An upload whose bytes reached the server; kept only so local ids can still be mapped. */
export const isUploaded = (item: OutboxItem): item is UploadItem & { serverId: string } =>
  item.type === 'upload' && !!item.serverId
