import { createFileRoute } from '@tanstack/react-router'

import { SiteFormPage } from '@/features/settings/SiteFormPage'

export const Route = createFileRoute('/_authenticated/settings/sites_/$siteId')({
  component: SiteRoute,
})

function SiteRoute() {
  const { siteId } = Route.useParams()
  return <SiteFormPage key={siteId} siteId={siteId} />
}
