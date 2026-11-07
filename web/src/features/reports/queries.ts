import { infiniteQueryOptions, queryOptions } from '@tanstack/react-query'

import type { ApiClient } from '@/api/client'
import { unwrap } from '@/api/unwrap'

import type { complianceFilters } from './search'

const base = (organizationId: string | undefined) => ['org', organizationId, 'reports'] as const

export const ACTIVITY_PAGE_SIZE = 50

/** Today's position at each active site in scope. */
export const dashboardQuery = (
  client: ApiClient,
  organizationId: string | undefined,
  siteId: string | undefined,
) =>
  queryOptions({
    queryKey: [...base(organizationId), 'dashboard', siteId ?? null],
    staleTime: 30_000,
    queryFn: () =>
      unwrap(client.GET('/api/v1/reports/dashboard', { params: { query: { SiteId: siteId } } })),
  })

export const complianceQuery = (
  client: ApiClient,
  organizationId: string | undefined,
  filters: ReturnType<typeof complianceFilters>,
) =>
  queryOptions({
    queryKey: [...base(organizationId), 'compliance', filters],
    queryFn: () => unwrap(client.GET('/api/v1/reports/compliance', { params: { query: filters } })),
  })

/** The activity feed, newest first; each page's `nextBefore` is the cursor for the next one. */
export const activityQuery = (
  client: ApiClient,
  organizationId: string | undefined,
  siteId: string | undefined,
) =>
  infiniteQueryOptions({
    queryKey: [...base(organizationId), 'activity', siteId ?? null],
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) =>
      unwrap(
        client.GET('/api/v1/reports/activity', {
          params: { query: { SiteId: siteId, Before: pageParam, Limit: ACTIVITY_PAGE_SIZE } },
        }),
      ),
    getNextPageParam: (last) => last.nextBefore ?? undefined,
  })
