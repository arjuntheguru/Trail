import { AlarmClock, ArrowUpCircle, Clock } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { formatDateTime } from '@/lib/dates'
import { cn } from '@/lib/utils'

import { PRIORITY_CLASS, PRIORITY_LABEL, STATUS_CLASS, STATUS_LABEL } from './labels'
import type { IssuePriority, IssueStatus } from './types'

export function StatusBadge({ status }: { status: IssueStatus }) {
  return (
    <Badge variant="outline" className={STATUS_CLASS[status]} data-testid="status-badge">
      {STATUS_LABEL[status]}
    </Badge>
  )
}

export function PriorityBadge({ priority }: { priority: IssuePriority }) {
  return (
    <Badge className={PRIORITY_CLASS[priority]} data-testid="priority-badge">
      {PRIORITY_LABEL[priority]}
    </Badge>
  )
}

export function EscalatedBadge({
  escalatedAt,
  timeZone,
}: {
  escalatedAt?: string | null
  timeZone?: string
}) {
  return (
    <Badge variant="destructive" data-testid="escalated-badge">
      <ArrowUpCircle aria-hidden />
      Escalated{escalatedAt ? ` ${formatDateTime(escalatedAt, timeZone)}` : ''}
    </Badge>
  )
}

/** "Due 8 Nov, 9:00 AM", in red with "Overdue" once an active issue is past its deadline. */
export function DueIndicator({
  dueAt,
  isOverdue,
  timeZone,
  className,
}: {
  dueAt: string | null
  isOverdue: boolean
  timeZone?: string
  className?: string
}) {
  if (!dueAt) return null
  const Icon = isOverdue ? AlarmClock : Clock
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 text-sm',
        isOverdue ? 'font-medium text-destructive' : 'text-muted-foreground',
        className,
      )}
      data-testid="due-indicator"
    >
      <Icon className="size-4" aria-hidden />
      {isOverdue ? 'Overdue · ' : 'Due '}
      {formatDateTime(dueAt, timeZone)}
    </span>
  )
}
