import { getRouteApi, Link, useRouter } from '@tanstack/react-router'
import { AlarmClock, ArrowLeft, Clock, MapPin } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { formatDay, formatTime } from '@/lib/dates'

import { statusLabel } from './status'
import type { TaskDetail } from './types'

const route = getRouteApi('/_authenticated/tasks_/$taskId')

const BACK_CLASS =
  '-ml-2 inline-flex h-11 w-fit items-center gap-1 rounded-md px-2 text-base font-medium text-muted-foreground'

/** Back to where the user came from: the Tasks list with its filters, or Today. */
function BackLink() {
  const { from } = route.useSearch()
  const router = useRouter()
  const toList = from?.startsWith('/tasks')
  if (!from) {
    return (
      <Link to="/today" className={BACK_CLASS}>
        <ArrowLeft className="size-5" aria-hidden />
        Today
      </Link>
    )
  }
  return (
    <a
      href={from}
      onClick={(e) => {
        e.preventDefault()
        router.history.push(from)
      }}
      className={BACK_CLASS}
    >
      <ArrowLeft className="size-5" aria-hidden />
      {toList ? 'Tasks' : 'Today'}
    </a>
  )
}

const KIND_LABEL = { Checklist: 'Checklist', RecordLog: 'Record log', Form: 'Form' } as const

export function TaskHeader({ task }: { task: TaskDetail }) {
  const label = statusLabel(task)
  const bad = task.status === 'Missed' || task.isOverdue
  return (
    <header className="flex flex-col gap-3">
      <BackLink />
      <div>
        <h1 className="text-2xl leading-tight font-semibold tracking-tight">{task.title}</h1>
        <p className="mt-1 flex items-center gap-1.5 text-sm text-muted-foreground">
          <MapPin className="size-4" aria-hidden />
          {task.siteName}
        </p>
        <p className="mt-0.5 flex items-center gap-1.5 text-sm text-muted-foreground">
          <Clock className="size-4" aria-hidden />
          Due {formatTime(task.dueAt, task.timeZone)} · {formatDay(task.dueDate)}
        </p>
      </div>
      <div className="flex flex-wrap gap-1.5">
        <Badge variant="secondary">{KIND_LABEL[task.kind]}</Badge>
        <Badge variant={bad ? 'destructive' : 'outline'} data-testid="status-badge">
          {task.isOverdue && <AlarmClock aria-hidden />}
          {label}
        </Badge>
        {task.completedLate && <Badge variant="outline">Completed late</Badge>}
        {task.hasFailures && <Badge variant="destructive">Failed checks</Badge>}
        {task.snoozeCount > 0 && <Badge variant="outline">Snoozed {task.snoozeCount}×</Badge>}
      </div>
      {task.description && <p className="text-base whitespace-pre-wrap">{task.description}</p>}
    </header>
  )
}
