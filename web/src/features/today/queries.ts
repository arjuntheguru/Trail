import { onlineManager, queryOptions } from '@tanstack/react-query'

import type { ApiClient } from '@/api/client'
import { unwrap } from '@/api/unwrap'

/** Active sites the caller can see. One page of 100 is plenty for a picker today. */
export const sitesQuery = (client: ApiClient, organizationId: string | undefined) =>
  queryOptions({
    queryKey: ['org', organizationId, 'sites'],
    queryFn: () =>
      unwrap(
        client.GET('/api/v1/sites', {
          params: { query: { IsActive: true, Page: 1, PageSize: 100 } },
        }),
      ),
  })

/** The site's task list for its own current day (the server resolves "today" in the site time zone). */
export const siteTasksQuery = (
  client: ApiClient,
  organizationId: string | undefined,
  siteId: string | undefined,
) =>
  queryOptions({
    queryKey: ['org', organizationId, 'site', siteId, 'tasks', 'today'],
    enabled: !!siteId,
    // No polling while offline; the reconnect refetch resumes it.
    refetchInterval: () => (onlineManager.isOnline() ? 60_000 : false),
    queryFn: () =>
      unwrap(client.GET('/api/v1/sites/{siteId}/tasks', { params: { path: { siteId: siteId! } } })),
  })
