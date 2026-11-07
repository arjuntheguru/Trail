import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { Pencil, Plus, Power, PowerOff, Trash2 } from 'lucide-react'
import { useState } from 'react'

import { unwrap } from '@/api/unwrap'
import { useAuthService, useSession } from '@/auth/context'
import { Badge } from '@/components/ui/badge'
import { Button, buttonVariants } from '@/components/ui/button'
import { allSitesQuery, regionsQuery } from '@/features/settings/queries'
import {
  ConfirmDialog,
  EmptyState,
  ListSkeleton,
  LoadError,
  RequirePermission,
  SectionHeader,
} from '@/features/settings/shared'

import {
  SCHEDULES_MANAGE,
  schedulesListQuery,
  templatesPickerQuery,
  type Schedule,
  type SchedulesFilter,
} from './queries'
import { parseWeekDays } from './recurrence'
import { summarizeSchedule } from './summary'

const selectClass = 'h-11 rounded-md border border-input bg-card px-3 text-base md:h-10 md:text-sm'

export function SchedulesPage() {
  return (
    <RequirePermission permission={SCHEDULES_MANAGE}>
      <SchedulesSection />
    </RequirePermission>
  )
}

type Action = { kind: 'activate' | 'deactivate' | 'delete'; schedule: Schedule }

/** Reads "Every weekday, from 06:00, due by 11:00, at 3 sites" from a schedule the API returned. */
function describe(s: Schedule) {
  return summarizeSchedule({
    recurrence: {
      frequency: s.frequency,
      interval: s.interval,
      startDate: s.startDate,
      endDate: s.endDate,
      days: parseWeekDays(s.days),
      dayOfMonth: s.dayOfMonth,
    },
    dueTime: s.dueTime,
    availableFrom: s.availableFrom,
    targets: { allSites: s.allSites, siteCount: s.siteIds.length, regionCount: s.regionIds.length },
  })
}

