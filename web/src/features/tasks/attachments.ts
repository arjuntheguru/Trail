import { useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'

import type { ApiClient } from '@/api/client'
import type { components } from '@/api/schema'
import { unwrap } from '@/api/unwrap'
import { useAuthService, useSession } from '@/auth/context'

import { attachmentBlobQuery } from './queries'

export type AttachmentKind = components['schemas']['AttachmentKind']

/** The API rejects files over 10 MB (Attachment.MaxBytes). */
export const MAX_UPLOAD_BYTES = 10 * 1024 * 1024

const MAX_EDGE = 2000
const JPEG_QUALITY = 0.85
/** Photos already this small and within the size cap are uploaded as taken. */
const KEEP_AS_IS_BYTES = 1_500_000

export interface UploadInput {
  file: Blob
  fileName: string
  kind: AttachmentKind
  siteId: string
}

/** Uploads one file as multipart form data and resolves with the stored attachment. */
export function uploadAttachment(client: ApiClient, { file, fileName, kind, siteId }: UploadInput) {
  return unwrap(
    client.POST('/api/v1/attachments', {
      // The generated type models the file part as a string; the serializer below builds the real form.
      body: { file: fileName, kind, siteId },
      bodySerializer: () => {
        const form = new FormData()
        form.append('file', file, fileName)
        form.append('kind', kind)
        form.append('siteId', siteId)
        return form
      },
    }),
  )
}

/**
 * Shrinks a camera photo to at most 2000 px on its longest edge as JPEG so phone photos stay well under the
 * 10 MB limit. Anything the browser cannot decode (e.g. HEIC outside Safari) is sent unchanged.
 */
export async function downscaleImage(file: File): Promise<File> {
  if (!file.type.startsWith('image/')) return file
  try {
    const bitmap = await createImageBitmap(file)
    const scale = Math.min(1, MAX_EDGE / Math.max(bitmap.width, bitmap.height))
    if (scale === 1 && file.size <= KEEP_AS_IS_BYTES) {
      bitmap.close()
      return file
    }
    const canvas = document.createElement('canvas')
    canvas.width = Math.round(bitmap.width * scale)
    canvas.height = Math.round(bitmap.height * scale)
    canvas.getContext('2d')?.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
    bitmap.close()
    const blob = await new Promise<Blob | null>((resolve) =>
      canvas.toBlob(resolve, 'image/jpeg', JPEG_QUALITY),
    )
    if (!blob || (blob.size >= file.size && file.size <= MAX_UPLOAD_BYTES)) return file
    return new File([blob], file.name.replace(/\.[^.]+$/, '') + '.jpg', { type: 'image/jpeg' })
  } catch {
    return file
  }
}

/**
 * An object URL for an attachment's image, or undefined while it loads. The bytes are fetched through the
 * authenticated client (an <img src> cannot send the bearer token) and the URL is revoked on unmount.
 */
export function useAttachmentUrl(attachmentId: string | undefined) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const query = useQuery({
    ...attachmentBlobQuery(client, organizationId, attachmentId ?? ''),
    enabled: !!attachmentId,
  })
  const blob = query.data
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

  return { url: blob ? url : undefined, isError: query.isError, isPending: query.isPending }
}
