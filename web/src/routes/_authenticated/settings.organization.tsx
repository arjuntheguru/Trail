import { createFileRoute } from '@tanstack/react-router'

import { OrganizationPage } from '@/features/settings/OrganizationPage'

export const Route = createFileRoute('/_authenticated/settings/organization')({
  component: OrganizationPage,
})
