import {
  infiniteQueryOptions,
  queryOptions,
  useMutation,
  useQueryClient,
  type QueryClient,
} from '@tanstack/react-query'

import type { ApiClient } from '@/api/client'
import type { components } from '@/api/schema'
import { unwrap } from '@/api/unwrap'
import { useAuthService, useSession } from '@/auth/context'

export type Notification = components['schemas']['NotificationResponse']
export type NotificationKind = components['schemas']['NotificationKind']
export type NotificationPreferences = components['schemas']['NotificationPreferencesResponse']

export const NOTIFICATIONS_PAGE_SIZE = 20
export const RECENT_COUNT = 10
export const POLL_INTERVAL_MS = 60_000

type OrgId = string | undefined

// A notification belongs to the signed-in membership, so everything hangs off the organization like the rest.
const base = (organizationId: OrgId) => ['org', organizationId, 'notifications'] as const

/**
 * Unread total for the bell. Refetches when the window regains focus and every minute, but not while the tab
 * is hidden (TanStack pauses interval refetches in the background unless told otherwise).
 */
export const unreadCountQuery = (client: ApiClient, organizationId: OrgId) =>
  queryOptions({
    queryKey: [...base(organizationId), 'unread-count'],
    staleTime: 30_000,
    refetchInterval: POLL_INTERVAL_MS,
    refetchIntervalInBackground: false,
    refetchOnWindowFocus: true,
    queryFn: () => unwrap(client.GET('/api/v1/notifications/unread-count')),
  })

/** The latest notifications shown in the bell. */
export const recentNotificationsQuery = (client: ApiClient, organizationId: OrgId) =>
  queryOptions({
    queryKey: [...base(organizationId), 'recent'],
    queryFn: () =>
      unwrap(
        client.GET('/api/v1/notifications', {
          params: { query: { Page: 1, PageSize: RECENT_COUNT } },
        }),
      ),
  })

export const notificationsListQuery = (
  client: ApiClient,
  organizationId: OrgId,
  unreadOnly: boolean,
) =>
  infiniteQueryOptions({
    queryKey: [...base(organizationId), 'list', { unreadOnly }],
    initialPageParam: 1,
    queryFn: ({ pageParam }) =>
      unwrap(
        client.GET('/api/v1/notifications', {
          params: {
            query: {
              UnreadOnly: unreadOnly || undefined,
              Page: pageParam,
              PageSize: NOTIFICATIONS_PAGE_SIZE,
            },
          },
        }),
      ),
    getNextPageParam: (last) =>
      (last.hasNextPage ?? last.page * last.pageSize < last.totalCount) ? last.page + 1 : undefined,
  })

export const notificationPreferencesQuery = (client: ApiClient, organizationId: OrgId) =>
  queryOptions({
    queryKey: [...base(organizationId), 'preferences'],
    queryFn: () => unwrap(client.GET('/api/v1/notifications/preferences')),
  })

/** Marks the count, the bell list and the full list stale (not the preferences). */
function refresh(queryClient: QueryClient, organizationId: OrgId) {
  const key = base(organizationId)
  return Promise.all(
    ['unread-count', 'recent', 'list'].map((part) =>
      queryClient.invalidateQueries({ queryKey: [...key, part] }),
    ),
  )
}

export function useMarkRead() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      unwrap(client.POST('/api/v1/notifications/{id}/read', { params: { path: { id } } })),
    onSuccess: () => refresh(queryClient, organizationId),
  })
}

export function useMarkAllRead() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => unwrap(client.POST('/api/v1/notifications/read-all')),
    onSuccess: () => refresh(queryClient, organizationId),
  })
}
