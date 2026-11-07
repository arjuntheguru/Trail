import { z } from 'zod'

import type { components } from '@/api/schema'
import { addDays, isDay } from '@/lib/dates'

export const AUDIT_ACTIONS = [
  'Created',
  'Updated',
  'Deleted',
] as const satisfies readonly components['schemas']['AuditAction'][]

export const AUDIT_PAGE_SIZE = 25

const day = z.string().refine(isDay).optional().catch(undefined)

export const auditSearchSchema = z.object({
  entityType: z.string().trim().max(100).optional().catch(undefined),
  actorId: z.string().optional().catch(undefined),
  siteId: z.string().optional().catch(undefined),
  action: z.enum(AUDIT_ACTIONS).optional().catch(undefined),
  from: day,
  to: day,
  page: z.number().int().min(1).optional().catch(undefined),
})
export type AuditSearch = z.infer<typeof auditSearchSchema>

/** Start of a local calendar day as a UTC instant. */
function startOfDay(value: string) {
  const [y, m, d] = value.split('-').map(Number)
  return new Date(y!, m! - 1, d!).toISOString()
}

/** The list endpoint's query: the range is [from 00:00, day after `to` 00:00) in the viewer's time zone. */
export function auditApiQuery(search: AuditSearch) {
  return {
    EntityType: search.entityType || undefined,
    ActorId: search.actorId,
    SiteId: search.siteId,
    Action: search.action,
    From: search.from ? startOfDay(search.from) : undefined,
    To: search.to ? startOfDay(addDays(search.to, 1)) : undefined,
    Page: search.page ?? 1,
    PageSize: AUDIT_PAGE_SIZE,
  }
}
