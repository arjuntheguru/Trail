import { createFileRoute } from '@tanstack/react-router'

import { SiteFormPage } from '@/features/settings/SiteFormPage'

export const Route = createFileRoute('/_authenticated/settings/sites_/new')({
  component: () => <SiteFormPage />,
})
