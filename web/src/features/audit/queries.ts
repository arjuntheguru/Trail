import { keepPreviousData, queryOptions } from '@tanstack/react-query'

import type { ApiClient } from '@/api/client'
import type { components } from '@/api/schema'
import { unwrap } from '@/api/unwrap'

import { auditApiQuery, type AuditSearch } from './search'

export type AuditEntry = components['schemas']['AuditEntryResponse']

export const auditQuery = (
  client: ApiClient,
  organizationId: string | undefined,
  search: AuditSearch,
) => {
  const query = auditApiQuery(search)
  return queryOptions({
    queryKey: ['org', organizationId, 'audit', query],
    placeholderData: keepPreviousData,
    queryFn: () => unwrap(client.GET('/api/v1/audit', { params: { query } })),
  })
}

/** People who can appear as the actor of a change (needs members.view). */
export const actorsQuery = (client: ApiClient, organizationId: string | undefined) =>
  queryOptions({
    queryKey: ['org', organizationId, 'audit-actors'],
    queryFn: () =>
      unwrap(client.GET('/api/v1/members', { params: { query: { Page: 1, PageSize: 100 } } })),
  })
