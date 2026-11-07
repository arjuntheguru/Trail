import { useQuery } from '@tanstack/react-query'

import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'

import { allSitesQuery, departmentsQuery, regionsQuery } from './queries'

/**
 * The sites, regions and departments an access editor can offer. The sites list is already limited to what
 * the caller can see; regions and departments are not, so the server may still refuse some choices.
 */
export function useAccessOptions() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const enabled = usePermission(Permissions.Sites.View)
  const sites = useQuery({ ...allSitesQuery(client, organizationId), enabled })
  const regions = useQuery({ ...regionsQuery(client, organizationId), enabled })
  const departments = useQuery({ ...departmentsQuery(client, organizationId), enabled })
  return {
    available: enabled,
    isPending: enabled && (sites.isPending || regions.isPending || departments.isPending),
    error: sites.error ?? regions.error ?? departments.error,
    refetch: () => {
      void sites.refetch()
      void regions.refetch()
      void departments.refetch()
    },
    sites: sites.data?.items ?? [],
    regions: regions.data ?? [],
    departments: departments.data ?? [],
  }
}
