import { infiniteQueryOptions, queryOptions, type QueryClient } from '@tanstack/react-query'

import type { ApiClient } from '@/api/client'
import { unwrap } from '@/api/unwrap'

import type { toApiFilters } from './search'
import type { TaskDetail } from './types'

export const taskKey = (organizationId: string | undefined, taskId: string) =>
  ['org', organizationId, 'task', taskId] as const

export const taskQuery = (client: ApiClient, organizationId: string | undefined, taskId: string) =>
  queryOptions({
    queryKey: taskKey(organizationId, taskId),
    queryFn: () => unwrap(client.GET('/api/v1/tasks/{id}', { params: { path: { id: taskId } } })),
  })

/** Stores a response the server just returned and marks every Today list stale. */
export function applyTaskUpdate(
  queryClient: QueryClient,
  organizationId: string | undefined,
  task: TaskDetail,
) {
  queryClient.setQueryData(taskKey(organizationId, task.id), task)
  void queryClient.invalidateQueries({ queryKey: ['org', organizationId, 'site'] })
  void queryClient.invalidateQueries({ queryKey: ['org', organizationId, 'tasks', 'list'] })
}

/** The attachment bytes, fetched with the bearer token (an <img src> cannot send it) and cached as a Blob. */
export const attachmentBlobQuery = (
  client: ApiClient,
  organizationId: string | undefined,
  attachmentId: string,
) =>
  queryOptions({
    queryKey: ['org', organizationId, 'attachment', attachmentId, 'content'],
    staleTime: Infinity,
    retry: 1,
    queryFn: async () =>
      unwrap(
        client.GET('/api/v1/attachments/{id}/content', {
          params: { path: { id: attachmentId } },
          parseAs: 'blob',
        }),
      ) as unknown as Promise<Blob>,
  })

export const TASKS_PAGE_SIZE = 20

type TaskFilters = ReturnType<typeof toApiFilters>

/** Cross-site task history for the given filters, a page at a time. */
export const tasksListQuery = (
  client: ApiClient,
  organizationId: string | undefined,
  filters: TaskFilters,
) =>
  infiniteQueryOptions({
    queryKey: ['org', organizationId, 'tasks', 'list', filters],
    initialPageParam: 1,
    queryFn: ({ pageParam }) =>
      unwrap(
        client.GET('/api/v1/tasks', {
          params: { query: { ...filters, Page: pageParam, PageSize: TASKS_PAGE_SIZE } },
        }),
      ),
    getNextPageParam: (last) =>
      (last.hasNextPage ?? last.page * last.pageSize < last.totalCount) ? last.page + 1 : undefined,
  })
