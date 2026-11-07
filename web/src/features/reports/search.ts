import { z } from 'zod'

import type { components } from '@/api/schema'
import { addDays, daysInclusive, isDay, localDay } from '@/lib/dates'

export const REPORT_RANGES = ['7d', '30d', '90d', 'custom'] as const
export type ReportRangeKey = (typeof REPORT_RANGES)[number]

export const REPORT_RANGE_LABEL: Record<ReportRangeKey, string> = {
  '7d': 'Last 7 days',
  '30d': 'Last 30 days',
  '90d': 'Last 90 days',
  custom: 'Custom range',
}

export const GROUPINGS = ['Site', 'Day', 'Template'] as const satisfies readonly Grouping[]
export type Grouping = components['schemas']['ComplianceGrouping']
export const GROUPING_LABEL: Record<Grouping, string> = {
  Site: 'By site',
  Day: 'By day',
  Template: 'By template',
}

/** The API refuses report ranges longer than this (ReportRange.MaxDays). */
export const MAX_REPORT_DAYS = 93
export const DEFAULT_REPORT_RANGE: ReportRangeKey = '30d'
export const DEFAULT_GROUPING: Grouping = 'Site'

const day = z.string().refine(isDay).optional().catch(undefined)

// One schema for the whole Reports area so the tabs keep the filters while you move between them. Every
// field is optional and falls back to "not set" when the URL carries something unusable.
export const reportsSearchSchema = z.object({
  range: z.enum(REPORT_RANGES).optional().catch(undefined),
  from: day,
  to: day,
  siteId: z.string().optional().catch(undefined),
  groupBy: z.enum(GROUPINGS).optional().catch(undefined),
})
export type ReportsSearch = z.infer<typeof reportsSearchSchema>

export interface ResolvedReportRange {
  key: ReportRangeKey
  from: string
  to: string
  /** Set when the range cannot be sent to the API. */
  error?: string
}

/** The dates the URL stands for, relative to the viewer's today. */
export function resolveReportRange(search: ReportsSearch, now: Date): ResolvedReportRange {
  const today = localDay(now)
  switch (search.range) {
    case '7d':
      return { key: '7d', from: addDays(today, -6), to: today }
    case '90d':
      return { key: '90d', from: addDays(today, -89), to: today }
    case 'custom':
      if (search.from && search.to) {
        const { from, to } = search
        const error =
          to < from
            ? 'The end date must not be before the start date.'
            : daysInclusive(from, to) > MAX_REPORT_DAYS
              ? `Choose a range of at most ${MAX_REPORT_DAYS} days.`
              : undefined
        return { key: 'custom', from, to, error }
      }
      break
  }
  return { key: '30d', from: addDays(today, -29), to: today }
}

export function complianceFilters(search: ReportsSearch, range: ResolvedReportRange) {
  return {
    From: range.from,
    To: range.to,
    SiteId: search.siteId,
    GroupBy: search.groupBy ?? DEFAULT_GROUPING,
  }
}

/** Search params that put the Reports filters back to their defaults. */
export const CLEARED_REPORT_FILTERS: ReportsSearch = {
  range: undefined,
  from: undefined,
  to: undefined,
  siteId: undefined,
  groupBy: undefined,
}
