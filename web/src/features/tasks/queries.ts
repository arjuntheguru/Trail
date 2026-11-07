import { queryOptions, type QueryClient } from '@tanstack/react-query'

import type { ApiClient } from '@/api/client'
import { unwrap } from '@/api/unwrap'

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
