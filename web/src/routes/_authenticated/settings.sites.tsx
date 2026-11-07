import { createFileRoute } from '@tanstack/react-router'

import { SitesPage } from '@/features/settings/SitesPage'

export const Route = createFileRoute('/_authenticated/settings/sites')({
  component: SitesPage,
})
