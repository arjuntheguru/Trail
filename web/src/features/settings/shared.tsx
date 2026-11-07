import { Loader2 } from 'lucide-react'
import type { FormEventHandler, ReactNode } from 'react'
import { toast } from 'sonner'

import { errorMessage } from '@/api/problem'
import { usePermission } from '@/auth/context'
import { FormAlert } from '@/components/FormField'
import { Button } from '@/components/ui/button'
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'

import type { MembershipStatus } from './queries'
import { useSettingsMutation } from './useSettingsMutation'

export function SectionHeader({
  title,
  description,
  action,
}: {
  title: string
  description?: string
  action?: ReactNode
}) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div className="flex min-w-0 flex-col gap-1">
        <h2 className="text-xl font-semibold">{title}</h2>
        {description && <p className="text-sm text-muted-foreground">{description}</p>}
      </div>
      {action}
    </div>
  )
}

export function LoadError({ error, retry }: { error: unknown; retry: () => void }) {
  return (
    <div role="alert" className="flex flex-col items-center gap-3 py-10 text-center">
      <p>{errorMessage(error)}</p>
      <Button variant="outline" onClick={retry}>
        Try again
      </Button>
    </div>
  )
}

export function ListSkeleton({ rows = 4 }: { rows?: number }) {
  return (
    <div className="flex flex-col gap-2" role="status" aria-label="Loading">
      {Array.from({ length: rows }, (_, i) => (
        <Skeleton key={i} className="h-14 w-full" />
      ))}
    </div>
  )
}

export function EmptyState({ children }: { children: ReactNode }) {
  return (
    <p className="rounded-lg border border-dashed p-6 text-center text-muted-foreground">
      {children}
    </p>
  )
}

/** Explains why an action is unavailable instead of silently hiding it. */
export function Hint({ children }: { children: ReactNode }) {
  return <p className="text-sm text-muted-foreground">{children}</p>
}

/** Renders its children only for holders of the permission; others see why the page is empty. */
export function RequirePermission({
  permission,
  children,
}: {
  permission: string
  children: ReactNode
}) {
  if (!usePermission(permission)) {
    return (
      <p role="status" className="py-10 text-center text-muted-foreground">
        You do not have permission to view this section.
      </p>
    )
  }
  return children
}

/** A modal form. Mount it only while open so each opening starts from fresh form state. */
export function FormDialog({
  title,
  description,
  submitLabel,
  pending,
  errors,
  onSubmit,
  onClose,
  children,
  className,
  readOnly = false,
}: {
  title: string
  description?: string
  submitLabel: string
  pending: boolean
  errors: string[]
  onSubmit: FormEventHandler<HTMLFormElement>
  onClose: () => void
  children: ReactNode
  className?: string
  /** Shows the form for reading only: no submit button. */
  readOnly?: boolean
}) {
  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className={className}>
        <form onSubmit={onSubmit} noValidate className="grid gap-4">
          <DialogHeader>
            <DialogTitle>{title}</DialogTitle>
            <DialogDescription className={description ? undefined : 'sr-only'}>
              {description ?? title}
            </DialogDescription>
          </DialogHeader>
          {children}
          <FormAlert messages={errors} />
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {readOnly ? 'Close' : 'Cancel'}
            </Button>
            {!readOnly && (
              <Button type="submit" disabled={pending}>
                {pending && <Loader2 className="animate-spin" aria-hidden />}
                {submitLabel}
              </Button>
            )}
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/** Asks before a destructive or access-changing action, and shows the server's refusal inside the dialog. */
export function ConfirmDialog({
  title,
  description,
  confirmLabel,
  destructive = true,
  successMessage,
  areas,
  run,
  onClose,
  onDone,
}: {
  title: string
  description: ReactNode
  confirmLabel: string
  destructive?: boolean
  successMessage: string
  areas: readonly (readonly string[])[]
  run: () => Promise<unknown>
  onClose: () => void
  /** Called after success, before the dialog closes. */
  onDone?: () => void
}) {
  const mutation = useSettingsMutation({
    run,
    areas,
    onSuccess: () => {
      toast.success(successMessage)
      onDone?.()
      onClose()
    },
  })
  return (
    <AlertDialog open onOpenChange={(open) => !open && !mutation.isPending && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription asChild>
            <div>{description}</div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        {mutation.isError && <FormAlert messages={[errorMessage(mutation.error)]} />}
        <AlertDialogFooter>
          <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>
            Cancel
          </Button>
          <Button
            variant={destructive ? 'destructive' : 'default'}
            disabled={mutation.isPending}
            onClick={() => mutation.mutate()}
          >
            {mutation.isPending && <Loader2 className="animate-spin" aria-hidden />}
            {confirmLabel}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

export function StatusBadge({ status }: { status: MembershipStatus }) {
  return (
    <Badge
      variant={status === 'Active' ? 'default' : status === 'Invited' ? 'secondary' : 'outline'}
    >
      {status}
    </Badge>
  )
}
