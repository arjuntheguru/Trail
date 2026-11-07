import { useQuery } from '@tanstack/react-query'
import { getRouteApi, Link } from '@tanstack/react-router'
import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react'
import { useMemo, useState, type ReactNode } from 'react'

import { errorMessage } from '@/api/problem'
import type { components } from '@/api/schema'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { PRIORITY_CLASS, PRIORITY_LABEL } from '@/features/issues/labels'
import { PRIORITIES } from '@/features/issues/types'
import { sitesQuery } from '@/features/today/queries'
import { cn } from '@/lib/utils'

import { ErrorBlock, SiteSelect } from './Filters'
import { dashboardQuery } from './queries'
import { Score } from './score'
import { formatScore, scoreTone, TONE_BAR, TONE_TEXT } from './scoreTone'
import { sortSites, type SortKey } from './sortSites'

type SiteRow = components['schemas']['DashboardSiteResponse']
type Totals = components['schemas']['DashboardTotalsResponse']

const route = getRouteApi('/_authenticated/reports/')

const COLUMNS: { key: SortKey; label: string; numeric: boolean }[] = [
  { key: 'siteName', label: 'Site', numeric: false },
  { key: 'score', label: 'On-time score', numeric: true },
  { key: 'completed', label: 'Done today', numeric: true },
  { key: 'overdue', label: 'Overdue tasks', numeric: true },
  { key: 'missedYesterday', label: 'Missed yesterday', numeric: true },
  { key: 'openIssues', label: 'Open issues', numeric: true },
  { key: 'overdueIssues', label: 'Overdue issues', numeric: true },
]

export function DashboardPage() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canSeeSites = usePermission(Permissions.Sites.View)
  const search = route.useSearch()
  const navigate = route.useNavigate()

  const sites = useQuery({ ...sitesQuery(client, organizationId), enabled: canSeeSites })
  const siteList = useMemo(() => sites.data?.items ?? [], [sites.data])
  const dashboard = useQuery(dashboardQuery(client, organizationId, search.siteId))

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <p className="text-sm text-muted-foreground">
          Where each site stands today, using the site&rsquo;s own calendar day.
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

      {dashboard.isError ? (
        <ErrorBlock
          message={errorMessage(dashboard.error)}
          onRetry={() => void dashboard.refetch()}
        />
      ) : dashboard.isPending ? (
        <div className="flex flex-col gap-4" role="status" aria-label="Loading dashboard">
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            {[0, 1, 2, 3].map((i) => (
              <Skeleton key={i} className="h-28 rounded-xl" />
            ))}
          </div>
          <Skeleton className="h-64 w-full rounded-xl" />
        </div>
      ) : dashboard.data.sites.length === 0 ? (
        <div className="py-12 text-center">
          <h2 className="text-lg font-medium">No active sites</h2>
          <p className="text-muted-foreground">
            There is nothing to report on yet. Sites you can see appear here.
          </p>
        </div>
      ) : (
        <>
          <Tiles totals={dashboard.data.totals} siteId={search.siteId} />
          <SiteTable rows={dashboard.data.sites} />
        </>
      )}
    </div>
  )
}

function Tile({
  label,
  value,
  detail,
  tone,
  children,
}: {
  label: string
  value: ReactNode
  detail?: ReactNode
  tone?: string
  children?: ReactNode
}) {
  return (
    <Card className="gap-1 p-4" data-testid="kpi-tile">
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className={cn('text-3xl font-semibold tabular-nums', tone)}>{value}</p>
      {detail && <p className="text-sm text-muted-foreground">{detail}</p>}
      {children}
    </Card>
  )
}

