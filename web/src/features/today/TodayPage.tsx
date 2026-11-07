import { useQuery } from '@tanstack/react-query'
import { CalendarCheck, RefreshCw } from 'lucide-react'
import { useMemo } from 'react'

import { errorMessage } from '@/api/problem'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { formatDay } from '@/lib/dates'

import { groupTasks } from './groups'
import { siteTasksQuery, sitesQuery } from './queries'
import { TaskCard } from './TaskCard'
import { useSelectedSite } from './useSelectedSite'

export function TodayPage() {
  const { client } = useAuthService()
  const { organization } = useSession()
  const organizationId = organization?.id
  const canViewTasks = usePermission(Permissions.Tasks.View)

  const sites = useQuery({ ...sitesQuery(client, organizationId), enabled: canViewTasks })
  const siteList = useMemo(() => sites.data?.items ?? [], [sites.data])
  const [siteId, selectSite] = useSelectedSite(
    organizationId ?? '',
    siteList.map((s) => s.id),
  )
  const tasks = useQuery({ ...siteTasksQuery(client, organizationId, siteId ?? undefined) })

  if (!canViewTasks) {
    return <Message title="Today" body="You do not have access to tasks in this organization." />
  }
  if (sites.isError) {
    return <ErrorState message={errorMessage(sites.error)} onRetry={() => void sites.refetch()} />
  }
  if (sites.isPending) return <TodaySkeleton />
  if (siteList.length === 0) {
    return (
      <Message
        title="Today"
        body="You do not have access to any sites yet. Ask an administrator to give you access."
      />
    )
  }

  const data = tasks.data
  const groups = data ? groupTasks(data.items, tasks.dataUpdatedAt) : []

  return (
    <section className="flex flex-col gap-5">
      <header className="flex flex-col gap-3">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <h1 className="text-2xl font-semibold tracking-tight">Today</h1>
            <p className="text-sm text-muted-foreground">{data ? formatDay(data.date) : ' '}</p>
          </div>
          <Button
            variant="outline"
            size="icon"
            aria-label="Refresh tasks"
            disabled={tasks.isFetching}
            onClick={() => void tasks.refetch()}
          >
            <RefreshCw className={tasks.isFetching ? 'animate-spin' : undefined} aria-hidden />
          </Button>
        </div>
        {siteList.length > 1 ? (
          <div className="flex flex-col gap-1.5">
            <label htmlFor="site-picker" className="text-sm font-medium">
              Site
            </label>
            <select
              id="site-picker"
              value={siteId ?? ''}
              onChange={(e) => selectSite(e.target.value)}
              className="h-12 w-full rounded-lg border border-input bg-card px-3 text-base"
            >
              {siteList.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
          </div>
        ) : (
          <p className="text-lg font-medium">{siteList[0]?.name}</p>
        )}
      </header>

      {tasks.isError ? (
        <ErrorState message={errorMessage(tasks.error)} onRetry={() => void tasks.refetch()} />
      ) : !data ? (
        <TaskListSkeleton />
      ) : groups.length === 0 ? (
        <div className="flex flex-col items-center gap-3 py-12 text-center">
          <CalendarCheck className="size-10 text-muted-foreground" aria-hidden />
          <h2 className="text-lg font-medium">Nothing scheduled today</h2>
          <p className="text-muted-foreground">There are no tasks for {data.siteName} today.</p>
        </div>
      ) : (
        groups.map((group) => (
          <section key={group.key} aria-label={group.label} className="flex flex-col gap-2">
            <h2 className="text-sm font-semibold tracking-wide text-muted-foreground uppercase">
              {group.label} ({group.items.length})
            </h2>
            {group.items.map((task) => (
              <TaskCard key={task.id} task={task} group={group.key} timeZone={data.timeZone} />
            ))}
          </section>
        ))
      )}
    </section>
  )
}

function Message({ title, body }: { title: string; body: string }) {
  return (
    <section className="flex flex-col gap-2 py-12 text-center">
      <h1 className="text-2xl font-semibold">{title}</h1>
      <p className="text-muted-foreground">{body}</p>
    </section>
  )
}

function ErrorState({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div role="alert" className="flex flex-col items-center gap-3 py-12 text-center">
      <p>{message}</p>
      <Button variant="outline" onClick={onRetry}>
        Try again
      </Button>
    </div>
  )
}

function TaskListSkeleton() {
  return (
    <div className="flex flex-col gap-3" aria-label="Loading tasks" role="status">
      {[0, 1, 2, 3].map((i) => (
        <Skeleton key={i} className="h-24 w-full rounded-xl" />
      ))}
    </div>
  )
}

function TodaySkeleton() {
  return (
    <div className="flex flex-col gap-5" role="status" aria-label="Loading">
      <Skeleton className="h-8 w-32" />
      <Skeleton className="h-12 w-full" />
      <TaskListSkeleton />
    </div>
  )
}
