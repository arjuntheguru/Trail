import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { getRouteApi, Link } from '@tanstack/react-router'
import { useMemo } from 'react'

import { errorMessage } from '@/api/problem'
import type { components } from '@/api/schema'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { sitesQuery } from '@/features/today/queries'
import { formatDateTime } from '@/lib/dates'

import { ErrorBlock, SiteSelect } from './Filters'
import { activityQuery } from './queries'

type Item = components['schemas']['ActivityItemResponse']

const route = getRouteApi('/_authenticated/reports/activity')

const VERB: Record<string, string> = {
  task_completed: 'completed',
  task_approved: 'approved',
  issue_raised: 'raised',
  issue_status_changed: 'changed the status of',
  issue_assigned: 'assigned',
  issue_commented: 'commented on',
}

function isTask(kind: string) {
  return kind.startsWith('task_')
}

export function ActivityPage() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canSeeSites = usePermission(Permissions.Sites.View)
  const canSeeTasks = usePermission(Permissions.Tasks.View)
  const canSeeIssues = usePermission(Permissions.Issues.View)
  const search = route.useSearch()
  const navigate = route.useNavigate()

  const sites = useQuery({ ...sitesQuery(client, organizationId), enabled: canSeeSites })
  const siteList = useMemo(() => sites.data?.items ?? [], [sites.data])
  const feed = useInfiniteQuery(activityQuery(client, organizationId, search.siteId))
  const items = feed.data?.pages.flatMap((p) => p.items) ?? []

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <p className="text-sm text-muted-foreground">
          Completed and approved tasks and issue activity, newest first.
        </p>
        {canSeeSites && (
          <div className="sm:w-64">
            <SiteSelect
              value={search.siteId}
              sites={siteList}
              onChange={(siteId) =>
                void navigate({ search: (prev) => ({ ...prev, siteId }), replace: true })
              }
            />
          </div>
        )}
      </div>

      {feed.isError ? (
        <ErrorBlock message={errorMessage(feed.error)} onRetry={() => void feed.refetch()} />
      ) : feed.isPending ? (
        <div className="flex flex-col gap-2" role="status" aria-label="Loading activity">
          {[0, 1, 2, 3, 4].map((i) => (
            <Skeleton key={i} className="h-16 w-full rounded-xl" />
          ))}
        </div>
      ) : items.length === 0 ? (
        <div className="py-12 text-center">
          <h2 className="text-lg font-medium">No activity yet</h2>
          <p className="text-muted-foreground">Completed tasks and issue updates show up here.</p>
        </div>
      ) : (
        <>
          <ul className="flex flex-col divide-y rounded-xl border bg-card" aria-label="Activity">
            {items.map((item, i) => (
              <li key={`${item.kind}-${item.subjectId}-${item.occurredAt}-${i}`}>
                <ActivityRow item={item} canSeeTasks={canSeeTasks} canSeeIssues={canSeeIssues} />
              </li>
            ))}
          </ul>
          <div className="flex flex-col items-center gap-2">
            <p className="text-sm text-muted-foreground">Showing {items.length} items</p>
            {feed.hasNextPage && (
              <Button
                variant="outline"
                size="lg"
                disabled={feed.isFetchingNextPage}
                onClick={() => void feed.fetchNextPage()}
              >
                {feed.isFetchingNextPage ? 'Loading…' : 'Load more'}
              </Button>
            )}
          </div>
        </>
      )}
    </div>
  )
}

function ActivityRow({
  item,
  canSeeTasks,
  canSeeIssues,
}: {
  item: Item
  canSeeTasks: boolean
  canSeeIssues: boolean
}) {
  const task = isTask(item.kind)
  const label =
    !task && item.issueNumber != null ? `#${item.issueNumber} ${item.title}` : item.title
  const className = 'font-medium underline-offset-4 hover:underline'
  const subject = task ? (
    canSeeTasks ? (
      <Link to="/tasks/$taskId" params={{ taskId: item.subjectId }} className={className}>
        {label}
      </Link>
    ) : (
      <span className="font-medium">{label}</span>
    )
  ) : canSeeIssues ? (
    <Link to="/issues/$issueId" params={{ issueId: item.subjectId }} className={className}>
      {label}
    </Link>
  ) : (
    <span className="font-medium">{label}</span>
  )

  return (
    <div className="flex flex-col gap-1 px-4 py-3" data-testid="activity-item">
      <p>
        <span className="font-medium">{item.actorName ?? 'Someone'}</span>{' '}
        {VERB[item.kind] ?? 'updated'} {subject}
      </p>
      <p className="text-sm text-muted-foreground">
        {item.siteName ?? 'Unknown site'} · {formatDateTime(item.occurredAt)}
      </p>
    </div>
  )
}
