import { infiniteQueryOptions, queryOptions } from '@tanstack/react-query'

import type { ApiClient } from '@/api/client'
import { unwrap } from '@/api/unwrap'

import type { TaskKind } from './types'

export const TEMPLATES_PAGE_SIZE = 25

type OrgId = string | undefined

// Under ['org', orgId, ...] like every other area, so a session or organization switch drops it with the rest.
// Writes invalidate ['templates'], which also refreshes the template pickers elsewhere that share the prefix.
const org = (organizationId: OrgId) => ['org', organizationId] as const

export interface TemplatesFilter {
  search: string
  /** '' leaves the server filter off. */
  kind: TaskKind | ''
  tagId: string
  /** The API hides inactive templates unless asked. */
  includeInactive: boolean
}

export const templatesListQuery = (
  client: ApiClient,
  organizationId: OrgId,
  filter: TemplatesFilter,
) =>
  infiniteQueryOptions({
    queryKey: [...org(organizationId), 'templates', 'list', filter],
    initialPageParam: 1,
    queryFn: ({ pageParam }) =>
      unwrap(
        client.GET('/api/v1/templates', {
          params: {
            query: {
              Search: filter.search || undefined,
              Kind: filter.kind || undefined,
              TagId: filter.tagId || undefined,
              IncludeInactive: filter.includeInactive || undefined,
              Page: pageParam,
              PageSize: TEMPLATES_PAGE_SIZE,
            },
          },
        }),
      ),
    getNextPageParam: (last) =>
      (last.hasNextPage ?? last.page * last.pageSize < last.totalCount) ? last.page + 1 : undefined,
  })

export const templateQuery = (client: ApiClient, organizationId: OrgId, id: string) =>
  queryOptions({
    queryKey: [...org(organizationId), 'templates', 'detail', id],
    queryFn: () => unwrap(client.GET('/api/v1/templates/{id}', { params: { path: { id } } })),
  })

// The built-in library never changes while the app runs.
export const libraryQuery = (client: ApiClient, organizationId: OrgId) =>
  queryOptions({
    queryKey: [...org(organizationId), 'template-library', 'list'],
    staleTime: Infinity,
    queryFn: () => unwrap(client.GET('/api/v1/templates/library')),
  })

export const libraryTemplateQuery = (client: ApiClient, organizationId: OrgId, key: string) =>
  queryOptions({
    queryKey: [...org(organizationId), 'template-library', 'detail', key],
    staleTime: Infinity,
    queryFn: () =>
      unwrap(client.GET('/api/v1/templates/library/{key}', { params: { path: { key } } })),
  })
