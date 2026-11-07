import { useQuery } from '@tanstack/react-query'
import { getRouteApi, Link } from '@tanstack/react-router'
import { useMemo } from 'react'

import { errorMessage } from '@/api/problem'
import type { components } from '@/api/schema'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { sitesQuery } from '@/features/today/queries'
import { formatDay } from '@/lib/dates'
import { cn } from '@/lib/utils'

import { ExportPanel } from './ExportPanel'
import { CustomDates, ErrorBlock, RangeSelect, SiteSelect, selectClass } from './Filters'
import { complianceQuery } from './queries'
import { Score } from './score'
import {
  CLEARED_REPORT_FILTERS,
  complianceFilters,
  DEFAULT_GROUPING,
  GROUPING_LABEL,
  GROUPINGS,
  resolveReportRange,
  type Grouping,
  type ReportsSearch,
} from './search'

type Row = components['schemas']['ComplianceRowResponse']

const route = getRouteApi('/_authenticated/reports/compliance')

const COLUMN_HEAD = ['Due', 'On time', 'Late', 'Missed', 'Failures', 'Awaiting approval'] as const

export function CompliancePage() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canSeeSites = usePermission(Permissions.Sites.View)
  const canSeeTasks = usePermission(Permissions.Tasks.View)
  const canSeeIssues = usePermission(Permissions.Issues.View)
  const search = route.useSearch()
  const navigate = route.useNavigate()
  const setFilters = (next: Partial<ReportsSearch>) =>
    void navigate({ search: (prev) => ({ ...prev, ...next }), replace: true })

  const range = resolveReportRange(search, new Date())
  const filters = complianceFilters(search, range)
  const groupBy: Grouping = filters.GroupBy

  const sites = useQuery({ ...sitesQuery(client, organizationId), enabled: canSeeSites })
  const siteList = useMemo(() => sites.data?.items ?? [], [sites.data])
  const report = useQuery({
    ...complianceQuery(client, organizationId, filters),
    enabled: !range.error,
  })

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-col gap-3">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <RangeSelect range={range} onChange={setFilters} />
          {canSeeSites && (
            <SiteSelect
              value={search.siteId}
              sites={siteList}
              onChange={(siteId) => setFilters({ siteId })}
            />
          )}
          <select
            aria-label="Group by"
            value={groupBy}
            onChange={(e) =>
              setFilters({
                groupBy:
                  e.target.value === DEFAULT_GROUPING ? undefined : (e.target.value as Grouping),
              })
            }
            className={selectClass}
          >
            {GROUPINGS.map((g) => (
              <option key={g} value={g}>
                {GROUPING_LABEL[g]}
              </option>
            ))}
          </select>
        </div>
        <CustomDates range={range} onChange={setFilters} />
        <p className="text-sm text-muted-foreground">
          Tasks due {formatDay(range.from)}
          {range.to !== range.from && <> to {formatDay(range.to)}</>}. Score is the share of due
          tasks completed on time; tasks still open today are not counted yet.
        </p>
      </div>

      {range.error ? (
        <p role="alert" className="text-destructive">
          {range.error}
        </p>
      ) : report.isError ? (
        <ErrorBlock message={errorMessage(report.error)} onRetry={() => void report.refetch()} />
      ) : report.isPending ? (
        <div role="status" aria-label="Loading report">
          <Skeleton className="h-64 w-full rounded-xl" />
        </div>
      ) : report.data.rows.length === 0 ? (
        <div className="flex flex-col items-center gap-3 py-12 text-center">
          <h2 className="text-lg font-medium">Nothing was due in this period</h2>
          {(search.siteId || range.key !== '30d' || search.groupBy) && (
            <Button variant="outline" onClick={() => setFilters(CLEARED_REPORT_FILTERS)}>
              Clear filters
            </Button>
          )}
        </div>
      ) : (
        <ComplianceTable
          rows={report.data.rows}
          total={report.data.total}
          groupBy={report.data.groupBy}
          range={range}
          siteId={search.siteId}
          canSeeTasks={canSeeTasks}
          canSeeIssues={canSeeIssues}
        />
      )}

      <ExportPanel
        params={{ from: range.from, to: range.to, siteId: search.siteId }}
        disabled={!!range.error}
      />
    </div>
  )
}

