import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { getRouteApi, Link } from '@tanstack/react-router'
import { Plus, Search, TriangleAlert } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { errorMessage } from '@/api/problem'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { sitesQuery } from '@/features/today/queries'
import { cn } from '@/lib/utils'

import { IssueRow } from './IssueRow'
import { PRIORITY_LABEL } from './labels'
import { issuesListQuery } from './queries'
import {
  hasActiveFilters,
  STATUS_FILTER_LABEL,
  STATUS_FILTERS,
  type IssuesSearch,
  type StatusFilter,
} from './search'
import { SummaryStrip } from './SummaryStrip'
import { PRIORITIES, type IssuePriority } from './types'

const route = getRouteApi('/_authenticated/issues')

const selectClass = 'h-12 w-full rounded-lg border border-input bg-card px-3 text-base'

export function IssuesPage() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canView = usePermission(Permissions.Issues.View)
  const canRaise = usePermission(Permissions.Issues.Raise)
  const search = route.useSearch()
  const navigate = route.useNavigate()

  // Filters live in the URL so the view can be shared and survives the back button.
  const setFilters = (next: Partial<IssuesSearch>) =>
    void navigate({ search: (prev) => ({ ...prev, ...next }), replace: true })

  const sites = useQuery({ ...sitesQuery(client, organizationId), enabled: canView })
  const siteList = useMemo(() => sites.data?.items ?? [], [sites.data])
  const list = useInfiniteQuery({
    ...issuesListQuery(client, organizationId, search),
    enabled: canView,
  })

  if (!canView) {
    return (
      <section className="flex flex-col gap-2 py-12 text-center">
        <h1 className="text-2xl font-semibold">Issues</h1>
        <p className="text-muted-foreground">
          You do not have access to issues in this organization.
        </p>
      </section>
    )
  }

  const items = list.data?.pages.flatMap((p) => p.items) ?? []
  const total = list.data?.pages.at(-1)?.totalCount ?? 0

  return (
    <section className="flex flex-col gap-5">
      <header className="flex items-start justify-between gap-3">
        <h1 className="text-2xl font-semibold tracking-tight">Issues</h1>
        {canRaise && (
          <Button asChild size="lg">
            <Link to="/issues/new" search={{ siteId: search.siteId }}>
              <Plus aria-hidden />
              Raise issue
            </Link>
          </Button>
        )}
      </header>

      <SummaryStrip search={search} onChange={setFilters} />

      <Filters search={search} sites={siteList} onChange={setFilters} />

      {list.isError ? (
        <div role="alert" className="flex flex-col items-center gap-3 py-12 text-center">
          <p>{errorMessage(list.error)}</p>
          <Button variant="outline" onClick={() => void list.refetch()}>
            Try again
          </Button>
        </div>
      ) : list.isPending ? (
        <div className="flex flex-col gap-3" role="status" aria-label="Loading issues">
          {[0, 1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-28 w-full rounded-xl" />
          ))}
        </div>
      ) : items.length === 0 ? (
        <div className="flex flex-col items-center gap-3 py-12 text-center">
          <TriangleAlert className="size-10 text-muted-foreground" aria-hidden />
          {hasActiveFilters(search) ? (
            <>
              <h2 className="text-lg font-medium">No issues match these filters</h2>
              <Button
                variant="outline"
                onClick={() =>
                  setFilters({
                    status: undefined,
                    priority: undefined,
                    siteId: undefined,
                    mine: undefined,
                    overdue: undefined,
                    q: undefined,
                  })
                }
              >
                Clear filters
              </Button>
            </>
          ) : (
            <>
              <h2 className="text-lg font-medium">No open issues</h2>
              <p className="text-muted-foreground">Nothing needs attention right now.</p>
            </>
          )}
        </div>
      ) : (
        <>
          <ul className="flex flex-col gap-3" aria-label="Issues">
            {items.map((issue) => (
              <li key={issue.id}>
                <IssueRow issue={issue} />
              </li>
            ))}
          </ul>
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

function Filters({
  search,
  sites,
  onChange,
}: {
  search: IssuesSearch
  sites: { id: string; name: string }[]
  onChange: (next: Partial<IssuesSearch>) => void
}) {
  // The box edits freely and is written to the URL once typing pauses.
  const [text, setText] = useState(search.q ?? '')
  const [lastQ, setLastQ] = useState(search.q)
  if (search.q !== lastQ) {
    // Adopt changes made elsewhere (back button, Clear filters).
    setLastQ(search.q)
    setText(search.q ?? '')
  }
  useEffect(() => {
    const next = text.trim() || undefined
    if (next === (search.q ?? undefined)) return
    const timer = setTimeout(() => onChange({ q: next }), 350)
    return () => clearTimeout(timer)
  }, [text, search.q, onChange])

  return (
    <div className="flex flex-col gap-3">
      <div className="relative">
        <Search
          className="pointer-events-none absolute top-1/2 left-3 size-5 -translate-y-1/2 text-muted-foreground"
          aria-hidden
        />
        <Input
          type="search"
          aria-label="Search issues"
          placeholder="Search title or #number"
          value={text}
          onChange={(e) => setText(e.target.value)}
          className="h-12 pl-10"
        />
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <select
          aria-label="Status"
          value={search.status ?? 'active'}
          onChange={(e) =>
            onChange({
              status: e.target.value === 'active' ? undefined : (e.target.value as StatusFilter),
            })
          }
          className={selectClass}
        >
          {STATUS_FILTERS.map((s) => (
            <option key={s} value={s}>
              {STATUS_FILTER_LABEL[s]}
            </option>
          ))}
        </select>
        <select
          aria-label="Priority"
          value={search.priority ?? ''}
          onChange={(e) =>
            onChange({ priority: (e.target.value || undefined) as IssuePriority | undefined })
          }
          className={selectClass}
        >
          <option value="">Any priority</option>
          {PRIORITIES.map((p) => (
            <option key={p} value={p}>
              {PRIORITY_LABEL[p]}
            </option>
          ))}
        </select>
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
      </div>
      <div className="flex flex-wrap gap-2">
        <Toggle pressed={!!search.mine} onPressedChange={(v) => onChange({ mine: v || undefined })}>
          Assigned to me
        </Toggle>
        <Toggle
          pressed={!!search.overdue}
          onPressedChange={(v) => onChange({ overdue: v || undefined })}
        >
          Overdue
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
