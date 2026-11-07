import type { ApiClient } from '@/api/client'
import { toApiError } from '@/api/problem'
import { fallbackFilename, filenameFromDisposition, saveBlob } from '@/features/reports/download'

import type { AuditSearch } from './search'

/**
 * Downloads reports/audit.csv for the filters the CSV supports (entity type, site, dates). The shared
 * `downloadExport` has no entity-type filter, so this reuses its file-saving helpers. With no dates the
 * API exports its default range (the last 30 days).
 */
export async function downloadAuditCsv(client: ApiClient, search: AuditSearch) {
  const query = {
    From: search.from,
    To: search.to,
    EntityType: search.entityType || undefined,
    SiteId: search.siteId,
  }
  let result
  try {
    result = await client.GET('/api/v1/reports/audit.csv', { params: { query }, parseAs: 'blob' })
  } catch {
    throw toApiError(0, undefined)
  }
  if (!result.response.ok) throw toApiError(result.response.status, result.error)
  const name =
    filenameFromDisposition(result.response.headers.get('content-disposition')) ??
    fallbackFilename('audit', search.from ?? 'default', search.to ?? 'range')
  saveBlob(result.data as unknown as Blob, name)
  return name
}
