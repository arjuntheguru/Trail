import { infiniteQueryOptions, queryOptions, type QueryClient } from '@tanstack/react-query'

import type { ApiClient } from '@/api/client'
import { unwrap } from '@/api/unwrap'

import { toApiFilters, type IssuesSearch } from './search'
import type { IssueDetail } from './types'

export const ISSUES_PAGE_SIZE = 20

const base = (organizationId: string | undefined) => ['org', organizationId, 'issues'] as const

export const issueKey = (organizationId: string | undefined, issueId: string) =>
  [...base(organizationId), 'detail', issueId] as const

export const issuesListQuery = (
  client: ApiClient,
  organizationId: string | undefined,
  search: IssuesSearch,
) => {
  const filters = toApiFilters(search)
  return infiniteQueryOptions({
    queryKey: [...base(organizationId), 'list', filters],
    initialPageParam: 1,
    queryFn: ({ pageParam }) =>
      unwrap(
        client.GET('/api/v1/issues', {
          params: { query: { ...filters, Page: pageParam, PageSize: ISSUES_PAGE_SIZE } },
        }),
      ),
    getNextPageParam: (last) =>
      (last.hasNextPage ?? last.page * last.pageSize < last.totalCount) ? last.page + 1 : undefined,
  })
}

/** Counts by status, overdue and assigned to the caller; optionally for one site. */
export const issuesSummaryQuery = (
  client: ApiClient,
  organizationId: string | undefined,
  siteId?: string,
) =>
  queryOptions({
    queryKey: [...base(organizationId), 'summary', siteId ?? null],
    staleTime: 30_000,
    queryFn: () =>
      unwrap(client.GET('/api/v1/issues/summary', { params: { query: { SiteId: siteId } } })),
  })

export const issueQuery = (
  client: ApiClient,
  organizationId: string | undefined,
  issueId: string,
) =>
  queryOptions({
    queryKey: issueKey(organizationId, issueId),
    queryFn: () => unwrap(client.GET('/api/v1/issues/{id}', { params: { path: { id: issueId } } })),
  })

/** People the server would accept as assignee for a site (up to 50, optionally matching a search). Needs issues.manage. */
export const assigneeCandidatesQuery = (
  client: ApiClient,
  organizationId: string | undefined,
  siteId: string,
  search: string,
) =>
  queryOptions({
    queryKey: [...base(organizationId), 'assignee-candidates', siteId, search],
    staleTime: 30_000,
    queryFn: () =>
      unwrap(
        client.GET('/api/v1/issues/assignee-candidates', {
          params: { query: { SiteId: siteId, Search: search || undefined } },
        }),
      ),
  })

/** Stores a response the server just returned and marks every list and summary stale. */
export function applyIssueUpdate(
  queryClient: QueryClient,
  organizationId: string | undefined,
  issue: IssueDetail,
) {
  queryClient.setQueryData(issueKey(organizationId, issue.id), issue)
  void queryClient.invalidateQueries({ queryKey: [...base(organizationId), 'list'] })
  void queryClient.invalidateQueries({ queryKey: [...base(organizationId), 'summary'] })
}
