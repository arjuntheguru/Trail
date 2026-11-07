import { createFileRoute } from '@tanstack/react-router'

import { DashboardPage } from '@/features/reports/DashboardPage'

export const Route = createFileRoute('/_authenticated/reports/')({
  component: DashboardPage,
})
