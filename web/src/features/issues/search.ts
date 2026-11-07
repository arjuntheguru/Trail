import { z } from 'zod'

import { PRIORITIES, type IssueStatus } from './types'

export const STATUS_FILTERS = ['active', 'open', 'inProgress', 'resolved', 'closed', 'all'] as const
export type StatusFilter = (typeof STATUS_FILTERS)[number]

export const STATUS_FILTER_LABEL: Record<StatusFilter, string> = {
  active: 'Open and in progress',
  open: 'Open',
  inProgress: 'In progress',
  resolved: 'Resolved',
  closed: 'Closed',
  all: 'All statuses',
}

// Every filter is optional and falls back to "not set" when the URL carries something unusable, so a
// hand-edited or stale link still renders. Numeric search terms ("123") are parsed to numbers by the router.
export const issuesSearchSchema = z.object({
  status: z.enum(STATUS_FILTERS).optional().catch(undefined),
  priority: z.enum(PRIORITIES).optional().catch(undefined),
  siteId: z.string().optional().catch(undefined),
  mine: z.boolean().optional().catch(undefined),
  overdue: z.boolean().optional().catch(undefined),
  q: z.union([z.string(), z.number()]).transform(String).optional().catch(undefined),
})
export type IssuesSearch = z.infer<typeof issuesSearchSchema>

/** What the list shows when the URL has no status: everything still needing attention. */
export const DEFAULT_STATUS: StatusFilter = 'active'

const STATUSES: Record<StatusFilter, IssueStatus[] | undefined> = {
  active: ['Open', 'InProgress'],
  open: ['Open'],
  inProgress: ['InProgress'],
  resolved: ['Resolved'],
  closed: ['Closed'],
  all: undefined,
}

/** The ListIssues query parameters for the URL's filters (paging is added by the caller). */
export function toApiFilters(search: IssuesSearch) {
  return {
    Status: STATUSES[search.status ?? DEFAULT_STATUS],
    Priority: search.priority,
    SiteId: search.siteId,
    Mine: search.mine ? true : undefined,
    Overdue: search.overdue ? true : undefined,
    Search: search.q?.trim() || undefined,
  }
}

export function hasActiveFilters(search: IssuesSearch) {
  return !!(
    (search.status && search.status !== DEFAULT_STATUS) ||
    search.priority ||
    search.siteId ||
    search.mine ||
    search.overdue ||
    search.q
  )
}
