import { PhotoPicker as SharedPhotoPicker, type PhotoPickerProps } from '@/components/Photos'

import { useTaskForm } from './form-context'

export { AttachmentImage, PhotoGallery } from '@/components/Photos'

/** The shared photo picker wired to the task form (site, read-only state and upload tracking). */
export function PhotoPicker(props: Omit<PhotoPickerProps, 'siteId' | 'readOnly' | 'trackUpload'>) {
  const { siteId, readOnly, trackUpload } = useTaskForm()
  return (
    <SharedPhotoPicker {...props} siteId={siteId} readOnly={readOnly} trackUpload={trackUpload} />
  )
}