function SchedulesSection() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const [filter, setFilter] = useState<SchedulesFilter>({
    templateId: '',
    siteId: '',
    status: 'active',
  })
  const schedules = useInfiniteQuery(schedulesListQuery(client, organizationId, filter))
  const templates = useQuery(templatesPickerQuery(client, organizationId, true))
  const sites = useQuery(allSitesQuery(client, organizationId))
  const regions = useQuery(regionsQuery(client, organizationId))
  const [action, setAction] = useState<Action | null>(null)
  const items = schedules.data?.pages.flatMap((p) => p.items) ?? []
  const siteNames = new Map((sites.data?.items ?? []).map((s) => [s.id, s.name]))
  const regionNames = new Map((regions.data ?? []).map((r) => [r.id, r.name]))
  const set = (patch: Partial<SchedulesFilter>) => setFilter((f) => ({ ...f, ...patch }))

  return (
    <section className="flex flex-col gap-4">
      <SectionHeader
        title="Schedules"
        description="Which templates run at which sites, and when."
        action={
          <Link to="/settings/schedules/new" className={buttonVariants()}>
            <Plus aria-hidden />
            Add schedule
          </Link>
        }
      />
      <div className="flex flex-wrap gap-2">
        <select
          aria-label="Template"
          className={selectClass}
          value={filter.templateId}
          onChange={(e) => set({ templateId: e.target.value })}
        >
          <option value="">All templates</option>
          {(templates.data?.items ?? []).map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>
        <select
          aria-label="Site"
          className={selectClass}
          value={filter.siteId}
          onChange={(e) => set({ siteId: e.target.value })}
        >
          <option value="">All sites</option>
          {(sites.data?.items ?? []).map((s) => (
            <option key={s.id} value={s.id}>
              {s.name}
            </option>
          ))}
        </select>
        <select
          aria-label="Status"
          className={selectClass}
          value={filter.status}
          onChange={(e) => set({ status: e.target.value as SchedulesFilter['status'] })}
        >
          <option value="active">Active</option>
          <option value="all">Active and stopped</option>
        </select>
      </div>
      {schedules.isPending ? (
        <ListSkeleton />
      ) : schedules.isError ? (
        <LoadError error={schedules.error} retry={() => void schedules.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState>No schedules found.</EmptyState>
      ) : (
        <ul className="flex flex-col gap-2">
          {items.map((schedule) => {
            const named = [
              ...schedule.regionIds.map((id) => regionNames.get(id)),
              ...schedule.siteIds.map((id) => siteNames.get(id)),
            ].filter((n): n is string => !!n)
            return (
              <li key={schedule.id} className="flex flex-col gap-2 rounded-xl border bg-card p-3">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="min-w-0 truncate font-medium">{schedule.templateName}</span>
                  {!schedule.isActive && <Badge variant="secondary">Stopped</Badge>}
                </div>
                <p className="text-sm">{describe(schedule)}</p>
                {!schedule.allSites && named.length > 0 && (
                  <p className="text-sm text-muted-foreground">{named.join(', ')}</p>
                )}
                <div className="flex flex-wrap gap-2">
                  <Link
                    to="/settings/schedules/$scheduleId"
                    params={{ scheduleId: schedule.id }}
                    aria-label={`Edit schedule ${schedule.templateName}`}
                    className={buttonVariants({ variant: 'outline', size: 'sm' })}
                  >
                    <Pencil aria-hidden />
                    Edit
                  </Link>
                  <Button
                    variant="outline"
                    size="sm"
                    aria-label={`${schedule.isActive ? 'Stop' : 'Resume'} schedule ${schedule.templateName}`}
                    onClick={() =>
                      setAction({ kind: schedule.isActive ? 'deactivate' : 'activate', schedule })
                    }
                  >
                    {schedule.isActive ? <PowerOff aria-hidden /> : <Power aria-hidden />}
                    {schedule.isActive ? 'Stop' : 'Resume'}
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    aria-label={`Delete schedule ${schedule.templateName}`}
                    onClick={() => setAction({ kind: 'delete', schedule })}
                  >
                    <Trash2 aria-hidden />
                    Delete
                  </Button>
                </div>
              </li>
            )
          })}
        </ul>
      )}
      {schedules.hasNextPage && (
        <Button
          variant="outline"
          disabled={schedules.isFetchingNextPage}
          onClick={() => void schedules.fetchNextPage()}
        >
          Load more
        </Button>
      )}
      {action && <ScheduleActionDialog action={action} onClose={() => setAction(null)} />}
    </section>
  )
}

function ScheduleActionDialog({ action, onClose }: { action: Action; onClose: () => void }) {
  const { client } = useAuthService()
  const { kind, schedule } = action
  const params = { params: { path: { id: schedule.id } } }
  const common = { areas: [['schedules']] as const, onClose }
  const name = schedule.templateName
  if (kind === 'delete') {
    return (
      <ConfirmDialog
        {...common}
        title="Delete schedule?"
        description={`The "${name}" schedule will be removed, along with its tasks that are not yet due and untouched. Tasks already started or completed are kept.`}
        confirmLabel="Delete schedule"
        successMessage="Schedule deleted."
        run={() => unwrap(client.DELETE('/api/v1/schedules/{id}', params))}
      />
    )
  }
  if (kind === 'deactivate') {
    return (
      <ConfirmDialog
        {...common}
        title="Stop schedule?"
        description={`No new "${name}" tasks will appear, and untouched upcoming ones are withdrawn. You can resume it at any time.`}
        confirmLabel="Stop schedule"
        destructive={false}
        successMessage="Schedule stopped."
        run={() => unwrap(client.POST('/api/v1/schedules/{id}/deactivate', params))}
      />
    )
  }
  return (
    <ConfirmDialog
      {...common}
      title="Resume schedule?"
      description={`"${name}" tasks will appear on the task lists again.`}
      confirmLabel="Resume schedule"
      destructive={false}
      successMessage="Schedule resumed."
      run={() => unwrap(client.POST('/api/v1/schedules/{id}/activate', params))}
    />
  )
}
