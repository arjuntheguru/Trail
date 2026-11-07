import { createFileRoute } from '@tanstack/react-router'

import { RegionsPage } from '@/features/settings/RegionsPage'

export const Route = createFileRoute('/_authenticated/settings/regions')({
  component: RegionsPage,
})
