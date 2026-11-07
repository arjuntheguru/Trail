import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { AlarmClockPlus, RotateCcw, ShieldCheck } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'

import { errorMessage } from '@/api/problem'
import { unwrap } from '@/api/unwrap'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { FormAlert } from '@/components/FormField'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { formatDay } from '@/lib/dates'

import { isConflict } from './errors'
import { applyTaskUpdate, taskKey } from './queries'
import type { TaskDetail } from './types'
import { addDays } from './workability'

/** The server allows snoozing up to 14 days ahead of the site's today. */
const SNOOZE_CHOICES = [
  { label: 'Tomorrow', days: 1 },
  { label: 'In 2 days', days: 2 },
  { label: 'Next week', days: 7 },
] as const

/** Snooze for staff; approve and reopen for people who hold the approval permission. */
export function TaskActions({ task }: { task: TaskDetail }) {
  const canComplete = usePermission(Permissions.Tasks.Complete)
  const canApprove = usePermission(Permissions.Tasks.Approve)
  const userId = useSession().user.id

  const canSnooze = canComplete && task.status === 'Open'
  const canApproveNow =
    canApprove && task.isAwaitingApproval && !!task.completedById && task.completedById !== userId
  const ownCompletion =
    canApprove && task.isAwaitingApproval && !!task.completedById && task.completedById === userId
  const canReopen = canApprove && task.status === 'Completed' && !task.approvedAt

  if (!canSnooze && !canApproveNow && !ownCompletion && !canReopen) return null

  return (
    <section aria-label="Actions" className="flex flex-col gap-3 border-t pt-4">
      {ownCompletion && (
        <p className="text-sm text-muted-foreground">
          You completed this task, so someone else needs to approve it.
        </p>
      )}
      {canApproveNow && <ApprovePanel task={task} />}
      {canReopen && <ReopenPanel task={task} />}
      {canSnooze && <SnoozePanel task={task} />}
    </section>
  )
}

/** Runs a task action, stores the response, and on a conflict reloads the task so the screen shows the truth. */
function useTaskAction<TInput>(
  task: TaskDetail,
  run: (input: TInput) => Promise<TaskDetail>,
  onSuccess: (updated: TaskDetail) => void,
) {
  const queryClient = useQueryClient()
  const organizationId = useSession().organization?.id
  const [error, setError] = useState<string>()
  const mutation = useMutation({
    mutationFn: run,
    onMutate: () => setError(undefined),
    onSuccess: (updated) => {
      applyTaskUpdate(queryClient, organizationId, updated)
      onSuccess(updated)
    },
    onError: (e) => {
      setError(errorMessage(e))
      if (isConflict(e)) {
        void queryClient.invalidateQueries({ queryKey: taskKey(organizationId, task.id) })
      }
    },
  })
  return { mutation, error }
}

function ApprovePanel({ task }: { task: TaskDetail }) {
  const { client } = useAuthService()
  const [note, setNote] = useState('')
  const { mutation, error } = useTaskAction(
    task,
    () =>
      unwrap(
        client.POST('/api/v1/tasks/{id}/approve', {
          params: { path: { id: task.id } },
          body: { note: note.trim() || null },
        }),
      ),
    () => toast.success('Task approved.'),
  )
  return (
    <div className="flex flex-col gap-2 rounded-xl border p-4">
      <h2 className="font-medium">Approve</h2>
      <Label htmlFor="approve-note" className="text-sm">
        Note (optional)
      </Label>
      <Textarea
        id="approve-note"
        value={note}
        onChange={(e) => setNote(e.target.value)}
        className="min-h-20"
      />
      {error && <FormAlert messages={[error]} />}
      <Button size="lg" disabled={mutation.isPending} onClick={() => mutation.mutate(undefined)}>
        <ShieldCheck aria-hidden />
        Approve task
      </Button>
    </div>
  )
}

function ReopenPanel({ task }: { task: TaskDetail }) {
  const { client } = useAuthService()
  const [reason, setReason] = useState('')
  const [missing, setMissing] = useState(false)
  const { mutation, error } = useTaskAction(
    task,
    () =>
      unwrap(
        client.POST('/api/v1/tasks/{id}/reopen', {
          params: { path: { id: task.id } },
          body: { reason: reason.trim() },
        }),
      ),
    () => toast.success('Task reopened.'),
  )
  return (
    <div className="flex flex-col gap-2 rounded-xl border p-4">
      <h2 className="font-medium">Reopen</h2>
      <Label htmlFor="reopen-reason" className="text-sm">
        Why does it need redoing?
      </Label>
      <Textarea
        id="reopen-reason"
        value={reason}
        aria-invalid={missing ? true : undefined}
        onChange={(e) => {
          setReason(e.target.value)
          setMissing(false)
        }}
        className="min-h-20"
      />
      {missing && (
        <p role="alert" className="text-sm text-destructive">
          Give a reason so the team knows what to fix.
        </p>
      )}
      {error && <FormAlert messages={[error]} />}
      <Button
        size="lg"
        variant="outline"
        disabled={mutation.isPending}
        onClick={() => (reason.trim() ? mutation.mutate(undefined) : setMissing(true))}
      >
        <RotateCcw aria-hidden />
        Reopen task
      </Button>
    </div>
  )
}

function SnoozePanel({ task }: { task: TaskDetail }) {
  const { client } = useAuthService()
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const [custom, setCustom] = useState('')
  const earliest = addDays(task.dueDate, 1)
  const { mutation, error } = useTaskAction(
    task,
    (date: string) =>
      unwrap(
        client.POST('/api/v1/tasks/{id}/snooze', {
          params: { path: { id: task.id } },
          body: { date },
        }),
      ),
    (updated) => {
      toast.success(`Snoozed to ${formatDay(updated.dueDate)}.`)
      void navigate({ to: '/today' })
    },
  )

  if (!open) {
    return (
      <Button variant="outline" size="lg" onClick={() => setOpen(true)}>
        <AlarmClockPlus aria-hidden />
        Snooze
      </Button>
    )
  }
  return (
    <div className="flex flex-col gap-3 rounded-xl border p-4">
      <h2 className="font-medium">Snooze until</h2>
      <div className="flex flex-wrap gap-2">
        {SNOOZE_CHOICES.map(({ label, days }) => (
          <Button
            key={label}
            variant="outline"
            size="lg"
            disabled={mutation.isPending}
            onClick={() => mutation.mutate(addDays(task.dueDate, days))}
          >
            {label}
          </Button>
        ))}
      </div>
      <div className="flex items-end gap-2">
        <div className="flex flex-1 flex-col gap-1.5">
          <Label htmlFor="snooze-date" className="text-sm">
            Pick a day
          </Label>
          <Input
            id="snooze-date"
            type="date"
            min={earliest}
            max={addDays(task.dueDate, 14)}
            value={custom}
            onChange={(e) => setCustom(e.target.value)}
            className="h-12"
          />
        </div>
        <Button
          size="lg"
          disabled={!custom || mutation.isPending}
          onClick={() => mutation.mutate(custom)}
        >
          Snooze to this day
        </Button>
      </div>
      {error && <FormAlert messages={[error]} />}
      <Button variant="ghost" onClick={() => setOpen(false)}>
        Cancel
      </Button>
    </div>
  )
}
