import { createFileRoute } from '@tanstack/react-router'

import { IntegrationsPage } from '@/features/integrations/IntegrationsPage'
import { integrationsSearchSchema } from '@/features/integrations/tabs'

export const Route = createFileRoute('/_authenticated/settings/integrations')({
  validateSearch: integrationsSearchSchema,
  component: IntegrationsPage,
})
