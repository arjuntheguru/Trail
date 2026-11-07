import { infiniteQueryOptions, queryOptions } from '@tanstack/react-query'

import type { ApiClient } from '@/api/client'
import type { components } from '@/api/schema'
import { unwrap } from '@/api/unwrap'
import { Permissions } from '@/auth/permissions'

export type Schedule = components['schemas']['ScheduleResponse']
export type ScheduleCommand = components['schemas']['CreateScheduleCommand']
export type TemplateOption = components['schemas']['TemplateSummaryResponse']

/** Needed for every schedule endpoint (see Trail.Application/Common/Security/Permissions.cs). */
export const SCHEDULES_MANAGE = Permissions.Schedules.Manage

export const SCHEDULES_PAGE_SIZE = 25

type OrgId = string | undefined

// Under ['org', orgId, ...] like the settings queries, so an organization switch drops them with the rest.
const org = (organizationId: OrgId) => ['org', organizationId] as const

export interface SchedulesFilter {
  templateId: string
  siteId: string
  /** 'all' also lists stopped schedules. */
  status: 'active' | 'all'
}

export const schedulesListQuery = (
  client: ApiClient,
  organizationId: OrgId,
  filter: SchedulesFilter,
) =>
  infiniteQueryOptions({
    queryKey: [...org(organizationId), 'schedules', 'list', filter],
    initialPageParam: 1,
    queryFn: ({ pageParam }) =>
      unwrap(
        client.GET('/api/v1/schedules', {
          params: {
            query: {
              TemplateId: filter.templateId || undefined,
              SiteId: filter.siteId || undefined,
              IncludeInactive: filter.status === 'all',
              Page: pageParam,
              PageSize: SCHEDULES_PAGE_SIZE,
            },
          },
        }),
      ),
    getNextPageParam: (last) =>
      (last.hasNextPage ?? last.page * last.pageSize < last.totalCount) ? last.page + 1 : undefined,
  })

export const scheduleQuery = (client: ApiClient, organizationId: OrgId, id: string) =>
  queryOptions({
    queryKey: [...org(organizationId), 'schedules', 'detail', id],
    queryFn: () => unwrap(client.GET('/api/v1/schedules/{id}', { params: { path: { id } } })),
  })

/** Templates for pickers (active only unless asked). One page of 100 is the API maximum. */
export const templatesPickerQuery = (
  client: ApiClient,
  organizationId: OrgId,
  includeInactive = false,
) =>
  queryOptions({
    queryKey: [...org(organizationId), 'templates', 'schedule-picker', includeInactive],
    queryFn: () =>
      unwrap(
        client.GET('/api/v1/templates', {
          params: { query: { IncludeInactive: includeInactive, Page: 1, PageSize: 100 } },
        }),
      ),
  })
