import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { getRouteApi } from '@tanstack/react-router'
import { ClipboardList } from 'lucide-react'
import { useMemo } from 'react'

import { errorMessage } from '@/api/problem'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { sitesQuery } from '@/features/today/queries'
import { formatDay } from '@/lib/dates'
import { cn } from '@/lib/utils'

import { tasksListQuery } from './queries'
import {
  CLEARED,
  hasActiveFilters,
  RANGE_LABEL,
  RANGES,
  resolveRange,
  TASK_STATUSES,
  toApiFilters,
  type RangeKey,
  type ResolvedRange,
  type TasksSearch,
} from './search'
import { TaskListRow } from './TaskListRow'
import type { TaskStatus } from './types'

const route = getRouteApi('/_authenticated/tasks')

const selectClass = 'h-12 w-full rounded-lg border border-input bg-card px-3 text-base'

export function TaskListPage() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canView = usePermission(Permissions.Tasks.View)
  const canSeeSites = usePermission(Permissions.Sites.View)
  const search = route.useSearch()
  const navigate = route.useNavigate()

  // Filters live in the URL so the view can be shared and survives the back button.
  const setFilters = (next: Partial<TasksSearch>) =>
    void navigate({ search: (prev) => ({ ...prev, ...next }), replace: true })

  const range = resolveRange(search, new Date())
  const filters = toApiFilters(search, range)

  const sites = useQuery({ ...sitesQuery(client, organizationId), enabled: canView && canSeeSites })
  const siteList = useMemo(() => sites.data?.items ?? [], [sites.data])
  const zones = useMemo(() => new Map(siteList.map((s) => [s.id, s.timeZone])), [siteList])
  const list = useInfiniteQuery({
    ...tasksListQuery(client, organizationId, filters),
    enabled: canView && !range.error,
  })

  if (!canView) {
    return (
      <section className="flex flex-col gap-2 py-12 text-center">
        <h1 className="text-2xl font-semibold">Tasks</h1>
        <p className="text-muted-foreground">
          You do not have access to tasks in this organization.
        </p>
      </section>
    )
  }

  const items = list.data?.pages.flatMap((p) => p.items) ?? []
  const total = list.data?.pages.at(-1)?.totalCount ?? 0

  return (
    <section className="flex flex-col gap-5">
      <header>
        <h1 className="text-2xl font-semibold tracking-tight">Tasks</h1>
        <p className="text-sm text-muted-foreground">Search task history across your sites.</p>
      </header>

      <Filters
        search={search}
        range={range}
        sites={canSeeSites ? siteList : undefined}
        onChange={setFilters}
      />

      {range.error ? (
        <p role="alert" className="text-destructive">
          {range.error}
        </p>
      ) : list.isError ? (
        <div role="alert" className="flex flex-col items-center gap-3 py-12 text-center">
          <p>{errorMessage(list.error)}</p>
          <Button variant="outline" onClick={() => void list.refetch()}>
            Try again
          </Button>
        </div>
      ) : list.isPending ? (
        <div className="flex flex-col gap-3" role="status" aria-label="Loading tasks">
          {[0, 1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-28 w-full rounded-xl" />
          ))}
        </div>
      ) : items.length === 0 ? (
        <div className="flex flex-col items-center gap-3 py-12 text-center">
          <ClipboardList className="size-10 text-muted-foreground" aria-hidden />
          {hasActiveFilters(search, range) ? (
            <>
              <h2 className="text-lg font-medium">No tasks match these filters</h2>
              <Button variant="outline" onClick={() => setFilters(CLEARED)}>
                Clear filters
              </Button>
            </>
          ) : (
            <>
              <h2 className="text-lg font-medium">No tasks in this period</h2>
              <p className="text-muted-foreground">Try a longer date range.</p>
            </>
          )}
        </div>
      ) : (
        <>
          <div className="flex flex-col gap-4">
            {groupByDay(items).map(([day, dayItems]) => (
              <section key={day} aria-label={formatDay(day)} className="flex flex-col gap-3">
                <h2 className="text-sm font-semibold text-muted-foreground">{formatDay(day)}</h2>
                <ul className="flex flex-col gap-3">
                  {dayItems.map((task) => (
                    <li key={task.id}>
                      <TaskListRow task={task} timeZone={zones.get(task.siteId)} />
                    </li>
                  ))}
                </ul>
              </section>
            ))}
          </div>
          <div className="flex flex-col items-center gap-2">
            <p className="text-sm text-muted-foreground">
              Showing {items.length} of {total}
            </p>
            {list.hasNextPage && (
              <Button
                variant="outline"
                size="lg"
                disabled={list.isFetchingNextPage}
                onClick={() => void list.fetchNextPage()}
              >
                {list.isFetchingNextPage ? 'Loading…' : 'Load more'}
              </Button>
            )}
          </div>
        </>
      )}
    </section>
  )
}

