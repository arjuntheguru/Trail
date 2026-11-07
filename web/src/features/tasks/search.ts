import { z } from 'zod'

import { addDays, daysInclusive, isDay, localDay } from '@/lib/dates'

import type { TaskStatus } from './types'

export const RANGES = ['today', 'yesterday', '7d', '30d', 'custom'] as const
export type RangeKey = (typeof RANGES)[number]

export const RANGE_LABEL: Record<RangeKey, string> = {
  today: 'Today',
  yesterday: 'Yesterday',
  '7d': 'Last 7 days',
  '30d': 'Last 30 days',
  custom: 'Custom range',
}

export const TASK_STATUSES = [
  'Open',
  'Completed',
  'Missed',
  'Cancelled',
] as const satisfies readonly TaskStatus[]

/** The API refuses ranges longer than this (ListTasksHandler.MaxRangeDays). */
export const MAX_RANGE_DAYS = 93
export const DEFAULT_RANGE: RangeKey = '7d'

const day = z.string().refine(isDay).optional().catch(undefined)

// Every filter is optional and falls back to "not set" when the URL carries something unusable, so a
// hand-edited or stale link still renders.
export const tasksSearchSchema = z.object({
  range: z.enum(RANGES).optional().catch(undefined),
  from: day,
  to: day,
  siteId: z.string().optional().catch(undefined),
  status: z.enum(TASK_STATUSES).optional().catch(undefined),
  failures: z.boolean().optional().catch(undefined),
  approval: z.boolean().optional().catch(undefined),
})
export type TasksSearch = z.infer<typeof tasksSearchSchema>

export interface ResolvedRange {
  key: RangeKey
  from: string
  to: string
  /** Set when a custom range cannot be sent to the API. */
  error?: string
}

/** The dates the URL stands for, relative to the viewer's today. */
export function resolveRange(search: TasksSearch, now: Date): ResolvedRange {
  const today = localDay(now)
  switch (search.range) {
    case 'today':
      return { key: 'today', from: today, to: today }
    case 'yesterday': {
      const y = addDays(today, -1)
      return { key: 'yesterday', from: y, to: y }
    }
    case '30d':
      return { key: '30d', from: addDays(today, -29), to: today }
    case 'custom':
      if (search.from && search.to) {
        const { from, to } = search
        const error =
          to < from
            ? 'The end date must not be before the start date.'
            : daysInclusive(from, to) > MAX_RANGE_DAYS
              ? `Choose a range of at most ${MAX_RANGE_DAYS} days.`
              : undefined
        return { key: 'custom', from, to, error }
      }
      break
  }
  return { key: '7d', from: addDays(today, -6), to: today }
}

/** The ListTasks query parameters for the URL's filters (paging is added by the caller). */
export function toApiFilters(search: TasksSearch, range: ResolvedRange) {
  return {
    From: range.from,
    To: range.to,
    SiteId: search.siteId,
    Status: search.status,
    HasFailures: search.failures ? true : undefined,
    AwaitingApproval: search.approval ? true : undefined,
  }
}

export function hasActiveFilters(search: TasksSearch, range: ResolvedRange) {
  return !!(
    range.key !== DEFAULT_RANGE ||
    search.siteId ||
    search.status ||
    search.failures ||
    search.approval
  )
}

/** Search params that put every filter back to its default. */
export const CLEARED: TasksSearch = {
  range: undefined,
  from: undefined,
  to: undefined,
  siteId: undefined,
  status: undefined,
  failures: undefined,
  approval: undefined,
}

/** Where the task screen's back link may go: only the two screens that list tasks. */
export function safeBackTarget(from: unknown): string | undefined {
  return typeof from === 'string' && /^\/(today|tasks)(\?.*)?$/.test(from) ? from : undefined
}
