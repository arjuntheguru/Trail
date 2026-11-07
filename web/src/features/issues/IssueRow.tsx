import { Link } from '@tanstack/react-router'
import { Camera, ClipboardX, MapPin, PenLine, User } from 'lucide-react'

import { Card } from '@/components/ui/card'

import { DueIndicator, EscalatedBadge, PriorityBadge, StatusBadge } from './Badges'
import type { IssueListItem } from './types'

export function IssueRow({ issue }: { issue: IssueListItem }) {
  const fromTask = issue.source === 'TaskFailure'
  const SourceIcon = fromTask ? ClipboardX : PenLine
  return (
    <Link
      to="/issues/$issueId"
      params={{ issueId: issue.id }}
      className="block rounded-xl outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
    >
      <Card className="gap-2 p-4 transition-colors hover:bg-accent/40" data-testid="issue-row">
        <div className="flex items-start gap-2">
          <SourceIcon
            className="mt-1 size-4 shrink-0 text-muted-foreground"
            role="img"
            aria-label={fromTask ? 'Raised from a task' : 'Raised manually'}
          />
          <div className="min-w-0 flex-1">
            <p className="text-sm text-muted-foreground">#{issue.number}</p>
            <h3 className="leading-snug font-medium">{issue.title}</h3>
          </div>
          <PriorityBadge priority={issue.priority} />
        </div>
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
          <StatusBadge status={issue.status} />
          {issue.isEscalated && <EscalatedBadge />}
          <span className="inline-flex items-center gap-1">
            <MapPin className="size-4" aria-hidden />
            {issue.siteName ?? 'Unknown site'}
          </span>
          <span className="inline-flex items-center gap-1">
            <User className="size-4" aria-hidden />
            {issue.assignee ? (issue.assignee.name ?? 'Assigned') : 'Unassigned'}
          </span>
          <DueIndicator dueAt={issue.dueAt} isOverdue={issue.isOverdue} timeZone={issue.timeZone} />
          {issue.photoCount > 0 && (
            <span className="inline-flex items-center gap-1">
              <Camera className="size-4" aria-hidden />
              {issue.photoCount}
              <span className="sr-only">photos</span>
            </span>
          )}
        </div>
      </Card>
    </Link>
  )
}