/** Consecutive tasks sharing a due date (the API returns them newest first). */
function groupByDay<T extends { dueDate: string }>(items: T[]): [string, T[]][] {
  const groups: [string, T[]][] = []
  for (const item of items) {
    const last = groups.at(-1)
    if (last && last[0] === item.dueDate) last[1].push(item)
    else groups.push([item.dueDate, [item]])
  }
  return groups
}

function Filters({
  search,
  range,
  sites,
  onChange,
}: {
  search: TasksSearch
  range: ResolvedRange
  /** Undefined when the caller may not list sites. */
  sites: { id: string; name: string }[] | undefined
  onChange: (next: Partial<TasksSearch>) => void
}) {
  const setRange = (key: RangeKey) =>
    onChange(
      key === 'custom'
        ? { range: 'custom', from: range.from, to: range.to }
        : { range: key === '7d' ? undefined : key, from: undefined, to: undefined },
    )

  return (
    <div className="flex flex-col gap-3">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <select
          aria-label="Date range"
          value={range.key}
          onChange={(e) => setRange(e.target.value as RangeKey)}
          className={selectClass}
        >
          {RANGES.map((r) => (
            <option key={r} value={r}>
              {RANGE_LABEL[r]}
            </option>
          ))}
        </select>
        {sites && (
          <select
            aria-label="Site"
            value={search.siteId ?? ''}
            onChange={(e) => onChange({ siteId: e.target.value || undefined })}
            className={selectClass}
          >
            <option value="">All sites</option>
            {sites.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>
        )}
        <select
          aria-label="Status"
          value={search.status ?? ''}
          onChange={(e) =>
            onChange({ status: (e.target.value || undefined) as TaskStatus | undefined })
          }
          className={selectClass}
        >
          <option value="">Any status</option>
          {TASK_STATUSES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </select>
      </div>
      {range.key === 'custom' && (
        <div className="grid grid-cols-2 gap-3">
          <label className="flex flex-col gap-1 text-sm text-muted-foreground">
            From
            <Input
              type="date"
              value={range.from}
              onChange={(e) => e.target.value && onChange({ from: e.target.value })}
              className="h-12"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm text-muted-foreground">
            To
            <Input
              type="date"
              value={range.to}
              onChange={(e) => e.target.value && onChange({ to: e.target.value })}
              className="h-12"
            />
          </label>
        </div>
      )}
      <div className="flex flex-wrap gap-2">
        <Toggle
          pressed={!!search.failures}
          onPressedChange={(v) => onChange({ failures: v || undefined })}
        >
          With failures
        </Toggle>
        <Toggle
          pressed={!!search.approval}
          onPressedChange={(v) => onChange({ approval: v || undefined })}
        >
          Awaiting approval
        </Toggle>
      </div>
    </div>
  )
}

function Toggle({
  pressed,
  onPressedChange,
  children,
}: {
  pressed: boolean
  onPressedChange: (pressed: boolean) => void
  children: string
}) {
  return (
    <button
      type="button"
      aria-pressed={pressed}
      onClick={() => onPressedChange(!pressed)}
      className={cn(
        'h-11 rounded-full border bg-card px-4 text-base font-medium',
        pressed && 'border-primary bg-accent',
      )}
    >
      {children}
    </button>
  )
}
