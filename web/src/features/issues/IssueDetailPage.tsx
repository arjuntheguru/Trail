import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { ArrowLeft, ClipboardX, MapPin, User } from 'lucide-react'

import { ApiError, errorMessage } from '@/api/problem'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { PhotoGallery } from '@/components/Photos'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { formatDay } from '@/lib/dates'

import { DueIndicator, EscalatedBadge, PriorityBadge, StatusBadge } from './Badges'
import { CommentBox } from './CommentBox'
import { IssueActions, ManageIssue } from './IssueActions'
import { issueQuery } from './queries'
import { Timeline } from './Timeline'
import type { IssueDetail } from './types'

export function IssueDetailPage({ issueId }: { issueId: string }) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canView = usePermission(Permissions.Issues.View)
  const issue = useQuery({ ...issueQuery(client, organizationId, issueId), enabled: canView })

  if (!canView) {
    return <Message body="You do not have access to issues in this organization." />
  }
  if (issue.isError) {
    if (issue.error instanceof ApiError && issue.error.status === 404) {
      return <Message body="This issue does not exist or you cannot see it." />
    }
    return (
      <div role="alert" className="flex flex-col items-center gap-3 py-12 text-center">
        <p>{errorMessage(issue.error)}</p>
        <Button variant="outline" onClick={() => void issue.refetch()}>
          Try again
        </Button>
      </div>
    )
  }
  if (!issue.data) {
    return (
      <div className="flex flex-col gap-4" role="status" aria-label="Loading issue">
        <Skeleton className="h-8 w-2/3" />
        <Skeleton className="h-6 w-1/2" />
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-40 w-full" />
      </div>
    )
  }
  return <IssueView issue={issue.data} />
}

function Message({ body }: { body: string }) {
  return (
    <section className="flex flex-col items-center gap-3 py-12 text-center">
      <h1 className="text-2xl font-semibold">Issue</h1>
      <p className="text-muted-foreground">{body}</p>
      <Button asChild variant="outline">
        <Link to="/issues">Back to issues</Link>
      </Button>
    </section>
  )
}

function IssueView({ issue }: { issue: IssueDetail }) {
  const active = issue.status === 'Open' || issue.status === 'InProgress'
  return (
    <article className="flex flex-col gap-5">
      <header className="flex flex-col gap-3">
        <Link
          to="/issues"
          className="-ml-2 inline-flex h-11 w-fit items-center gap-1 rounded-md px-2 text-base font-medium text-muted-foreground"
        >
          <ArrowLeft className="size-5" aria-hidden />
          Issues
        </Link>
        <div>
          <p className="text-sm text-muted-foreground">#{issue.number}</p>
          <h1 className="text-2xl leading-tight font-semibold tracking-tight">{issue.title}</h1>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <StatusBadge status={issue.status} />
          <PriorityBadge priority={issue.priority} />
          {issue.isOverdue && <Badge variant="destructive">Overdue</Badge>}
          {issue.escalatedAt && (
            <EscalatedBadge escalatedAt={issue.escalatedAt} timeZone={issue.timeZone} />
          )}
          {issue.source === 'TaskFailure' && (
            <Badge variant="outline">
              <ClipboardX aria-hidden />
              Raised from a task
            </Badge>
          )}
        </div>
        <div className="flex flex-col gap-1 text-sm text-muted-foreground">
          <p className="flex items-center gap-1.5">
            <MapPin className="size-4" aria-hidden />
            {issue.siteName ?? 'Unknown site'}
            {issue.departmentName && ` · ${issue.departmentName}`}
          </p>
          <p className="flex items-center gap-1.5">
            <User className="size-4" aria-hidden />
            {issue.assignee ? (issue.assignee.name ?? 'Assigned') : 'Unassigned'}
          </p>
          {active && (
            <DueIndicator
              dueAt={issue.dueAt}
              isOverdue={issue.isOverdue}
              timeZone={issue.timeZone}
            />
          )}
        </div>
      </header>

      {issue.description && <p className="text-base whitespace-pre-wrap">{issue.description}</p>}
      <PhotoGallery ids={issue.photoIds} label="Issue photo" />

      {issue.siteTaskId && (
        <p className="text-sm">
          Raised from task{' '}
          <Link
            to="/tasks/$taskId"
            params={{ taskId: issue.siteTaskId }}
            className="font-medium text-primary underline-offset-4 hover:underline"
          >
            {issue.taskTitle ?? 'View task'}
          </Link>
          {issue.taskDueDate && ` (${formatDay(issue.taskDueDate)})`}
          {issue.fieldKey && (
            <span className="text-muted-foreground"> · field {issue.fieldKey}</span>
          )}
        </p>
      )}

      {issue.resolution && (
        <section className="rounded-xl border p-4" aria-label="Resolution">
          <h2 className="font-medium">Corrective action</h2>
          <p className="mt-1 whitespace-pre-wrap">{issue.resolution}</p>
          {issue.resolvedBy && (
            <p className="mt-1 text-sm text-muted-foreground">Resolved by {issue.resolvedBy}</p>
          )}
        </section>
      )}

      <IssueActions issue={issue} />
      <ManageIssue issue={issue} />

      <section aria-label="History" className="flex flex-col gap-3">
        <h2 className="text-sm font-semibold tracking-wide text-muted-foreground uppercase">
          History
        </h2>
        <Timeline events={issue.timeline} timeZone={issue.timeZone} />
      </section>

      <CommentBox issue={issue} />
    </article>
  )
}
