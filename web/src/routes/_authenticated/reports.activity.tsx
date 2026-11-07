import { createFileRoute } from '@tanstack/react-router'

import { ActivityPage } from '@/features/reports/ActivityPage'

export const Route = createFileRoute('/_authenticated/reports/activity')({
  component: ActivityPage,
})
