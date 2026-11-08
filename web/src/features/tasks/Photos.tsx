import { useCallback } from 'react'

import {
  PhotoPicker as SharedPhotoPicker,
  type PhotoPickerProps,
  type QueuedPhoto,
} from '@/components/Photos'
import { useOutboxApi } from '@/offline/context'

import { useTaskForm } from './form-context'

export { AttachmentImage, PhotoGallery } from '@/components/Photos'

/** The shared photo picker wired to the task form (site, read-only state, upload tracking and the outbox). */
export function PhotoPicker(
  props: Omit<PhotoPickerProps, 'siteId' | 'readOnly' | 'trackUpload' | 'queueUpload'>,
) {
  const { siteId, taskId, readOnly, trackUpload } = useTaskForm()
  const outbox = useOutboxApi()
  const queueUpload = useCallback(
    (photo: QueuedPhoto) => outbox.enqueueUpload({ ...photo, taskId: taskId! }),
    [outbox, taskId],
  )
  return (
    <SharedPhotoPicker
      {...props}
      siteId={siteId}
      readOnly={readOnly}
      trackUpload={trackUpload}
      queueUpload={taskId ? queueUpload : undefined}
    />
  )
}
