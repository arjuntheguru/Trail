import { Eraser, PenLine } from 'lucide-react'
import { useRef, useState } from 'react'

import { errorMessage } from '@/api/problem'
import { useAuthService } from '@/auth/context'
import { Button } from '@/components/ui/button'

import { uploadAttachment } from './attachments'
import { useTaskForm } from './form-context'
import { AttachmentImage } from './Photos'

interface SignaturePadProps {
  /** Id of the uploaded signature image, or '' when none. */
  value: string
  onChange: (id: string) => void
  label: string
}

/** Draw with a finger or pen; saving uploads the drawing as a PNG with the Signature kind and keeps its id. */
export function SignaturePad({ value, onChange, label }: SignaturePadProps) {
  const { client } = useAuthService()
  const { siteId, readOnly, trackUpload } = useTaskForm()
  const canvas = useRef<HTMLCanvasElement>(null)
  const drawing = useRef(false)
  const [hasInk, setHasInk] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string>()

  function point(e: React.PointerEvent<HTMLCanvasElement>) {
    const el = canvas.current!
    const rect = el.getBoundingClientRect()
    return {
      x: ((e.clientX - rect.left) / rect.width) * el.width,
      y: ((e.clientY - rect.top) / rect.height) * el.height,
    }
  }

  function start(e: React.PointerEvent<HTMLCanvasElement>) {
    const ctx = canvas.current?.getContext('2d')
    if (!ctx) return
    e.currentTarget.setPointerCapture?.(e.pointerId)
    drawing.current = true
    const { x, y } = point(e)
    ctx.lineWidth = 4
    ctx.lineCap = 'round'
    ctx.strokeStyle = '#111'
    ctx.beginPath()
    ctx.moveTo(x, y)
    ctx.lineTo(x + 0.1, y + 0.1)
    ctx.stroke()
    setHasInk(true)
  }

  function move(e: React.PointerEvent<HTMLCanvasElement>) {
    if (!drawing.current) return
    const ctx = canvas.current?.getContext('2d')
    if (!ctx) return
    const { x, y } = point(e)
    ctx.lineTo(x, y)
    ctx.stroke()
  }

  function clear() {
    const el = canvas.current
    el?.getContext('2d')?.clearRect(0, 0, el.width, el.height)
    setHasInk(false)
    setError(undefined)
  }

  async function save() {
    const el = canvas.current
    if (!el) return
    setSaving(true)
    setError(undefined)
    try {
      const blob = await new Promise<Blob | null>((resolve) => el.toBlob(resolve, 'image/png'))
      if (!blob) throw new Error('The signature could not be read.')
      const attachment = await trackUpload(
        uploadAttachment(client, {
          file: blob,
          fileName: 'signature.png',
          kind: 'Signature',
          siteId,
        }),
      )
      onChange(attachment.id)
      clear()
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      setSaving(false)
    }
  }

  if (value) {
    return (
      <div className="flex flex-col gap-2">
        <div className="h-32 w-full max-w-sm overflow-hidden rounded-lg border bg-white">
          <AttachmentImage id={value} alt={`${label} signature`} className="object-contain" />
        </div>
        {!readOnly && (
          <Button type="button" variant="outline" size="lg" onClick={() => onChange('')}>
            <PenLine aria-hidden />
            Sign again
          </Button>
        )}
      </div>
    )
  }

  if (readOnly) return <p className="text-sm text-muted-foreground">Not signed</p>

  return (
    <div className="flex flex-col gap-2">
      <canvas
        ref={canvas}
        width={600}
        height={240}
        aria-label={`${label} signature pad`}
        className="h-40 w-full max-w-sm touch-none rounded-lg border bg-white"
        onPointerDown={start}
        onPointerMove={move}
        onPointerUp={() => (drawing.current = false)}
        onPointerCancel={() => (drawing.current = false)}
      />
      <div className="flex gap-2">
        <Button type="button" variant="outline" size="lg" onClick={clear} disabled={!hasInk}>
          <Eraser aria-hidden />
          Clear
        </Button>
        <Button type="button" size="lg" onClick={() => void save()} disabled={!hasInk || saving}>
          {saving ? 'Saving…' : 'Use signature'}
        </Button>
      </div>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}
