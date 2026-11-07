import { Check, Copy, TriangleAlert } from 'lucide-react'
import { useState } from 'react'

import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'

/**
 * Shows a credential exactly once. The value lives only in the parent's state while this dialog is mounted;
 * it is never put in a query cache, and closing the dialog is the only way out.
 */
export function SecretDialog({
  title,
  label,
  value,
  onClose,
}: {
  title: string
  label: string
  value: string
  onClose: () => void
}) {
  const [copied, setCopied] = useState<'done' | 'failed' | null>(null)
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value)
      setCopied('done')
    } catch {
      setCopied('failed')
    }
  }
  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>Copy it now and store it somewhere safe.</DialogDescription>
        </DialogHeader>
        <div
          role="alert"
          className="flex gap-2 rounded-md border border-amber-500/40 bg-amber-500/10 p-3 text-sm"
        >
          <TriangleAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
          <p>
            <strong>You won&apos;t see this again.</strong> If you lose it you will have to create a
            new one.
          </p>
        </div>
        <div className="flex flex-col gap-2">
          <span className="text-sm font-medium">{label}</span>
          <code
            data-testid="secret-value"
            className="rounded-md border bg-muted p-3 font-mono text-sm break-all select-all"
          >
            {value}
          </code>
          {copied === 'failed' && (
            <p role="status" className="text-sm text-destructive">
              Could not copy automatically. Select the text and copy it by hand.
            </p>
          )}
        </div>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => void copy()}>
            {copied === 'done' ? <Check aria-hidden /> : <Copy aria-hidden />}
            {copied === 'done' ? 'Copied' : 'Copy'}
          </Button>
          <Button type="button" onClick={onClose}>
            I have saved it
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