function Tiles({ totals, siteId }: { totals: Totals; siteId: string | undefined }) {
  const tone = scoreTone(totals.score)
  const issueLink = (overdue: boolean) => (
    <Link
      to="/issues"
      search={{ siteId, overdue: overdue || undefined }}
      className="text-primary underline-offset-4 hover:underline"
    >
      View issues
    </Link>
  )
  return (
    <div className="flex flex-col gap-3">
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Tile label="On-time score" value={formatScore(totals.score)} tone={TONE_TEXT[tone]}>
          {totals.score != null && (
            <div className="mt-1 h-2 overflow-hidden rounded-full bg-muted" aria-hidden>
              <div
                className={cn('h-full rounded-full', TONE_BAR[tone])}
                style={{ width: `${Math.min(100, Math.max(0, totals.score))}%` }}
              />
            </div>
          )}
        </Tile>
        <Tile
          label="Completed today"
          value={`${totals.completed} / ${totals.dueToday}`}
          detail={`${totals.remaining} remaining`}
        />
        <Tile
          label="Overdue tasks"
          value={totals.overdue}
          tone={totals.overdue > 0 ? 'text-destructive' : undefined}
        />
        <Tile
          label="Missed yesterday"
          value={totals.missedYesterday}
          tone={totals.missedYesterday > 0 ? 'text-destructive' : undefined}
        />
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Tile label="Open issues" value={totals.openIssues} detail={issueLink(false)}>
          <ul className="mt-1 flex flex-wrap gap-1.5" aria-label="Open issues by priority">
            {PRIORITIES.map((p) => (
              <li key={p}>
                <span
                  className={cn(
                    'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium',
                    PRIORITY_CLASS[p],
                  )}
                >
                  {PRIORITY_LABEL[p]}
                  <span className="tabular-nums">{totals.openByPriority[p] ?? 0}</span>
                </span>
              </li>
            ))}
          </ul>
        </Tile>
        <Tile
          label="Overdue issues"
          value={totals.overdueIssues}
          tone={totals.overdueIssues > 0 ? 'text-destructive' : undefined}
          detail={totals.overdueIssues > 0 ? issueLink(true) : undefined}
        />
      </div>
    </div>
  )
}

function SiteTable({ rows }: { rows: SiteRow[] }) {
  const [sort, setSort] = useState<{ key: SortKey; dir: 'asc' | 'desc' }>({
    key: 'siteName',
    dir: 'asc',
  })
  const sorted = useMemo(() => sortSites(rows, sort.key, sort.dir), [rows, sort])

  const toggle = (key: SortKey) =>
    setSort((s) =>
      s.key === key
        ? { key, dir: s.dir === 'asc' ? 'desc' : 'asc' }
        : { key, dir: key === 'siteName' ? 'asc' : 'desc' },
    )

  return (
    <section aria-labelledby="by-site" className="flex flex-col gap-2">
      <h2 id="by-site" className="text-lg font-semibold">
        By site
      </h2>
      <Card className="overflow-x-auto p-0">
        <table className="w-full min-w-[44rem] text-left text-sm">
          <thead className="border-b bg-muted/40">
            <tr>
              {COLUMNS.map((c) => {
                const active = sort.key === c.key
                const Icon = !active ? ArrowUpDown : sort.dir === 'asc' ? ArrowUp : ArrowDown
                return (
                  <th
                    key={c.key}
                    scope="col"
                    aria-sort={active ? (sort.dir === 'asc' ? 'ascending' : 'descending') : 'none'}
                    className={cn('px-3 py-2 font-medium', c.numeric && 'text-right')}
                  >
                    <button
                      type="button"
                      onClick={() => toggle(c.key)}
                      className="inline-flex items-center gap-1 rounded-md px-1 py-1 hover:bg-accent"
                    >
                      {c.label}
                      <Icon className="size-3.5" aria-hidden />
                    </button>
                  </th>
                )
              })}
            </tr>
          </thead>
          <tbody>
            {sorted.map((site) => (
              <tr key={site.siteId} className="border-b last:border-0" data-testid="site-row">
                <th scope="row" className="px-3 py-3 font-medium">
                  <Link
                    to="/reports/compliance"
                    search={{ siteId: site.siteId }}
                    className="underline-offset-4 hover:underline"
                  >
                    {site.siteName}
                  </Link>
                </th>
                <td className="px-3 py-3 text-right">
                  <Score score={site.score} />
                </td>
                <td className="px-3 py-3 text-right tabular-nums">
                  {site.completed} / {site.dueToday}
                </td>
                <td
                  className={cn(
                    'px-3 py-3 text-right tabular-nums',
                    site.overdue > 0 && 'font-semibold text-destructive',
                  )}
                >
                  {site.overdue}
                </td>
                <td
                  className={cn(
                    'px-3 py-3 text-right tabular-nums',
                    site.missedYesterday > 0 && 'font-semibold text-destructive',
                  )}
                >
                  {site.missedYesterday}
                </td>
                <td className="px-3 py-3 text-right tabular-nums">
                  <Link
                    to="/issues"
                    search={{ siteId: site.siteId }}
                    className="underline-offset-4 hover:underline"
                  >
                    {site.openIssues}
                  </Link>
                </td>
                <td
                  className={cn(
                    'px-3 py-3 text-right tabular-nums',
                    site.overdueIssues > 0 && 'font-semibold text-destructive',
                  )}
                >
                  {site.overdueIssues}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </section>
  )
}
