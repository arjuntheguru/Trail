import { createFileRoute } from '@tanstack/react-router'

import { SchedulesPage } from '@/features/schedules/SchedulesPage'

export const Route = createFileRoute('/_authenticated/settings/schedules')({
  component: SchedulesPage,
})
