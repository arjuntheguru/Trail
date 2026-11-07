import { infiniteQueryOptions, queryOptions, type QueryClient } from '@tanstack/react-query'

import type { ApiClient } from '@/api/client'
import type { components } from '@/api/schema'
import { unwrap } from '@/api/unwrap'

export type Organization = components['schemas']['OrganizationResponse']
export type Region = components['schemas']['RegionResponse']
export type Department = components['schemas']['DepartmentResponse']
export type Tag = components['schemas']['TagResponse']
export type Site = components['schemas']['SiteResponse']
export type Member = components['schemas']['MemberResponse']
export type MemberAccess = components['schemas']['MemberAccessResponse']
export type Role = components['schemas']['RoleResponse']
export type PermissionInfo = components['schemas']['PermissionResponse']
export type MembershipStatus = components['schemas']['MembershipStatus']

export const SETTINGS_PAGE_SIZE = 25

type OrgId = string | undefined

// Everything lives under ['org', orgId, ...] so a session or organization switch drops it with the rest.
// Sites share the 'sites' prefix with the pickers in other areas, so a site change refreshes those too.
const org = (organizationId: OrgId) => ['org', organizationId] as const

/** The sites and regions the caller holds; null when /me does not say (treated as unrestricted). */
export const callerAccessQuery = (client: ApiClient, organizationId: OrgId) =>
  queryOptions({
    queryKey: [...org(organizationId), 'me', 'access'],
    queryFn: async () => (await unwrap(client.GET('/api/v1/me'))).access ?? null,
  })

export const organizationQuery = (client: ApiClient, organizationId: OrgId) =>
  queryOptions({
    queryKey: [...org(organizationId), 'organization'],
    queryFn: () => unwrap(client.GET('/api/v1/organizations/current')),
  })

export const regionsQuery = (client: ApiClient, organizationId: OrgId) =>
  queryOptions({
    queryKey: [...org(organizationId), 'regions'],
    queryFn: () => unwrap(client.GET('/api/v1/regions')),
  })

export const departmentsQuery = (client: ApiClient, organizationId: OrgId) =>
  queryOptions({
    queryKey: [...org(organizationId), 'departments'],
    queryFn: () => unwrap(client.GET('/api/v1/departments')),
  })

export const tagsQuery = (client: ApiClient, organizationId: OrgId) =>
  queryOptions({
    queryKey: [...org(organizationId), 'tags'],
    queryFn: () => unwrap(client.GET('/api/v1/tags')),
  })

export const rolesQuery = (client: ApiClient, organizationId: OrgId) =>
  queryOptions({
    queryKey: [...org(organizationId), 'roles'],
    queryFn: () => unwrap(client.GET('/api/v1/roles')),
  })

export const permissionCatalogQuery = (client: ApiClient, organizationId: OrgId) =>
  queryOptions({
    queryKey: [...org(organizationId), 'roles', 'permissions'],
    staleTime: Infinity,
    queryFn: () => unwrap(client.GET('/api/v1/roles/permissions')),
  })

export interface SitesFilter {
  search: string
  /** 'all' leaves the server filter off. */
  status: 'all' | 'active' | 'inactive'
}

export const sitesListQuery = (client: ApiClient, organizationId: OrgId, filter: SitesFilter) =>
  infiniteQueryOptions({
    queryKey: [...org(organizationId), 'sites', 'settings-list', filter],
    initialPageParam: 1,
    queryFn: ({ pageParam }) =>
      unwrap(
        client.GET('/api/v1/sites', {
          params: {
            query: {
              Search: filter.search || undefined,
              IsActive: filter.status === 'all' ? undefined : filter.status === 'active',
              Page: pageParam,
              PageSize: SETTINGS_PAGE_SIZE,
            },
          },
        }),
      ),
    getNextPageParam: (last) =>
      (last.hasNextPage ?? last.page * last.pageSize < last.totalCount) ? last.page + 1 : undefined,
  })

/** Every site the caller can see (active or not), for pickers. One page of 100 is the API maximum. */
export const allSitesQuery = (client: ApiClient, organizationId: OrgId) =>
  queryOptions({
    queryKey: [...org(organizationId), 'sites', 'all'],
    queryFn: () =>
      unwrap(client.GET('/api/v1/sites', { params: { query: { Page: 1, PageSize: 100 } } })),
  })

export const siteQuery = (client: ApiClient, organizationId: OrgId, siteId: string) =>
  queryOptions({
    queryKey: [...org(organizationId), 'sites', 'detail', siteId],
    queryFn: () => unwrap(client.GET('/api/v1/sites/{id}', { params: { path: { id: siteId } } })),
  })

export interface MembersFilter {
  search: string
  status: 'all' | MembershipStatus
}

export const membersListQuery = (client: ApiClient, organizationId: OrgId, filter: MembersFilter) =>
  infiniteQueryOptions({
    queryKey: [...org(organizationId), 'members', 'list', filter],
    initialPageParam: 1,
    queryFn: ({ pageParam }) =>
      unwrap(
        client.GET('/api/v1/members', {
          params: {
            query: {
              search: filter.search || undefined,
              status: filter.status === 'all' ? undefined : filter.status,
              Page: pageParam,
              PageSize: SETTINGS_PAGE_SIZE,
            },
          },
        }),
      ),
    getNextPageParam: (last) =>
      (last.hasNextPage ?? last.page * last.pageSize < last.totalCount) ? last.page + 1 : undefined,
  })

export const memberQuery = (client: ApiClient, organizationId: OrgId, membershipId: string) =>
  queryOptions({
    queryKey: [...org(organizationId), 'members', 'detail', membershipId],
    queryFn: () =>
      unwrap(client.GET('/api/v1/members/{membershipId}', { params: { path: { membershipId } } })),
  })

export const memberAccessQuery = (client: ApiClient, organizationId: OrgId, membershipId: string) =>
  queryOptions({
    queryKey: [...org(organizationId), 'members', 'access', membershipId],
    queryFn: () =>
      unwrap(
        client.GET('/api/v1/members/{membershipId}/access', { params: { path: { membershipId } } }),
      ),
  })

/** Marks everything under one area (e.g. 'tags', 'members') stale. */
export function invalidateArea(
  queryClient: QueryClient,
  organizationId: OrgId,
  ...area: readonly string[]
) {
  return queryClient.invalidateQueries({ queryKey: [...org(organizationId), ...area] })
}
