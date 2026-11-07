import { getRouteApi, Link, useRouter } from '@tanstack/react-router'
import { AlarmClock, CircleCheck, Clock, MapPin } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { formatTime } from '@/lib/dates'

import { listStatusLabel } from './status'
import type { TaskListItem } from './types'

const KIND_LABEL: Record<TaskListItem['kind'], string> = {
  Checklist: 'Checklist',
  RecordLog: 'Record log',
  Form: 'Form',
}

const listRoute = getRouteApi('/_authenticated/tasks')

export function TaskListRow({ task, timeZone }: { task: TaskListItem; timeZone?: string }) {
  // The task screen's back link returns to this list with its filters.
  const router = useRouter()
  const search = listRoute.useSearch()
  const from = router.buildLocation({ to: '/tasks', search }).href
  const label = listStatusLabel(task)
  const bad = task.status === 'Missed' || task.isOverdue
  return (
    <Link
      to="/tasks/$taskId"
      params={{ taskId: task.id }}
      search={{ from }}
      className="block rounded-xl outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
    >
      <Card className="gap-2 p-4 transition-colors hover:bg-accent/40" data-testid="task-row">
        <h3 className="leading-snug font-medium">{task.title}</h3>
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
          <span className="inline-flex items-center gap-1">
            <MapPin className="size-4" aria-hidden />
            {task.siteName}
          </span>
          <span className="inline-flex items-center gap-1">
            <Clock className="size-4" aria-hidden />
            Due {formatTime(task.dueAt, timeZone)}
          </span>
          {task.completedAt && (
            <span className="inline-flex items-center gap-1">
              <CircleCheck className="size-4" aria-hidden />
              Done {formatTime(task.completedAt, timeZone)}
              {task.completedBy ? ` by ${task.completedBy}` : ''}
            </span>
          )}
        </div>
        <div className="flex flex-wrap gap-1.5">
          <Badge variant="secondary">{KIND_LABEL[task.kind]}</Badge>
          <Badge variant={bad ? 'destructive' : 'outline'} data-testid="status-badge">
            {task.isOverdue && <AlarmClock aria-hidden />}
            {label}
          </Badge>
          {task.completedLate && <Badge variant="outline">Completed late</Badge>}
          {task.hasFailures && <Badge variant="destructive">Failed checks</Badge>}
        </div>
      </Card>
    </Link>
  )
}
