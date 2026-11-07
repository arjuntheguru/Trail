import { Camera, ImagePlus, LoaderCircle, RotateCw, X } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'

import { errorMessage } from '@/api/problem'
import { useAuthService } from '@/auth/context'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

import {
  downscaleImage,
  MAX_UPLOAD_BYTES,
  uploadAttachment,
  useAttachmentUrl,
} from '@/features/tasks/attachments'

export function AttachmentImage({
  id,
  alt,
  className,
}: {
  id: string
  alt: string
  className?: string
}) {
  const { url, isError } = useAttachmentUrl(id)
  if (url) return <img src={url} alt={alt} className={cn('size-full object-cover', className)} />
  return (
    <div
      className={cn(
        'grid size-full place-items-center bg-muted text-xs text-muted-foreground',
        className,
      )}
      role={isError ? 'img' : 'status'}
      aria-label={isError ? `${alt} could not be loaded` : `Loading ${alt}`}
    >
      {isError ? 'Unavailable' : <LoaderCircle className="size-5 animate-spin" aria-hidden />}
    </div>
  )
}

/** Read-only strip of photo thumbnails. */
export function PhotoGallery({ ids, label = 'Photo' }: { ids: readonly string[]; label?: string }) {
  if (ids.length === 0) return null
  return (
    <ul className="flex flex-wrap gap-2">
      {ids.map((id, i) => (
        <li key={id} className="size-20 overflow-hidden rounded-lg border">
          <AttachmentImage id={id} alt={`${label} ${i + 1}`} />
        </li>
      ))}
    </ul>
  )
}

interface PendingUpload {
  key: number
  name: string
  file: File
  state: 'uploading' | 'error'
  message?: string
}

export interface PhotoPickerProps {
  value: string[]
  onChange: (ids: string[]) => void
  label: string
  max?: number
  /** Site the photos are stored against; the API only accepts photos uploaded for the record's own site. */
  siteId: string
  readOnly?: boolean
  /** Lets the owner of the form wait for uploads in flight (see `useUploadTracker`). */
  trackUpload?: <T>(upload: Promise<T>) => Promise<T>
}

const untracked = <T,>(upload: Promise<T>) => upload

/**
 * Camera capture (or library pick), a downscale to keep uploads small, then an upload that yields the photo id.
 * Thumbnails can be removed until the form is submitted.
 */
export function PhotoPicker({
  value,
  onChange,
  label,
  max = 10,
  siteId,
  readOnly = false,
  trackUpload = untracked,
}: PhotoPickerProps) {
  const { client } = useAuthService()
  const [pending, setPending] = useState<PendingUpload[]>([])
  const camera = useRef<HTMLInputElement>(null)
  const library = useRef<HTMLInputElement>(null)
  const counter = useRef(0)
  // Uploads finish at different times; each appends to the latest list rather than to a stale render's.
  const latest = useRef(value)
  useEffect(() => {
    latest.current = value
  }, [value])

  const full = value.length + pending.length >= max

  async function upload(item: PendingUpload) {
    setPending((list) => list.map((p) => (p.key === item.key ? { ...p, state: 'uploading' } : p)))
    try {
      const prepared = await trackUpload(
        (async () => {
          const file = await downscaleImage(item.file)
          if (file.size > MAX_UPLOAD_BYTES) throw new Error('This photo is larger than 10 MB.')
          return uploadAttachment(client, {
            file,
            fileName: file.name,
            kind: 'Photo',
            siteId,
          })
        })(),
      )
      latest.current = [...latest.current, prepared.id]
      onChange(latest.current)
      setPending((list) => list.filter((p) => p.key !== item.key))
    } catch (error) {
      setPending((list) =>
        list.map((p) =>
          p.key === item.key ? { ...p, state: 'error', message: errorMessage(error) } : p,
        ),
      )
    }
  }

  function pick(files: FileList | null) {
    const room = max - value.length - pending.length
    const items = Array.from(files ?? [])
      .slice(0, Math.max(room, 0))
      .map((file) => ({
        key: ++counter.current,
        name: file.name,
        file,
        state: 'uploading' as const,
      }))
    if (items.length === 0) return
    setPending((list) => [...list, ...items])
    items.forEach((item) => void upload(item))
  }

  function remove(id: string) {
    latest.current = latest.current.filter((x) => x !== id)
    onChange(latest.current)
  }

  return (
    <div className="flex flex-col gap-2" data-testid="photo-picker">
      {(value.length > 0 || pending.length > 0) && (
        <ul className="flex flex-wrap gap-2">
          {value.map((id, i) => (
            <li key={id} className="relative size-20 rounded-lg border">
              <div className="size-full overflow-hidden rounded-lg">
                <AttachmentImage id={id} alt={`${label} ${i + 1}`} />
              </div>
              {!readOnly && (
                <button
                  type="button"
                  aria-label={`Remove ${label} ${i + 1}`}
                  onClick={() => remove(id)}
                  className="absolute -top-2 -right-2 grid size-9 place-items-center rounded-full border bg-background shadow"
                >
                  <X className="size-4" aria-hidden />
                </button>
              )}
            </li>
          ))}
          {pending.map((p) => (
            <li
              key={p.key}
              className="relative grid size-20 place-items-center rounded-lg border bg-muted p-1 text-center text-[11px]"
            >
              {p.state === 'uploading' ? (
                <span role="status" className="flex flex-col items-center gap-1">
                  <LoaderCircle className="size-5 animate-spin" aria-hidden />
                  Uploading…
                </span>
              ) : (
                <span role="alert" className="flex flex-col items-center gap-1 text-destructive">
                  Failed
                  <button
                    type="button"
                    className="grid size-9 place-items-center"
                    aria-label={`Retry upload of ${p.name}`}
                    onClick={() => void upload(p)}
                  >
                    <RotateCw className="size-4" aria-hidden />
                  </button>
                </span>
              )}
              {p.state === 'error' && (
                <button
                  type="button"
                  aria-label={`Discard ${p.name}`}
                  onClick={() => setPending((list) => list.filter((x) => x.key !== p.key))}
                  className="absolute -top-2 -right-2 grid size-9 place-items-center rounded-full border bg-background shadow"
                >
                  <X className="size-4" aria-hidden />
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
      {pending.some((p) => p.state === 'error') && (
        <p role="alert" className="text-sm text-destructive">
          {pending.find((p) => p.state === 'error')?.message}
        </p>
      )}
      {!readOnly && (
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            variant="outline"
            size="lg"
            disabled={full}
            onClick={() => camera.current?.click()}
          >
            <Camera aria-hidden />
            Take photo
          </Button>
          <Button
            type="button"
            variant="ghost"
            size="lg"
            disabled={full}
            onClick={() => library.current?.click()}
          >
            <ImagePlus aria-hidden />
            Choose from library
          </Button>
          <input
            ref={camera}
            type="file"
            accept="image/*"
            capture="environment"
            hidden
            aria-label={`${label} camera`}
            data-testid="photo-input-camera"
            onChange={(e) => {
              pick(e.target.files)
              e.target.value = ''
            }}
          />
          <input
            ref={library}
            type="file"
            accept="image/*"
            multiple
            hidden
            aria-label={`${label} library`}
            data-testid="photo-input-library"
            onChange={(e) => {
              pick(e.target.files)
              e.target.value = ''
            }}
          />
        </div>
      )}
    </div>
  )
}
