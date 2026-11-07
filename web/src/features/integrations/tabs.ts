import { z } from 'zod'

export const TABS = ['webhooks', 'api-keys', 'sensors'] as const
export type IntegrationsTab = (typeof TABS)[number]

export const TAB_LABEL: Record<IntegrationsTab, string> = {
  webhooks: 'Webhooks',
  'api-keys': 'API keys',
  sensors: 'Sensors',
}

export const integrationsSearchSchema = z.object({
  tab: z.enum(TABS).optional().catch(undefined),
})