function ComplianceTable({
  rows,
  total,
  groupBy,
  range,
  siteId,
  canSeeTasks,
  canSeeIssues,
}: {
  rows: Row[]
  total: Row
  groupBy: Grouping
  range: { from: string; to: string }
  siteId: string | undefined
  canSeeTasks: boolean
  canSeeIssues: boolean
}) {
  const firstHead = groupBy === 'Site' ? 'Site' : groupBy === 'Day' ? 'Day' : 'Template'

  // Where a row's numbers lead: the Tasks screen for the same sites and days (it has no template filter, so
  // template rows keep the range and site only).
  const tasksLink = (row: Row, status?: 'Missed') => {
    if (!canSeeTasks) return undefined
    const day = groupBy === 'Day'
    return {
      range: 'custom' as const,
      from: day ? row.key : range.from,
      to: day ? row.key : range.to,
      siteId: groupBy === 'Site' ? row.key : siteId,
      status,
    }
  }

  const count = (row: Row, value: number, status?: 'Missed') => {
    const search = tasksLink(row, status)
    if (!search || value === 0 || row === total)
      return <span className="tabular-nums">{value}</span>
    return (
      <Link to="/tasks" search={search} className="tabular-nums underline-offset-4 hover:underline">
        {value}
      </Link>
    )
  }

  return (
    <Card className="overflow-x-auto p-0">
      <table className="w-full min-w-[48rem] text-left text-sm">
        <thead className="border-b bg-muted/40">
          <tr>
            <th scope="col" className="px-3 py-3 font-medium">
              {firstHead}
            </th>
            <th scope="col" className="px-3 py-3 text-right font-medium">
              On-time score
            </th>
            {COLUMN_HEAD.map((h) => (
              <th key={h} scope="col" className="px-3 py-3 text-right font-medium">
                {h}
              </th>
            ))}
            {groupBy === 'Site' && (
              <th scope="col" className="px-3 py-3 text-right font-medium">
                <span className="sr-only">Details</span>
              </th>
            )}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.key} className="border-b" data-testid="compliance-row">
              <th scope="row" className="px-3 py-3 font-medium">
                {groupBy === 'Day' ? formatDay(row.key) : row.name}
              </th>
              <td className="px-3 py-3 text-right">
                <Score score={row.score} />
              </td>
              <td className="px-3 py-3 text-right">{count(row, row.due)}</td>
              <td className="px-3 py-3 text-right tabular-nums">{row.completedOnTime}</td>
              <td className="px-3 py-3 text-right tabular-nums">{row.completedLate}</td>
              <td
                className={cn(
                  'px-3 py-3 text-right',
                  row.missed > 0 && 'font-semibold text-destructive',
                )}
              >
                {count(row, row.missed, 'Missed')}
              </td>
              <td className="px-3 py-3 text-right tabular-nums">{row.withFailures}</td>
              <td className="px-3 py-3 text-right tabular-nums">{row.awaitingApproval}</td>
              {groupBy === 'Site' && (
                <td className="px-3 py-3 text-right whitespace-nowrap">
                  {canSeeIssues && (
                    <Link
                      to="/issues"
                      search={{ siteId: row.key }}
                      className="text-primary underline-offset-4 hover:underline"
                      aria-label={`Issues at ${row.name}`}
                    >
                      Issues
                    </Link>
                  )}
                </td>
              )}
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr className="bg-muted/40 font-semibold" data-testid="compliance-total">
            <th scope="row" className="px-3 py-3">
              Total
            </th>
            <td className="px-3 py-3 text-right">
              <Score score={total.score} />
            </td>
            <td className="px-3 py-3 text-right tabular-nums">{total.due}</td>
            <td className="px-3 py-3 text-right tabular-nums">{total.completedOnTime}</td>
            <td className="px-3 py-3 text-right tabular-nums">{total.completedLate}</td>
            <td className="px-3 py-3 text-right tabular-nums">{total.missed}</td>
            <td className="px-3 py-3 text-right tabular-nums">{total.withFailures}</td>
            <td className="px-3 py-3 text-right tabular-nums">{total.awaitingApproval}</td>
            {groupBy === 'Site' && <td />}
          </tr>
        </tfoot>
      </table>
    </Card>
  )
}
