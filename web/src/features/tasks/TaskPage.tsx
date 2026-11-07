import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useEffect, useState } from 'react'

import { ApiError, errorMessage } from '@/api/problem'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { formatTime } from '@/lib/dates'

import { ChecklistForm } from './ChecklistForm'
import { taskKey, taskQuery } from './queries'
import { AnswerList } from './ReadOnly'
import { RecordLogView } from './RecordLogView'
import { TaskActions } from './TaskActions'
import { TaskHeader } from './TaskHeader'
import type { TaskDetail } from './types'
import { workability } from './workability'

function useNow(intervalMs = 30_000) {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), intervalMs)
    return () => clearInterval(id)
  }, [intervalMs])
  return now
}

export function TaskPage({ taskId }: { taskId: string }) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const query = useQuery({ ...taskQuery(client, organizationId, taskId) })

  if (query.isPending) return <TaskSkeleton />
  if (query.isError) {
    const notFound = query.error instanceof ApiError && query.error.status === 404
    return (
      <div role="alert" className="flex flex-col items-center gap-3 py-12 text-center">
        <p>{notFound ? 'This task could not be found.' : errorMessage(query.error)}</p>
        {!notFound && (
          <Button variant="outline" onClick={() => void query.refetch()}>
            Try again
          </Button>
        )}
        <Button asChild variant="ghost">
          <Link to="/today">Back to Today</Link>
        </Button>
      </div>
    )
  }
  return <TaskScreen task={query.data} />
}

function TaskScreen({ task }: { task: TaskDetail }) {
  const canComplete = usePermission(Permissions.Tasks.Complete)
  const now = useNow()
  const queryClient = useQueryClient()
  const organizationId = useSession().organization?.id
  const [notice, setNotice] = useState<string>()
  const state = workability(task, now, canComplete)

  function onConflict(message: string) {
    setNotice(message)
    void queryClient.invalidateQueries({ queryKey: taskKey(organizationId, task.id) })
  }

  return (
    <article className="flex flex-col gap-6" aria-label={task.title}>
      <TaskHeader task={task} />

      {notice && (
        <div
          role="alert"
          className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
        >
          {notice}
        </div>
      )}
      {state.message && (
        <p
          className="rounded-md border bg-muted p-3 text-sm"
          role="status"
          data-testid="workability"
        >
          {state.message}
        </p>
      )}
      {task.status === 'Completed' && <CompletionSummary task={task} />}
      {task.status === 'Open' && task.approvalNote && (
        <p className="rounded-md border border-warning/50 bg-warning/10 p-3 text-sm">
          <span className="font-medium">Sent back for changes:</span> {task.approvalNote}
        </p>
      )}

      {task.kind === 'RecordLog' ? (
        <RecordLogView task={task} editable={state.workable} onConflict={onConflict} />
      ) : state.workable ? (
        // Re-created when the task changes status (e.g. reopened) so the draft is loaded again.
        <ChecklistForm key={`${task.id}:${task.status}`} task={task} onConflict={onConflict} />
      ) : (
        <SubmittedAnswers task={task} />
      )}

      <TaskActions task={task} />
    </article>
  )
}

function SubmittedAnswers({ task }: { task: TaskDetail }) {
  const entry = task.entries.find((e) => e.submittedAt) ?? task.entries[0]
  if (!entry) return null
  return (
    <section aria-label="Answers" className="flex flex-col gap-3">
      <h2 className="text-lg font-semibold">Answers</h2>
      <AnswerList fields={task.fields} entry={entry} />
    </section>
  )
}

function CompletionSummary({ task }: { task: TaskDetail }) {
  return (
    <div
      className="flex flex-col gap-1 rounded-xl border bg-card p-4 text-sm"
      data-testid="summary"
    >
      <p>
        <span className="font-medium">Completed</span>
        {task.completedAt && ` ${formatTime(task.completedAt, task.timeZone)}`}
        {task.completedBy && ` by ${task.completedBy}`}
        {task.completedLate && ' (late)'}
      </p>
      {task.approvedAt ? (
        <p>
          <span className="font-medium">Approved</span> {formatTime(task.approvedAt, task.timeZone)}
          {task.approvedBy && ` by ${task.approvedBy}`}
          {task.approvalNote && (
            <span className="block text-muted-foreground">“{task.approvalNote}”</span>
          )}
        </p>
      ) : task.isAwaitingApproval ? (
        <p className="text-muted-foreground">Waiting for a manager to approve this task.</p>
      ) : null}
    </div>
  )
}

function TaskSkeleton() {
  return (
    <div className="flex flex-col gap-4" role="status" aria-label="Loading task">
      <Skeleton className="h-8 w-24" />
      <Skeleton className="h-10 w-3/4" />
      <Skeleton className="h-28 w-full rounded-xl" />
      <Skeleton className="h-28 w-full rounded-xl" />
    </div>
  )
}
