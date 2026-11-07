import { Link } from '@tanstack/react-router'
import {
  AlarmClock,
  Ban,
  CircleCheck,
  CircleDashed,
  CircleX,
  Clock,
  ShieldAlert,
} from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { formatTime } from '@/lib/dates'
import { cn } from '@/lib/utils'

import type { TaskGroupKey, TaskItem } from './groups'

const KIND_LABEL: Record<TaskItem['kind'], string> = {
  Checklist: 'Checklist',
  RecordLog: 'Record log',
  Form: 'Form',
}

const STYLE: Record<TaskGroupKey, { icon: typeof Clock; className: string; label: string }> = {
  overdue: { icon: AlarmClock, className: 'text-destructive', label: 'Overdue' },
  due: { icon: Clock, className: 'text-primary', label: 'Due' },
  upcoming: { icon: CircleDashed, className: 'text-muted-foreground', label: 'Upcoming' },
  'awaiting-approval': { icon: ShieldAlert, className: 'text-warning', label: 'Awaiting approval' },
  missed: { icon: CircleX, className: 'text-destructive', label: 'Missed' },
  completed: { icon: CircleCheck, className: 'text-success', label: 'Completed' },
  cancelled: { icon: Ban, className: 'text-muted-foreground', label: 'Cancelled' },
}

export function TaskCard({
  task,
  group,
  timeZone,
}: {
  task: TaskItem
  group: TaskGroupKey
  timeZone: string
}) {
  const { icon: Icon, className, label } = STYLE[group]
  const done = group === 'completed' || group === 'awaiting-approval'

  return (
    <Link
      to="/tasks/$taskId"
      params={{ taskId: task.id }}
      className="block rounded-xl outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
    >
      <Card
        className="flex-row items-center gap-3 p-4 transition-colors hover:bg-accent/40"
        data-testid="task-card"
        data-group={group}
      >
        <Icon className={cn('size-7 shrink-0', className)} aria-hidden />
        <div className="min-w-0 flex-1">
          <h3
            className={cn(
              'text-base leading-snug font-medium',
              group === 'cancelled' && 'line-through',
            )}
          >
            {task.title}
          </h3>
          <p className="mt-0.5 text-sm text-muted-foreground">
            {done && task.completedAt
              ? `Done ${formatTime(task.completedAt, timeZone)}${task.completedBy ? ` by ${task.completedBy}` : ''}${task.completedLate ? ' (late)' : ''}`
              : group === 'upcoming' && task.availableAt
                ? `Opens ${formatTime(task.availableAt, timeZone)}, due ${formatTime(task.dueAt, timeZone)}`
                : `Due ${formatTime(task.dueAt, timeZone)}`}
            {task.kind === 'RecordLog' && ` · ${Number(task.entryCount)} readings`}
          </p>
          <div className="mt-2 flex flex-wrap gap-1.5">
            <Badge variant="secondary">{KIND_LABEL[task.kind]}</Badge>
            <Badge variant="outline" className={className}>
              {label}
            </Badge>
            {task.hasFailures && <Badge variant="destructive">Failed checks</Badge>}
            {task.canCompleteLate && <Badge variant="outline">Can still be completed</Badge>}
          </div>
        </div>
      </Card>
    </Link>
  )
}
